// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the mode information of a single single-reference inter block, orchestrating the reference
/// selection, motion-vector prediction, inter-mode and dynamic-reference-list parse, motion-vector
/// residual, interpolation filter and motion mode in specification order. After decoding it writes the
/// block back into the motion-vector grid and the inter neighbour-context store. This is a port of the
/// single-reference inter branch of the reference decoder's <c>decode_b</c>.
/// </summary>
internal static class Av1InterModeInfoDecoder
{
    // Dynamic-reference-list index below which a predictor is precision-fixed.
    private const int NearDrl = 2;

    /// <summary>
    /// Decodes a single-reference inter block and updates the neighbour state.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="interCdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="mvCdf">The tile's adaptive motion-vector CDFs.</param>
    /// <param name="filterCdf">The tile's adaptive interpolation-filter CDFs.</param>
    /// <param name="motionModeCdf">The tile's adaptive motion-mode CDFs.</param>
    /// <param name="grid">The motion-vector reference grid.</param>
    /// <param name="neighbours">The inter neighbour-context store.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="options">The frame-level inter parameters.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <param name="topRightAvailable">Whether the top-right neighbour is available.</param>
    /// <param name="readMotionMode">Whether the block carries a coded motion mode.</param>
    /// <param name="skipMode">Whether the block uses skip mode (recorded for neighbours).</param>
    /// <returns>The decoded inter block info.</returns>
    public static Av1InterBlockInfo Decode(
        Av1SymbolDecoder decoder,
        Av1InterModeCdfContext interCdf,
        Av1MotionVectorCdfContext mvCdf,
        Av1InterpolationFilterCdfContext filterCdf,
        Av1MotionModeCdfContext motionModeCdf,
        Av1MotionVectorGrid grid,
        Av1InterNeighbourContext neighbours,
        int bx4,
        int by4,
        Av1BlockSize blockSize,
        in Av1InterModeInfoOptions options,
        bool haveTop,
        bool haveLeft,
        bool topRightAvailable,
        bool readMotionMode,
        bool skipMode)
    {
        int bw4 = blockSize.GetWidth4();
        int bh4 = blockSize.GetHeight4();

        Av1ReferenceNeighbour above = neighbours.GetAbove(bx4);
        Av1ReferenceNeighbour left = neighbours.GetLeft(by4);

        // Reference frame (zero-based).
        int[] referenceContexts = Av1ReferenceContext.ComputeSingleReferenceContexts(above, left, haveTop, haveLeft);
        int reference = Av1ReferenceFrameReader.ReadSingleReference(decoder, interCdf, referenceContexts);

        // The block's global-motion vector for its reference (dav1d tgmv): the predictor list fills
        // towards it, GLOBALMV blocks resolve to it, and neighbours coded as GLOBALMV substitute it when
        // the model is non-translational.
        Obu.Av1WarpedMotionParams globalModel = options.GlobalMotion[reference];
        Av1MotionVector globalMv = Prediction.Av1WarpedMotion.GetGlobalMv(
            globalModel, bx4, by4, bw4, bh4, options.AllowHighPrecisionMv, options.ForceIntegerMv);
        bool globalMvSubstitution = globalModel.Type > Obu.Av1WarpModelType.Translation;

        // Motion-vector candidate list and mode context.
        Av1MotionVectorStack stack = new();
        (int candidateCount, int modeContext) = Av1MotionVectorFinder.Find(
            grid, stack, bx4, by4, blockSize, reference + 1, options.Bounds, topRightAvailable,
            options.ImageWidth4, options.ImageHeight4, globalMv, globalMvSubstitution, options.SignBias, options.Temporal);

        Span<Av1MotionVectorCandidate> candidates = stackalloc Av1MotionVectorCandidate[8];
        stack.CopyTo(candidates);

        (Av1InterPredictionMode mode, int drlIndex) = Av1InterModeReader.ReadMode(
            decoder, interCdf, modeContext, candidateCount, candidates, forceGlobalMv: false);

        // Resolve the motion vector.
        Av1MotionVector motionVector = ResolveMotionVector(
            decoder, mvCdf, stack, candidateCount, mode, drlIndex, globalMv, options);

        // has_subpel_filter: always set unless this is a non-translation global-motion block.
        bool hasSubpelFilter = mode != Av1InterPredictionMode.GlobalMv
            || Math.Min(bw4, bh4) == 1
            || globalModel.Type == Obu.Av1WarpModelType.Translation;

        // Motion mode (read before the subpel filter so a warp block can clear it). A GLOBALMV block
        // predicted by a warped (non-translational) global model carries no coded motion mode. The
        // WARP choice is only offered when a same-reference neighbour exists on the block edges; a
        // warp block then derives its local model from those neighbours (falling back to translation
        // when the fit degenerates or shears too strongly).
        Av1MotionMode motionMode = Av1MotionMode.Translation;
        int[]? warpMatrix = null;
        short[]? warpShear = null;
        bool isWarpedGlobalMv = mode == Av1InterPredictionMode.GlobalMv
            && !options.ForceIntegerMv
            && globalModel.Type > Obu.Av1WarpModelType.Translation;
        if (readMotionMode && !isWarpedGlobalMv)
        {
            int w4 = Math.Min(bw4, options.ImageWidth4 - bx4);
            int h4 = Math.Min(bh4, options.ImageHeight4 - by4);
            Span<ulong> masks = stackalloc ulong[2];
            Av1WarpDerivation.FindMatchingRef(
                grid, bx4, by4, bw4, bh4, w4, h4, haveLeft, haveTop, topRightAvailable,
                options.Bounds.ColumnEnd, reference, masks);
            bool allowWarp = !options.ForceIntegerMv
                && options.AllowWarpedMotion
                && (masks[0] | masks[1]) != 0;

            motionMode = Av1MotionModeReader.ReadMotionMode(decoder, motionModeCdf, blockSize, allowWarp);
            if (motionMode == Av1MotionMode.Warp)
            {
                hasSubpelFilter = false;
                int[] localMatrix = new int[6];
                short[] localShear = new short[4];
                if (Av1WarpDerivation.TryDeriveWarpMv(grid, bx4, by4, bw4, bh4, masks, motionVector, localMatrix, localShear))
                {
                    warpMatrix = localMatrix;
                    warpShear = localShear;
                }
            }
        }

        // Interpolation filter.
        int filter0;
        int filter1;
        if (options.FilterSwitchable)
        {
            int horizontalContext = Av1ReferenceContext.ComputeFilterContext(above, left, isCompound: false, direction: 0, reference);
            int verticalContext = Av1ReferenceContext.ComputeFilterContext(above, left, isCompound: false, direction: 1, reference);
            (filter0, filter1) = Av1InterpolationFilterReader.ReadFilters(
                decoder, filterCdf, hasSubpelFilter, options.DualFilter, horizontalContext, verticalContext);
        }
        else
        {
            filter0 = options.FixedFilter;
            filter1 = options.FixedFilter;
        }

        // Write the block back into the grid and the neighbour context.
        bool isNewMv = mode == Av1InterPredictionMode.NewMv;
        bool isGlobalMv = mode == Av1InterPredictionMode.GlobalMv && Math.Min(bw4, bh4) >= 2;
        Av1RefMvsBlock gridBlock = new(
            motionVector, default, reference + 1, -1, blockSize, isNewMv, isGlobalMv, isIntra: false);
        grid.Fill(by4, bx4, bw4, bh4, gridBlock);
        neighbours.Write(by4, bx4, bw4, bh4, isIntra: false, reference, -1, isCompound: false, filter0, filter1, skipMode);

        return new Av1InterBlockInfo(reference, mode, drlIndex, motionVector, filter0, filter1, motionMode, warpMatrix, warpShear);
    }

