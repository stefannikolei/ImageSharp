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