    // dav1d wedge_allowed_mask / dav1d_wedge_ctx_lut, indexed by Av1BlockSize.
    private static readonly int[] WedgeContextLut = CreateWedgeContextLut();

    private static readonly int WedgeAllowedMask = CreateWedgeAllowedMask();

    private static int[] CreateWedgeContextLut()
    {
        int[] lut = new int[32];
        lut[(int)Av1BlockSize.Block32x32] = 6;
        lut[(int)Av1BlockSize.Block32x16] = 5;
        lut[(int)Av1BlockSize.Block32x8] = 8;
        lut[(int)Av1BlockSize.Block16x32] = 4;
        lut[(int)Av1BlockSize.Block16x16] = 3;
        lut[(int)Av1BlockSize.Block16x8] = 2;
        lut[(int)Av1BlockSize.Block8x32] = 7;
        lut[(int)Av1BlockSize.Block8x16] = 1;
        lut[(int)Av1BlockSize.Block8x8] = 0;
        return lut;
    }

    private static int CreateWedgeAllowedMask()
        => (1 << (int)Av1BlockSize.Block32x32) | (1 << (int)Av1BlockSize.Block32x16) | (1 << (int)Av1BlockSize.Block32x8)
         | (1 << (int)Av1BlockSize.Block16x32) | (1 << (int)Av1BlockSize.Block16x16) | (1 << (int)Av1BlockSize.Block16x8)
         | (1 << (int)Av1BlockSize.Block8x32) | (1 << (int)Av1BlockSize.Block8x16) | (1 << (int)Av1BlockSize.Block8x8);

    // dav1d_comp_inter_pred_modes: the two component modes of each compound inter mode
    // (0 = NEARESTMV, 1 = NEARMV, 2 = GLOBALMV, 3 = NEWMV).
    private static readonly byte[][] CompoundModeComponents =
    [
        [0, 0], [1, 1], [0, 3], [3, 0], [1, 3], [3, 1], [2, 2], [3, 3],
    ];

    /// <summary>The GLOBALMV_GLOBALMV compound inter mode.</summary>
    public const int CompoundGlobalGlobal = 6;

    /// <summary>The NEWMV_NEWMV compound inter mode.</summary>
    public const int CompoundNewNew = 7;

    /// <summary>
    /// Decodes a compound (two-reference) inter block: the reference pair (explicit, or the frame's
    /// derived skip-mode pair), the compound candidate list, the compound inter mode with its
    /// dynamic-reference-list index, both motion vectors and the interpolation filter, then writes the
    /// block back into the grid and neighbour context (dav1d's compound branch of <c>decode_b</c>).
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="interCdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="mvCdf">The tile's adaptive motion-vector CDFs.</param>
    /// <param name="filterCdf">The tile's adaptive interpolation-filter CDFs.</param>
    /// <param name="grid">The motion-vector reference grid.</param>
    /// <param name="neighbours">The inter neighbour-context store.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="options">The frame-level inter parameters.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <param name="topRightAvailable">Whether the top-right neighbour is available.</param>
    /// <param name="skipMode">Whether the block uses skip mode (forced pair and mode, no coded syntax).</param>
    /// <param name="skipModeReference0">The frame's first skip-mode reference.</param>
    /// <param name="skipModeReference1">The frame's second skip-mode reference.</param>
    /// <returns>The decoded compound block info.</returns>
    public static Av1InterBlockInfo DecodeCompound(
        Av1SymbolDecoder decoder,
        Av1InterModeCdfContext interCdf,
        Av1MotionVectorCdfContext mvCdf,
        Av1InterpolationFilterCdfContext filterCdf,
        Av1MotionVectorGrid grid,
        Av1InterNeighbourContext neighbours,
        int bx4,
        int by4,
        Av1BlockSize blockSize,
        in Av1InterModeInfoOptions options,
        bool haveTop,
        bool haveLeft,
        bool topRightAvailable,
        bool skipMode,
        int skipModeReference0,
        int skipModeReference1)
    {
        int bw4 = blockSize.GetWidth4();
        int bh4 = blockSize.GetHeight4();

        Av1ReferenceNeighbour above = neighbours.GetAbove(bx4);
        Av1ReferenceNeighbour left = neighbours.GetLeft(by4);

        int reference0;
        int reference1;
        if (skipMode)
        {
            reference0 = skipModeReference0;
            reference1 = skipModeReference1;
        }
        else
        {
            int[] referenceContexts = Av1ReferenceContext.ComputeSingleReferenceContexts(above, left, haveTop, haveLeft);
            (reference0, reference1) = Av1ReferenceFrameReader.ReadCompoundReferences(
                decoder, interCdf, above, left, haveTop, haveLeft, referenceContexts);
        }

        Obu.Av1WarpedMotionParams model0 = options.GlobalMotion[reference0];
        Obu.Av1WarpedMotionParams model1 = options.GlobalMotion[reference1];
        Av1MotionVector globalMv0 = Prediction.Av1WarpedMotion.GetGlobalMv(
            model0, bx4, by4, bw4, bh4, options.AllowHighPrecisionMv, options.ForceIntegerMv);
        Av1MotionVector globalMv1 = Prediction.Av1WarpedMotion.GetGlobalMv(
            model1, bx4, by4, bw4, bh4, options.AllowHighPrecisionMv, options.ForceIntegerMv);

        Av1CompoundMotionVectorStack stack = new();
        (int candidateCount, int modeContext) = Av1MotionVectorFinder.FindCompound(
            grid, stack, bx4, by4, blockSize, reference0 + 1, reference1 + 1, options.Bounds, topRightAvailable,
            options.ImageWidth4, options.ImageHeight4,
            globalMv0, model0.Type > Obu.Av1WarpModelType.Translation,
            globalMv1, model1.Type > Obu.Av1WarpModelType.Translation,
            options.SignBias, options.Temporal);

        int compoundMode = 0;
        int drlIndex = 0;
        if (!skipMode)
        {
            compoundMode = decoder.ReadSymbol(interCdf.CompoundInterMode[modeContext]);

            Span<Av1MotionVectorCandidate> weights = stackalloc Av1MotionVectorCandidate[8];
            for (int i = 0; i < 8; i++)
            {
                weights[i] = new Av1MotionVectorCandidate(default, stack.Weight(i));
            }

            byte[] components = CompoundModeComponents[compoundMode];
            if (compoundMode == CompoundNewNew)
            {
                if (candidateCount > 1)
                {
                    drlIndex += decoder.ReadSymbol(interCdf.DrlBit[Av1InterModeReader.GetDrlContext(weights, 0)]);
                    if (drlIndex == 1 && candidateCount > 2)
                    {
                        drlIndex += decoder.ReadSymbol(interCdf.DrlBit[Av1InterModeReader.GetDrlContext(weights, 1)]);
                    }
                }
            }
            else if (components[0] == 1 || components[1] == 1)
            {
                drlIndex = 1;
                if (candidateCount > 2)
                {
                    drlIndex += decoder.ReadSymbol(interCdf.DrlBit[Av1InterModeReader.GetDrlContext(weights, 1)]);
                    if (drlIndex == 2 && candidateCount > 3)
                    {
                        drlIndex += decoder.ReadSymbol(interCdf.DrlBit[Av1InterModeReader.GetDrlContext(weights, 2)]);
                    }
                }
            }
        }

        // Assign both motion vectors; a NEW component reads a residual on top of its predictor.
        bool hasSubpelFilter = Math.Min(bw4, bh4) == 1 || compoundMode != CompoundGlobalGlobal;
        if (skipMode)
        {
            hasSubpelFilter = false;
        }

        Av1MotionVector motionVector0 = AssignCompoundComponent(
            decoder, mvCdf, stack, drlIndex, CompoundModeComponents[compoundMode][0], 0, globalMv0, model0, options, ref hasSubpelFilter);
        Av1MotionVector motionVector1 = AssignCompoundComponent(
            decoder, mvCdf, stack, drlIndex, CompoundModeComponents[compoundMode][1], 1, globalMv1, model1, options, ref hasSubpelFilter);

        // Compound type: with masked compound enabled a flag selects the masked (wedge/segmented)
        // blend; distance-weighted compound stays rejected at construction, so the unmasked case is
        // always the plain average. Skip-mode blocks are always averaged with no coded type.
        const int CompoundAverage = 2;
        const int CompoundSeg = 3;
        const int CompoundWedge = 4;
        int compoundType = CompoundAverage;
        bool maskSign = false;
        if (!skipMode && options.EnableMaskedCompound)
        {
            int maskContext = Av1ReferenceContext.ComputeMaskCompoundContext(above, left);
            bool isSegWedge = decoder.ReadSymbol(interCdf.MaskComp[maskContext]) != 0;
            if (isSegWedge)
            {
                if ((WedgeAllowedMask & (1 << (int)blockSize)) != 0)
                {
                    int wedgeContext = WedgeContextLut[(int)blockSize];
                    compoundType = CompoundWedge - decoder.ReadSymbol(interCdf.WedgeComp[wedgeContext]);
                    if (compoundType == CompoundWedge)
                    {
                        _ = decoder.ReadSymbol(interCdf.WedgeIdx[wedgeContext]);
                        throw new NotSupportedException("Wedge-masked compound prediction is not supported yet.");
                    }
                }
                else
                {
                    compoundType = CompoundSeg;
                }

                maskSign = decoder.ReadBool() != 0;
            }
        }

        // Interpolation filter.
        int filter0;
        int filter1;
        if (options.FilterSwitchable)
        {
            int horizontalContext = Av1ReferenceContext.ComputeFilterContext(above, left, isCompound: true, direction: 0, reference0);
            int verticalContext = Av1ReferenceContext.ComputeFilterContext(above, left, isCompound: true, direction: 1, reference0);
            (filter0, filter1) = Av1InterpolationFilterReader.ReadFilters(
                decoder, filterCdf, hasSubpelFilter, options.DualFilter, horizontalContext, verticalContext);
        }
        else
        {
            filter0 = options.FixedFilter;
            filter1 = options.FixedFilter;
        }

        // Write the block back into the grid and the neighbour context (dav1d splat_tworef_mv).
        bool isNewMv = ((1 << compoundMode) & 0xbc) != 0;
        bool isGlobalMv = compoundMode == CompoundGlobalGlobal;
        Av1RefMvsBlock gridBlock = new(
            motionVector0, motionVector1, reference0 + 1, reference1 + 1, blockSize, isNewMv, isGlobalMv, isIntra: false);
        grid.Fill(by4, bx4, bw4, bh4, gridBlock);
        neighbours.Write(by4, bx4, bw4, bh4, isIntra: false, reference0, reference1, isCompound: true, filter0, filter1, skipMode, compoundType);

        return new Av1InterBlockInfo(
            reference0, (Av1InterPredictionMode)CompoundModeComponents[compoundMode][0], drlIndex, motionVector0,
            filter0, filter1, Av1MotionMode.Translation, null, null, reference1, motionVector1, compoundMode, compoundType, maskSign);
    }

    private static Av1MotionVector AssignCompoundComponent(
        Av1SymbolDecoder decoder,
        Av1MotionVectorCdfContext mvCdf,
        Av1CompoundMotionVectorStack stack,
        int drlIndex,
        int componentMode,
        int component,
        Av1MotionVector globalMv,
        Obu.Av1WarpedMotionParams model,
        in Av1InterModeInfoOptions options,
        ref bool hasSubpelFilter)
    {
        Av1MotionVector candidate = component == 0 ? stack.Mv0(drlIndex) : stack.Mv1(drlIndex);
        switch (componentMode)
        {
            case 0: // NEARESTMV
            case 1: // NEARMV
                return Av1MotionVectorPrecision.Fix(candidate, options.AllowHighPrecisionMv, options.ForceIntegerMv);

            case 2: // GLOBALMV
                hasSubpelFilter |= model.Type == Obu.Av1WarpModelType.Translation;
                return globalMv;

            default: // NEWMV
            {
                int precision = (options.AllowHighPrecisionMv ? 1 : 0) - (options.ForceIntegerMv ? 1 : 0);
                return Av1MotionVectorReader.ReadResidual(decoder, mvCdf, candidate, precision);
            }
        }
    }

    private static Av1MotionVector ResolveMotionVector(
        Av1SymbolDecoder decoder,
        Av1MotionVectorCdfContext mvCdf,
        Av1MotionVectorStack stack,
        int candidateCount,
        Av1InterPredictionMode mode,
        int drlIndex,
        Av1MotionVector globalMv,
        in Av1InterModeInfoOptions options)
    {
        switch (mode)
        {
            case Av1InterPredictionMode.GlobalMv:
                return globalMv;

            case Av1InterPredictionMode.NewMv:
            {
                Av1MotionVector predictor;
                if (candidateCount > 1)
                {
                    predictor = stack[drlIndex].MotionVector;
                }
                else
                {
                    predictor = Av1MotionVectorPrecision.Fix(stack[0].MotionVector, options.AllowHighPrecisionMv, options.ForceIntegerMv);
                }

                int precision = (options.AllowHighPrecisionMv ? 1 : 0) - (options.ForceIntegerMv ? 1 : 0);
                return Av1MotionVectorReader.ReadResidual(decoder, mvCdf, predictor, precision);
            }

            default:
            {
                Av1MotionVector candidate = stack[drlIndex].MotionVector;
                if (drlIndex < NearDrl)
                {
                    candidate = Av1MotionVectorPrecision.Fix(candidate, options.AllowHighPrecisionMv, options.ForceIntegerMv);
                }

                return candidate;
            }
        }
    }
}
