// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the tiles of an inter frame, reusing the shared reconstruction and post-filter pipeline of
/// <see cref="Av1TileDecoder"/> and overriding only the per-block prediction. Each block reads its skip
/// flag and is-inter flag; inter blocks then decode the reference, motion vector and filters
/// (<see cref="Av1InterModeInfoDecoder"/>), motion-compensate from the reference frame
/// (<see cref="Av1InterPredictor"/>) and add the residual through the shared transform-block loop, which
/// supports both the uniform transform mode and the <c>TX_MODE_SELECT</c> variable-transform tree.
/// Intra blocks inside the inter frame read their luma mode from the inter-frame y_mode CDF and reuse
/// the shared intra body. Frames may inherit their CDF state via <c>primary_ref_frame</c> and predict
/// motion vectors temporally via <c>use_ref_frame_mvs</c>. Single-reference prediction covers
/// translation, global and local warp, overlapped and inter-intra blocks; compound prediction covers
/// the average, distance-agnostic masked (segmented and wedge) blends and skip mode. Only
/// distance-weighted compound remains guarded behind its sequence flag.
/// </summary>
internal sealed class Av1InterTileDecoder : Av1TileDecoder
{
    // The reference frames, indexed by the zero-based reference name (LAST .. ALTREF).
    private readonly Av1ReferenceFrame?[] references;

    private Av1InterModeCdfContext interCdf;
    private Av1MotionVectorCdfContext mvCdf;
    private Av1InterpolationFilterCdfContext filterCdf;
    private Av1MotionModeCdfContext motionModeCdf;
    private Av1InterTransformTypeCdfContext interTransformTypeCdf;
    private ushort[][][] transformPartitionCdf;

    // The inter variable-transform size neighbour context (dav1d's BlockContext.tx, distinct from the
    // intra tx_intra context in the base class). It stores the transform width/height category (log2 of
    // the size in 4x4 units) and is reset to TX_64X64 (category 4) at the frame top and each superblock row.
    private readonly sbyte[] interAboveTx;
    private readonly sbyte[] interLeftTx;

    // The per-4x4 luma transform type, recorded while the luma residual is decoded and read back when an
    // inter block's chroma transform type is inferred from the co-located luma type (dav1d's txtp_map).
    private readonly Av1TransformType[] lumaTransformTypes;
    private readonly Av1MotionVectorGrid grid;
    private readonly Av1InterNeighbourContext interNeighbours;
    private Av1InterModeInfoOptions options;
    private readonly int interpolationFilter;

    // The current block's prediction source: set true around an inter block so the Predict override
    // substitutes the motion-compensated samples instead of an intra prediction.
    private bool currentBlockIsInter;

    // Per reference: the derived shear parameters of an applicable global-motion warp model, or null
    // when the reference's model is identity/translation or too sheared to warp.
    private readonly short[]?[] globalWarpShear = new short[7][];

    /// <summary>Initializes a new instance of the <see cref="Av1InterTileDecoder"/> class with a single
    /// reference frame used for every reference name (two-frame clips, where all slots hold the key frame).</summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The inter frame header.</param>
    /// <param name="reference">The reference frame used for every reference name.</param>
    public Av1InterTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1ReferenceFrame reference)
        : this(sequenceHeader, frameHeader, CreateUniformReferences(reference))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Av1InterTileDecoder"/> class starting from
    /// the default CDF tables (frames with <c>primary_ref_frame</c> set to NONE).</summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The inter frame header.</param>
    /// <param name="references">The reference frames, indexed by the zero-based reference name (LAST .. ALTREF).</param>
    public Av1InterTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1ReferenceFrame?[] references)
        : this(sequenceHeader, frameHeader, references, Av1FrameCdfSet.CreateDefault(frameHeader.BaseQIndex))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Av1InterTileDecoder"/> class.</summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The inter frame header.</param>
    /// <param name="references">The reference frames, indexed by the zero-based reference name (LAST .. ALTREF).</param>
    /// <param name="cdfs">The initial CDF state (defaults, or a copy of the primary reference's saved state).</param>
    public Av1InterTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1ReferenceFrame?[] references, Av1FrameCdfSet cdfs)
        : base(sequenceHeader, frameHeader, cdfs)
    {
        if (frameHeader.ReferenceSelect && sequenceHeader.EnableJntComp)
        {
            throw new NotSupportedException("Distance-weighted compound prediction is not supported yet.");
        }

        this.interCdf = cdfs.InterMode;
        this.mvCdf = cdfs.MotionVector;
        this.filterCdf = cdfs.Filter;
        this.motionModeCdf = cdfs.MotionMode;
        this.interTransformTypeCdf = cdfs.InterTransformType;
        this.transformPartitionCdf = cdfs.TransformPartition;
        this.references = references;
        this.interpolationFilter = frameHeader.InterpolationFilter;

        int columns4 = frameHeader.ModeInfoColumns;
        int rows4 = frameHeader.ModeInfoRows;
        this.interAboveTx = new sbyte[columns4];
        this.interLeftTx = new sbyte[rows4];
        Array.Fill(this.interAboveTx, (sbyte)4);
        Array.Fill(this.interLeftTx, (sbyte)4);
        this.lumaTransformTypes = new Av1TransformType[columns4 * rows4];
        this.grid = new Av1MotionVectorGrid(columns4, rows4);
        this.interNeighbours = new Av1InterNeighbourContext(columns4, rows4);

        // Sign bias: whether each reference lies in the future of the current frame.
        int[] signBias = new int[7];
        int orderHintBits = sequenceHeader.OrderHintBits;
        if (orderHintBits > 0)
        {
            for (int i = 0; i < 7; i++)
            {
                int refHint = references[i]?.OrderHint ?? 0;
                signBias[i] = Av1TemporalMvs.GetOrderHintDifference(orderHintBits, refHint, frameHeader.OrderHint) > 0 ? 1 : 0;
            }
        }

        this.options = new Av1InterModeInfoOptions(
            new Av1TileBounds(0, columns4, 0, rows4),
            columns4,
            rows4,
            allowHighPrecisionMv: frameHeader.AllowHighPrecisionMv,
            forceIntegerMv: frameHeader.ForceIntegerMv,
            filterSwitchable: frameHeader.InterpolationFilter == 4,
            dualFilter: sequenceHeader.EnableDualFilter,
            fixedFilter: frameHeader.InterpolationFilter == 4 ? 0 : frameHeader.InterpolationFilter,
            frameHeader.GlobalMotionParams,
            signBias,
            frameHeader.AllowWarpedMotion,
            Av1TemporalMvContext.Create(sequenceHeader, frameHeader, references),
            sequenceHeader.EnableMaskedCompound,
            sequenceHeader.EnableInterIntraCompound);

        // Whether each reference's global-motion model can be applied as a warp (dav1d
        // gmv_warp_allowed): a non-translation model whose shear parameters are within limits.
        for (int i = 0; i < 7; i++)
        {
            Av1WarpedMotionParams model = frameHeader.GlobalMotionParams[i];
            if (!frameHeader.ForceIntegerMv && model.Type > Av1WarpModelType.Translation)
            {
                short[] shear = new short[4];
                if (!Prediction.Av1WarpedMotion.TryGetShearParams(model.Matrix, shear))
                {
                    this.globalWarpShear[i] = shear;
                }
            }
        }
    }

    /// <summary>Gets the frame's 4x4 motion-vector grid (sampled at the frame end into the temporal field).</summary>
    public Av1MotionVectorGrid MotionVectorGrid => this.grid;

    private static Av1ReferenceFrame?[] CreateUniformReferences(Av1ReferenceFrame reference)
    {
        Av1ReferenceFrame?[] references = new Av1ReferenceFrame?[7];
        Array.Fill(references, reference);
        return references;
    }

    // Resolves a zero-based reference name (LAST .. ALTREF) to its frame, rejecting empty slots.
    private Av1ReferenceFrame GetReference(int reference)
        => this.references[reference] ?? throw new InvalidDataException($"Inter block references the empty reference slot {reference}.");

    private protected override void OnSuperblockRowStart()
    {
        Array.Fill(this.interLeftTx, (sbyte)4);
        this.interNeighbours.ClearLeft();
    }

    private protected override void BindCdfs(Av1FrameCdfSet cdfs)
    {
        base.BindCdfs(cdfs);
        this.interCdf = cdfs.InterMode;
        this.mvCdf = cdfs.MotionVector;
        this.filterCdf = cdfs.Filter;
        this.motionModeCdf = cdfs.MotionMode;
        this.interTransformTypeCdf = cdfs.InterTransformType;
        this.transformPartitionCdf = cdfs.TransformPartition;
    }

    private protected override void OnTileStart()
    {
        base.OnTileStart();
        int start = this.tileBounds.ColumnStart;
        int count = this.tileBounds.ColumnEnd - start;
        Array.Fill(this.interAboveTx, (sbyte)4, start, count);
        this.interNeighbours.ClearAbove(start, count);
        this.options = this.options.WithBounds(this.tileBounds);
    }

    private protected override void RecordLumaTransformType(int txCol, int txRow, int txWidth4, int txHeight4, Av1TransformType txType)
    {
        for (int my = 0; my < txHeight4 && txRow + my < this.miRows; my++)
        {
            int rowBase = (txRow + my) * this.miColumns;
            for (int mx = 0; mx < txWidth4 && txCol + mx < this.miColumns; mx++)
            {
                this.lumaTransformTypes[rowBase + txCol + mx] = txType;
            }
        }
    }

    // Infers an inter chroma transform block's type from the co-located luma type (dav1d's get_uv_inter_txtp).
    // The luma position is the block's own (untruncated) row/col plus the chroma transform's offset from
    // the block's chroma origin, scaled back to luma units: for a block at an odd row/column (the shared-
    // chroma half of a sub-8x8 partition) simply left-shifting the chroma-plane coordinate would recover
    // the wrong (rounded-down) luma row/column, since right-shift-then-left-shift loses the odd bit.
    private Av1TransformType InterChromaTransformType(int row, int col, int chromaRow, int chromaCol, Av1TransformSize chromaTx, int chromaTxCol, int chromaTxRow)
    {
        int lumaCol = col + ((chromaTxCol - chromaCol) << this.subsamplingX);
        int lumaRow = row + ((chromaTxRow - chromaRow) << this.subsamplingY);
        Av1TransformType lumaType = this.lumaTransformTypes[(lumaRow * this.miColumns) + lumaCol];
        return Av1ChromaTransformType.FromInter(chromaTx, lumaType);
    }

    private protected override void DecodeBlock(int row, int col, Av1BlockSize bsize, bool topRightAvailable)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();
        bool haveTop = row > this.tileBounds.RowStart;
        bool haveLeft = col > this.tileBounds.ColumnStart;

        // skip_mode: read before the skip flag; a skip-mode block is a residual-free compound block
        // with the frame's derived reference pair (no further mode syntax).
        bool skipModeBlock = false;
        if (this.frameHeader.SkipModeEnabled && Math.Min(width4, height4) > 1)
        {
            int skipModeContext = this.interNeighbours.AboveSkipMode(col) + this.interNeighbours.LeftSkipMode(row);
            skipModeBlock = this.decoder.ReadSymbol(this.interCdf.SkipMode[skipModeContext]) != 0;
        }

        int skip = this.ReadSkipFlag(row, col, width4, height4, skipModeBlock ? 1 : null);

        bool isInter = true;
        if (!skipModeBlock)
        {
            int intraContext = Av1IsInterReader.GetIntraContext(
                this.interNeighbours.LeftIntra(row), this.interNeighbours.AboveIntra(col), haveLeft, haveTop);
            isInter = Av1IsInterReader.ReadIsInter(this.decoder, this.interCdf, intraContext);
        }

        if (!isInter)
        {
            // An intra block inside an inter frame: the luma mode is read from the inter-frame y_mode
            // CDF (indexed only by a block-size group, no neighbour-mode context) rather than the
            // key-frame CDF; the rest of the intra body is shared with the key-frame path.
            int sizeGroup = Math.Min(3, System.Numerics.BitOperations.Log2((uint)Math.Min(width4, height4)));
            int yMode = this.decoder.ReadSymbol(this.modeCdf.YMode[sizeGroup]);
            this.DecodeIntraBlockBody(row, col, bsize, skip, yMode);
            return;
        }

        // Motion mode is coded for blocks at least 8x8 that have an overlappable (inter) neighbour.
        bool readMotionMode = this.frameHeader.IsMotionModeSwitchable
            && Math.Min(width4, height4) >= 2
            && this.HasOverlappableNeighbour(row, col, width4, height4, haveTop, haveLeft);

        // The sub-8x8 chroma prediction needs the left and top neighbours' interpolation filters; capture
        // them before the mode-info decode overwrites the neighbour contexts with this block's own values.
        Av1ReferenceNeighbour leftNeighbour = this.interNeighbours.GetLeft(row);
        Av1ReferenceNeighbour aboveNeighbour = this.interNeighbours.GetAbove(col);

        // Overlapped prediction reads the above/left neighbours' filters at odd offsets inside the
        // block's span; capture them too before the mode-info decode overwrites those contexts.
        (int F0, int F1)[]? obmcAboveFilters = null;
        (int F0, int F1)[]? obmcLeftFilters = null;
        if (readMotionMode)
        {
            int w4 = Math.Min(width4, this.miColumns - col);
            int h4 = Math.Min(height4, this.miRows - row);
            obmcAboveFilters = new (int, int)[w4];
            for (int x = 0; x < w4 && col + 1 + x < this.miColumns; x++)
            {
                Av1ReferenceNeighbour n = this.interNeighbours.GetAbove(col + 1 + x);
                obmcAboveFilters[x] = (n.Filter0, n.Filter1);
            }

            obmcLeftFilters = new (int, int)[h4];
            for (int y = 0; y < h4 && row + 1 + y < this.miRows; y++)
            {
                Av1ReferenceNeighbour n = this.interNeighbours.GetLeft(row + 1 + y);
                obmcLeftFilters[y] = (n.Filter0, n.Filter1);
            }
        }

        // Compound flag (dav1d: switchable_comp_refs && min(bw4,bh4) > 1); a skip-mode block is
        // compound by definition.
        bool isCompound = skipModeBlock;
        if (!skipModeBlock && this.frameHeader.ReferenceSelect && Math.Min(width4, height4) > 1)
        {
            int compoundContext = Av1ReferenceContext.ComputeCompoundContext(
                this.interNeighbours.GetAbove(col), this.interNeighbours.GetLeft(row), haveTop, haveLeft);
            isCompound = this.decoder.ReadSymbol(this.interCdf.Compound[compoundContext]) != 0;
        }

        Av1InterBlockInfo info;
        if (isCompound)
        {
            int[] skipModeReferences = this.frameHeader.SkipModeReferences ?? [0, 0];
            info = Av1InterModeInfoDecoder.DecodeCompound(
                this.decoder,
                this.interCdf,
                this.mvCdf,
                this.filterCdf,
                this.grid,
                this.interNeighbours,
                col,
                row,
                bsize,
                this.options,
                haveTop,
                haveLeft,
                topRightAvailable,
                skipModeBlock,
                skipModeReferences[0],
                skipModeReferences[1]);
        }
        else
        {
            info = Av1InterModeInfoDecoder.Decode(
                this.decoder,
                this.interCdf,
                this.mvCdf,
                this.filterCdf,
                this.motionModeCdf,
                this.grid,
                this.interNeighbours,
                col,
                row,
                bsize,
                this.options,
                haveTop,
                haveLeft,
                topRightAvailable,
                readMotionMode,
                skipMode: false);
        }

        // Motion-compensate every plane from the block's reference frame into the output planes. A
        // GLOBALMV block whose reference has a warpable (non-translational, shearable) global model,
        // or a WARP motion-mode block whose derived local model is affine, is predicted with the
        // affine warp kernel instead of translational MC (dav1d's warp_affine path); an OBMC block
        // blends overlapped predictions from its inter neighbours over the translational prediction.
                Av1ReferenceFrame blockReference = this.GetReference(info.Reference);
        if (info.IsCompound)
        {
            bool compoundHasChroma = this.sequenceHeader.NumPlanes > 1 &&
                                     (width4 > this.subsamplingX || (col & 1) != 0) &&
                                     (height4 > this.subsamplingY || (row & 1) != 0);
            this.CompoundPredict(row, col, width4, height4, info, bsize);
            this.DecodeInterResidual(row, col, bsize, skip, info, compoundHasChroma);
            return;
        }

        int[]? warpMatrix = null;
        short[]? warpShear = null;
        if (Math.Min(width4, height4) > 1)
        {
            if (info.Mode == Av1InterPredictionMode.GlobalMv && this.globalWarpShear[info.Reference] is { } globalShear)
            {
                warpMatrix = this.frameHeader.GlobalMotionParams[info.Reference].Matrix;
                warpShear = globalShear;
            }
            else if (info.MotionMode == Av1MotionMode.Warp && info.WarpShear is not null)
            {
                warpMatrix = info.WarpMatrix;
                warpShear = info.WarpShear;
            }
        }

        if (warpShear is not null)
        {
            Prediction.Av1WarpedMotion.WarpPlane(
                this.luma, blockReference.Luma, col, row, width4, height4, warpMatrix, warpShear, 0, 0);
        }
        else
        {
            this.MotionCompensate(this.luma, blockReference.Luma, row, col, width4, height4, info, 0, 0);
            if (info.MotionMode == Av1MotionMode.Obmc)
            {
                this.OverlappedPrediction(this.luma, 0, row, col, width4, height4, obmcAboveFilters!, obmcLeftFilters!);
            }
        }

        if (info.InterIntraType != 0)
        {
            this.InterIntraBlend(this.luma, 0, row, col, width4, height4, bsize, info);
        }
        bool hasChroma = this.sequenceHeader.NumPlanes > 1 &&
                         (width4 > this.subsamplingX || (col & 1) != 0) &&
                         (height4 > this.subsamplingY || (row & 1) != 0);
        if (hasChroma && blockReference.ChromaU is not null && blockReference.ChromaV is not null)
        {
            this.MotionCompensateChroma(row, col, width4, height4, info, leftNeighbour, aboveNeighbour, warpMatrix, warpShear, obmcAboveFilters, obmcLeftFilters);
            if (info.InterIntraType != 0)
            {
                this.InterIntraBlend(this.chromaU, 1, row, col, width4, height4, bsize, info);
                this.InterIntraBlend(this.chromaV, 2, row, col, width4, height4, bsize, info);
            }
        }

        this.DecodeInterResidual(row, col, bsize, skip, info, hasChroma);
    }

    // Adds the residual through the shared transform-block loop (substituting the motion-compensated
    // prediction via the Predict override; a skipped block carries no residual) and records the
    // neighbour contexts an inter block contributes.
    private void DecodeInterResidual(int row, int col, Av1BlockSize bsize, int skip, in Av1InterBlockInfo info, bool hasChroma)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();
        bool blockSkip = skip != 0;
        this.currentBlockIsInter = true;
        Av1TransformSize maxLumaTx = bsize.GetMaxTransformSize();
        bool variableTransform = this.frameHeader.TxMode == 2 && !blockSkip && maxLumaTx != Av1TransformSize.Size4x4;
        if (variableTransform)
        {
            this.DecodeLumaVariableTransform(row, col, bsize, maxLumaTx);
        }
        else
        {
            // The whole block uses a single transform; record the transform-size neighbour context so a
            // later block's variable-transform tree sees it.
            Fill(this.interAboveTx, col, width4, (sbyte)(maxLumaTx.GetWidthLog2() - 2));
            Fill(this.interLeftTx, row, height4, (sbyte)(maxLumaTx.GetHeightLog2() - 2));
            this.DecodePlane(this.luma, this.lumaLevels, 0, row, col, bsize, maxLumaTx, 0, 0, -1, 0, blockSkip, this.ReadInterLumaTransformType);
        }

        if (hasChroma)
        {
            Av1TransformSize chromaTx = bsize.GetMaxChromaTransformSize(this.sequenceHeader);
            int chromaRow = row >> this.subsamplingY;
            int chromaCol = col >> this.subsamplingX;
            Av1TransformType ChromaTxtp(Av1TransformSize t, int tc, int tr) => this.InterChromaTransformType(row, col, chromaRow, chromaCol, t, tc, tr);
            this.DecodePlane(this.chromaU, this.chromaULevels, 1, chromaRow, chromaCol, bsize, chromaTx, 0, 0, -1, 0, blockSkip, chromaTransformTypeProvider: ChromaTxtp);
            this.DecodePlane(this.chromaV, this.chromaVLevels, 2, chromaRow, chromaCol, bsize, chromaTx, 0, 0, -1, 0, blockSkip, chromaTransformTypeProvider: ChromaTxtp);
        }

        this.currentBlockIsInter = false;

        // Record the shared skip neighbour context for the next block's skip flag, and the intra-side
        // contexts (tx_intra with block-dimension categories, mode/uv-mode resets) an inter block
        // contributes for later intra blocks.
        Fill(this.aboveSkip, col, width4, (byte)skip);
        Fill(this.leftSkip, row, height4, (byte)skip);
        this.RecordInterBlockIntraContexts(row, col, bsize, hasChroma);
        bool isGlobal = info.IsCompound
            ? info.CompoundMode == Av1InterModeInfoDecoder.CompoundGlobalGlobal
            : info.Mode == Av1InterPredictionMode.GlobalMv;
        this.RecordLoopFilterLevels(row, col, bsize, hasChroma, info.Reference + 1, isGlobal ? 0 : 1);
        this.TopLeft4x4Filter = (info.Filter0, info.Filter1);
    }

    // Motion-compensates both references of a compound block into 16-bit intermediates and blends them
    // by plain averaging (dav1d's COMP_INTER_AVG path); a GLOBALMV_GLOBALMV component with a warpable
    // model uses the warp kernel into the intermediate instead.
    private void CompoundPredict(int row, int col, int width4, int height4, in Av1InterBlockInfo info, Av1BlockSize bsize)
    {
        bool hasChroma = this.sequenceHeader.NumPlanes > 1 &&
                         (width4 > this.subsamplingX || (col & 1) != 0) &&
                         (height4 > this.subsamplingY || (row & 1) != 0);

        // A segmented (difference-weighted) compound derives its blend mask from the luma
        // intermediates at the chroma resolution; the chroma planes then blend with that mask.
        byte[]? segMask = null;
        if (info.CompoundType == 3)
        {
            segMask = new byte[((width4 * 4) >> this.subsamplingX) * ((height4 * 4) >> this.subsamplingY)];
        }

        this.CompoundPredictPlane(0, row, col, width4, height4, info, segMask, bsize);
        if (hasChroma)
        {
            this.CompoundPredictPlane(1, row, col, width4, height4, info, segMask, bsize);
            this.CompoundPredictPlane(2, row, col, width4, height4, info, segMask, bsize);
        }
    }

    private void CompoundPredictPlane(int plane, int row, int col, int width4, int height4, in Av1InterBlockInfo info, byte[]? segMask, Av1BlockSize bsizeForWedge)
    {
        int ssX = plane == 0 ? 0 : this.subsamplingX;
        int ssY = plane == 0 ? 0 : this.subsamplingY;
        int width = (width4 * 4) >> ssX;
        int height = (height4 * 4) >> ssY;
        short[] intermediate0 = new short[width * height];
        short[] intermediate1 = new short[width * height];
        int cbw4 = (width4 + this.subsamplingX) >> this.subsamplingX;
        int cbh4 = (height4 + this.subsamplingY) >> this.subsamplingY;
        bool warpSized = plane == 0 || Math.Min(cbw4, cbh4) > 1;

        for (int i = 0; i < 2; i++)
        {
            int reference = i == 0 ? info.Reference : info.Reference1;
            Av1MotionVector motionVector = i == 0 ? info.MotionVector : info.MotionVector1;
            Av1ReferenceFrame referenceFrame = this.GetReference(reference);
            Av1Plane referencePlane = plane == 0 ? referenceFrame.Luma : plane == 1 ? referenceFrame.ChromaU! : referenceFrame.ChromaV!;
            short[] intermediate = i == 0 ? intermediate0 : intermediate1;

            if (info.CompoundMode == Av1InterModeInfoDecoder.CompoundGlobalGlobal
                && warpSized
                && this.globalWarpShear[reference] is { } shear)
            {
                Prediction.Av1WarpedMotion.WarpPlane16(
                    intermediate, width, referencePlane, col, row, width4, height4,
                    this.frameHeader.GlobalMotionParams[reference].Matrix, shear, ssX, ssY);
            }
            else
            {
                Av1InterPredictor.Prepare(
                    intermediate, referencePlane.Samples, referencePlane.CropWidth, referencePlane.CropHeight, referencePlane.Width,
                    col, row, width4, height4, motionVector, info.Filter0, info.Filter1, ssX, ssY);
            }
        }

        Av1Plane destination = plane == 0 ? this.luma : plane == 1 ? this.chromaU : this.chromaV;
        int dstX = (col >> ssX) * 4;
        int dstY = (row >> ssY) * 4;
        if (info.CompoundType is 3 or 4)
        {
            // Masked compound: the mask-signed intermediate leads the blend. A segmented block
            // derives the mask from the luma difference; a wedge block reads it from the tables.
            short[] lead = info.MaskSign ? intermediate1 : intermediate0;
            short[] trail = info.MaskSign ? intermediate0 : intermediate1;
            if (info.CompoundType == 3 && segMask is not null)
            {
                if (plane == 0)
                {
                    WMaskBlend(destination, dstX, dstY, lead, trail, width, height, segMask, info.MaskSign ? 1 : 0, this.subsamplingX, this.subsamplingY);
                }
                else
                {
                    MaskBlend(destination, dstX, dstY, lead, trail, width, height, segMask);
                }
            }
            else
            {
                int wedgeContext = Av1InterModeInfoDecoder.GetWedgeContext(bsizeForWedge);
                byte[] wedgeMask = plane == 0
                    ? Prediction.Av1WedgeMasks.Luma[wedgeContext][info.WedgeIndex]
                    : Prediction.Av1WedgeMasks.Chroma420[wedgeContext][info.MaskSign ? 1 : 0][info.WedgeIndex];
                MaskBlend(destination, dstX, dstY, lead, trail, width, height, wedgeMask);
            }

            return;
        }

        for (int y = 0; y < height; y++)
        {
            int dstBase = ((dstY + y) * destination.Width) + dstX;
            int srcBase = y * width;
            for (int x = 0; x < width; x++)
            {
                destination.Samples[dstBase + x] = (byte)Math.Clamp((intermediate0[srcBase + x] + intermediate1[srcBase + x] + 16) >> 5, 0, 255);
            }
        }
    }

    // dav1d w_mask_c (8-bit): blends the luma intermediates by a per-pixel weight derived from their
    // difference and stores the weight, subsampled to the chroma layout, for the chroma blend.
    private static void WMaskBlend(Av1Plane destination, int dstX, int dstY, short[] tmp1, short[] tmp2, int width, int height, byte[] mask, int sign, int ssHor, int ssVer)
    {
        int maskStride = width >> ssHor;
        int maskBase = 0;
        for (int y = 0; y < height; y++)
        {
            int remaining = height - y;
            int dstBase = ((dstY + y) * destination.Width) + dstX;
            int srcBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int diff = tmp1[srcBase + x] - tmp2[srcBase + x];
                int m = Math.Min(38 + ((Math.Abs(diff) + 8) >> 8), 64);
                destination.Samples[dstBase + x] = (byte)Math.Clamp(((diff * m) + (tmp2[srcBase + x] * 64) + 512) >> 10, 0, 255);

                if (ssHor != 0)
                {
                    x++;
                    int diff2 = tmp1[srcBase + x] - tmp2[srcBase + x];
                    int n = Math.Min(38 + ((Math.Abs(diff2) + 8) >> 8), 64);
                    destination.Samples[dstBase + x] = (byte)Math.Clamp(((diff2 * n) + (tmp2[srcBase + x] * 64) + 512) >> 10, 0, 255);

                    if ((remaining & ssVer) != 0)
                    {
                        mask[maskBase + (x >> 1)] = (byte)((m + n + mask[maskBase + (x >> 1)] + 2 - sign) >> 2);
                    }
                    else if (ssVer != 0)
                    {
                        mask[maskBase + (x >> 1)] = (byte)(m + n);
                    }
                    else
                    {
                        mask[maskBase + (x >> 1)] = (byte)((m + n + 1 - sign) >> 1);
                    }
                }
                else
                {
                    mask[maskBase + x] = (byte)m;
                }
            }

            if (ssVer == 0 || (remaining & 1) != 0)
            {
                maskBase += maskStride;
            }
        }
    }

    // dav1d mask_c (8-bit): blends two intermediates by an explicit per-pixel 64-weight mask.
    private static void MaskBlend(Av1Plane destination, int dstX, int dstY, short[] tmp1, short[] tmp2, int width, int height, byte[] mask)
    {
        for (int y = 0; y < height; y++)
        {
            int dstBase = ((dstY + y) * destination.Width) + dstX;
            int srcBase = y * width;
            int maskBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int m = mask[maskBase + x];
                destination.Samples[dstBase + x] = (byte)Math.Clamp(((tmp1[srcBase + x] * m) + (tmp2[srcBase + x] * (64 - m)) + 512) >> 10, 0, 255);
            }
        }
    }

    private protected override (int F0, int F1) TopLeft4x4Filter { get; set; }

    // Motion-compensates the chroma planes of an inter block. Blocks of at least 8x8 luma samples map
    // 1:1 onto a chroma block. A sub-8x8 block shares its chroma unit with its group neighbours: when
    // all covering neighbours are themselves inter, each 2x2 chroma quadrant is predicted with the
    // corresponding luma block's own motion vector and filter (dav1d's is_sub8x8 piecewise path, reading
    // the neighbours back from the motion-vector grid); if any of them is intra, the whole chroma unit
    // is instead predicted once, 8x8-aligned, with this block's motion vector.
    private void MotionCompensateChroma(int row, int col, int width4, int height4, in Av1InterBlockInfo info, in Av1ReferenceNeighbour leftNeighbour, in Av1ReferenceNeighbour aboveNeighbour, int[]? warpMatrix, short[]? warpShear, (int F0, int F1)[]? obmcAboveFilters, (int F0, int F1)[]? obmcLeftFilters)
    {
        bool subWidth = width4 == this.subsamplingX;
        bool subHeight = height4 == this.subsamplingY;
        bool isSub8x8 = subWidth || subHeight;
        if (subWidth)
        {
            isSub8x8 &= this.grid[row, col - 1].Reference0 > 0;
        }

        if (subHeight)
        {
            isSub8x8 &= this.grid[row - 1, col].Reference0 > 0;
        }

        if (subWidth && subHeight)
        {
            isSub8x8 &= this.grid[row - 1, col - 1].Reference0 > 0;
        }

        int baseX = (col >> this.subsamplingX) * 4;
        int baseY = (row >> this.subsamplingY) * 4;
        if (!isSub8x8)
        {
            // A chroma unit of more than one 4x4 cell per dimension follows the luma warp when the
            // block is warped (global or local); a smaller unit (or any non-warp block) is predicted
            // translationally with this block's motion vector, plus overlapped blending for OBMC.
            int cbw4 = (width4 + this.subsamplingX) >> this.subsamplingX;
            int cbh4 = (height4 + this.subsamplingY) >> this.subsamplingY;
            if (Math.Min(cbw4, cbh4) > 1 && warpShear is not null)
            {
                Av1ReferenceFrame referenceFrame = this.GetReference(info.Reference);
                Prediction.Av1WarpedMotion.WarpPlane(
                    this.chromaU, referenceFrame.ChromaU!, col, row, width4, height4, warpMatrix, warpShear, this.subsamplingX, this.subsamplingY);
                Prediction.Av1WarpedMotion.WarpPlane(
                    this.chromaV, referenceFrame.ChromaV!, col, row, width4, height4, warpMatrix, warpShear, this.subsamplingX, this.subsamplingY);
                return;
            }

            // Predict the whole (8x8-aligned) chroma unit with this block's motion vector.
            int alignedCol = col & ~this.subsamplingX;
            int alignedRow = row & ~this.subsamplingY;
            int mcWidth4 = subWidth ? width4 << 1 : width4;
            int mcHeight4 = subHeight ? height4 << 1 : height4;
            this.ChromaMcPiece(info.Reference, alignedCol, alignedRow, mcWidth4, mcHeight4, info.MotionVector, info.Filter0, info.Filter1, baseX, baseY);
            if (info.MotionMode == Av1MotionMode.Obmc)
            {
                this.OverlappedPrediction(this.chromaU, 1, row, col, width4, height4, obmcAboveFilters!, obmcLeftFilters!);
                this.OverlappedPrediction(this.chromaV, 2, row, col, width4, height4, obmcAboveFilters!, obmcLeftFilters!);
            }

            return;
        }

        int hOff = 0;
        int vOff = 0;
        if (subWidth && subHeight)
        {
            Av1RefMvsBlock topLeft = this.grid[row - 1, col - 1];
            (int f0, int f1) = this.TopLeft4x4Filter;
            this.ChromaMcPiece(topLeft.Reference0 - 1, col - 1, row - 1, width4, height4, topLeft.MotionVector0, f0, f1, baseX, baseY);
            hOff = 2;
            vOff = 2;
        }

        if (subWidth)
        {
            Av1RefMvsBlock left = this.grid[row, col - 1];
            this.ChromaMcPiece(left.Reference0 - 1, col - 1, row, width4, height4, left.MotionVector0, leftNeighbour.Filter0, leftNeighbour.Filter1, baseX, baseY + vOff);
            hOff = 2;
        }

        if (subHeight)
        {
            Av1RefMvsBlock top = this.grid[row - 1, col];
            this.ChromaMcPiece(top.Reference0 - 1, col, row - 1, width4, height4, top.MotionVector0, aboveNeighbour.Filter0, aboveNeighbour.Filter1, baseX + hOff, baseY);
            vOff = 2;
        }

        this.ChromaMcPiece(info.Reference, col, row, width4, height4, info.MotionVector, info.Filter0, info.Filter1, baseX + hOff, baseY + vOff);
    }

    // Motion-compensates one chroma piece of both planes from the piece's own reference frame: the
    // source position derives from the piece's luma 4x4 position and motion vector, the destination is
    // an explicit chroma-plane position (for sub-8x8 pieces the quadrant inside the shared chroma unit).
    private void ChromaMcPiece(int reference, int pieceCol, int pieceRow, int width4, int height4, Av1MotionVector motionVector, int filter0, int filter1, int dstX, int dstY)
    {
        Av1ReferenceFrame referenceFrame = this.GetReference(reference);
        this.ChromaMcPlane(this.chromaU, referenceFrame.ChromaU!, pieceCol, pieceRow, width4, height4, motionVector, filter0, filter1, dstX, dstY);
        this.ChromaMcPlane(this.chromaV, referenceFrame.ChromaV!, pieceCol, pieceRow, width4, height4, motionVector, filter0, filter1, dstX, dstY);
    }

    // dav1d obmc_masks: the overlapped-prediction blend weights, indexed by the blend dimension.
    private static readonly byte[] ObmcMasks =
    [
        0, 0,
        19, 0,
        25, 14, 5, 0,
        28, 22, 16, 11, 7, 3, 0, 0,
        30, 27, 24, 21, 18, 15, 12, 10, 8, 6, 4, 3, 0, 0, 0, 0,
        31, 29, 28, 26, 24, 23, 21, 20, 19, 17, 16, 14, 13, 12, 11, 9,
        8, 7, 6, 5, 4, 4, 3, 2, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    // Scratch buffer for the overlapped neighbour predictions (dav1d t->scratch.lap).
    private readonly byte[] obmcLap = new byte[64 * 32];

    // Overlapped block motion compensation for one plane (dav1d's obmc): the top quarter-to-half of
    // the block is re-predicted from up to four above neighbours' motion vectors and blended in, then
    // the left part from up to four left neighbours. Only inter neighbours at odd 4x4 offsets
    // contribute; chroma planes participate only for blocks whose summed chroma dimensions reach 16.
    private void OverlappedPrediction(Av1Plane destination, int plane, int row, int col, int bw4, int bh4, (int F0, int F1)[] aboveFilters, (int F0, int F1)[] leftFilters)
    {
        int ssX = plane == 0 ? 0 : this.subsamplingX;
        int ssY = plane == 0 ? 0 : this.subsamplingY;
        int hMul = 4 >> ssX;
        int vMul = 4 >> ssY;
        int w4 = Math.Min(bw4, this.miColumns - col);
        int h4 = Math.Min(bh4, this.miRows - row);
        byte[] lap = this.obmcLap;
        int dstBase = ((row >> ssY) * 4 * destination.Width) + ((col >> ssX) * 4);

        // The chroma minimum-size condition gates only the above pass (dav1d obmc); the left pass
        // runs for every plane.
        if (row > this.tileBounds.RowStart && (plane == 0 || (bw4 * hMul) + (bh4 * vMul) >= 16))
        {
            int maxNeighbours = Math.Min(System.Numerics.BitOperations.Log2((uint)bw4), 4);
            for (int i = 0, x = 0; x < w4 && i < maxNeighbours;)
            {
                if (col + x + 1 >= this.miColumns)
                {
                    break;
                }

                Av1RefMvsBlock aboveBlock = this.grid[row - 1, col + x + 1];
                int step4 = Math.Clamp(aboveBlock.BlockSize.GetWidth4(), 2, 16);
                if (aboveBlock.Reference0 > 0)
                {
                    int ow4 = Math.Min(step4, bw4);
                    int oh4 = Math.Min(bh4, 16) >> 1;
                    (int f0, int f1) = aboveFilters[x];
                    Av1Plane referencePlane = this.GetReferencePlane(aboveBlock.Reference0 - 1, plane);
                    Av1InterPredictor.Predict(
                        lap, 0, ow4 * hMul, referencePlane.Samples, referencePlane.CropWidth, referencePlane.CropHeight, referencePlane.Width,
                        col + x, row, ow4, ((oh4 * 3) + 3) >> 2, aboveBlock.MotionVector0, f0, f1, ssX, ssY);
                    BlendFromAbove(destination, dstBase + (x * hMul), lap, ow4 * hMul, oh4 * vMul);
                    i++;
                }

                x += step4;
            }
        }

        if (col > this.tileBounds.ColumnStart)
        {
            int maxNeighbours = Math.Min(System.Numerics.BitOperations.Log2((uint)bh4), 4);
            for (int i = 0, y = 0; y < h4 && i < maxNeighbours;)
            {
                if (row + y + 1 >= this.miRows)
                {
                    break;
                }

                Av1RefMvsBlock leftBlock = this.grid[row + y + 1, col - 1];
                int step4 = Math.Clamp(leftBlock.BlockSize.GetHeight4(), 2, 16);
                if (leftBlock.Reference0 > 0)
                {
                    int ow4 = Math.Min(bw4, 16) >> 1;
                    int oh4 = Math.Min(step4, bh4);
                    (int f0, int f1) = leftFilters[y];
                    Av1Plane referencePlane = this.GetReferencePlane(leftBlock.Reference0 - 1, plane);
                    Av1InterPredictor.Predict(
                        lap, 0, ow4 * hMul, referencePlane.Samples, referencePlane.CropWidth, referencePlane.CropHeight, referencePlane.Width,
                        col, row + y, ow4, oh4, leftBlock.MotionVector0, f0, f1, ssX, ssY);
                    BlendFromLeft(destination, dstBase + (y * vMul * destination.Width), lap, ow4 * hMul, oh4 * vMul);
                    i++;
                }

                y += step4;
            }
        }
    }

    // Blends an intra prediction of the whole block over its motion-compensated samples (dav1d's
    // interintra recon): DC/V/H/SMOOTH prediction from the neighbouring reconstruction, weighted by
    // the mode-faded ii mask (flat 32 for DC), or the sign-0 wedge mask for the wedge type.
    private void InterIntraBlend(Av1Plane plane, int planeIndex, int row, int col, int width4, int height4, Av1BlockSize bsize, in Av1InterBlockInfo info)
    {
        int ssX = planeIndex == 0 ? 0 : this.subsamplingX;
        int ssY = planeIndex == 0 ? 0 : this.subsamplingY;
        int width = (width4 * 4) >> ssX;
        int height = (height4 * 4) >> ssY;
        int x = (col >> ssX) * 4;
        int y = (row >> ssY) * 4;

        int intraMode = info.InterIntraMode == 3 ? 9 : info.InterIntraMode;
        byte[] intra = new byte[width * height];
        this.Predict(plane, x, y, width, height, intraMode, 0, -1, 0, intra);

        byte[]? mask;
        int maskStride;
        if (info.InterIntraType == 2)
        {
            int wedgeContext = Av1InterModeInfoDecoder.GetWedgeContext(bsize);
            mask = planeIndex == 0
                ? Prediction.Av1WedgeMasks.Luma[wedgeContext][info.WedgeIndex]
                : Prediction.Av1WedgeMasks.Chroma420[wedgeContext][0][info.WedgeIndex];
            maskStride = width;
        }
        else if (Prediction.Av1InterIntraMasks.Get(width4, height4, info.InterIntraMode, planeIndex != 0) is { } faded)
        {
            mask = faded.Mask;
            maskStride = faded.Stride;
        }
        else
        {
            mask = null;
            maskStride = 0;
        }

        for (int dy = 0; dy < height; dy++)
        {
            int dstBase = ((y + dy) * plane.Width) + x;
            int srcBase = dy * width;
            int maskBase = dy * maskStride;
            for (int dx = 0; dx < width; dx++)
            {
                int m = mask is null ? 32 : mask[maskBase + dx];
                int value = plane.Samples[dstBase + dx];
                plane.Samples[dstBase + dx] = (byte)(((value * (64 - m)) + (intra[srcBase + dx] * m) + 32) >> 6);
            }
        }
    }

    // Resolves one plane of a zero-based reference frame.
    private Av1Plane GetReferencePlane(int reference, int plane)
    {
        Av1ReferenceFrame frame = this.GetReference(reference);
        return plane == 0 ? frame.Luma : plane == 1 ? frame.ChromaU! : frame.ChromaV!;
    }

    // dav1d blend_h: blends the neighbour prediction over the top (3/4 of height) rows, the weight
    // decreasing with the distance from the shared edge.
    private static void BlendFromAbove(Av1Plane destination, int offset, byte[] overlap, int width, int height)
    {
        byte[] samples = destination.Samples;
        int rows = (height * 3) >> 2;
        for (int y = 0; y < rows; y++)
        {
            int m = ObmcMasks[height + y];
            int rowOffset = offset + (y * destination.Width);
            for (int x = 0; x < width; x++)
            {
                samples[rowOffset + x] = (byte)(((samples[rowOffset + x] * (64 - m)) + (overlap[(y * width) + x] * m) + 32) >> 6);
            }
        }
    }

    // dav1d blend_v: blends the neighbour prediction over the left (3/4 of width) columns.
    private static void BlendFromLeft(Av1Plane destination, int offset, byte[] overlap, int width, int height)
    {
        byte[] samples = destination.Samples;
        int columns = (width * 3) >> 2;
        for (int y = 0; y < height; y++)
        {
            int rowOffset = offset + (y * destination.Width);
            for (int x = 0; x < columns; x++)
            {
                int m = ObmcMasks[width + x];
                samples[rowOffset + x] = (byte)(((samples[rowOffset + x] * (64 - m)) + (overlap[(y * width) + x] * m) + 32) >> 6);
            }
        }
    }

    private void ChromaMcPlane(Av1Plane destination, Av1Plane reference, int pieceCol, int pieceRow, int width4, int height4, Av1MotionVector motionVector, int filter0, int filter1, int dstX, int dstY)
        => Av1InterPredictor.Predict(
            destination.Samples,
            (dstY * destination.Width) + dstX,
            destination.Width,
            reference.Samples,
            reference.CropWidth,
            reference.CropHeight,
            reference.Width,
            pieceCol,
            pieceRow,
            width4,
            height4,
            motionVector,
            filter0,
            filter1,
            this.subsamplingX,
            this.subsamplingY);

    private protected override void OnIntraBlockDecoded(int row, int col, Av1BlockSize bsize, int skip, int yMode, Av1TransformSize lumaTx)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();

        // An intra block contributes no motion vector or reference; record it as intra in every
        // inter-specific neighbour context a later inter block reads (dav1d's splat_intraref plus the
        // shared set_ctx: comp_type=none, ref=-1, filter=unset), and in the inter var-tx size context.
        const int filterUnset = 3;
        this.interNeighbours.Write(row, col, width4, height4, isIntra: true, reference0: -1, reference1: -1, isCompound: false, filterUnset, filterUnset, skipMode: false);
        Av1RefMvsBlock gridBlock = new(default, default, 0, -1, bsize, isNewMv: false, isGlobalMv: false, isIntra: true);
        this.grid.Fill(row, col, width4, height4, gridBlock);
        Fill(this.interAboveTx, col, width4, (sbyte)(lumaTx.GetWidthLog2() - 2));
        Fill(this.interLeftTx, row, height4, (sbyte)(lumaTx.GetHeightLog2() - 2));
    }

    private protected override void Predict(Av1Plane plane, int x, int y, int width, int height, int intraMode, int angleDelta, int filterIntraMode, int cflAlpha, byte[] prediction)
    {
        if (!this.currentBlockIsInter)
        {
            base.Predict(plane, x, y, width, height, intraMode, angleDelta, filterIntraMode, cflAlpha, prediction);
            return;
        }

        // The block is already motion-compensated into the plane; copy those samples as the prediction.
        for (int ry = 0; ry < height; ry++)
        {
            for (int rx = 0; rx < width; rx++)
            {
                byte sample = (x + rx < plane.Width && y + ry < plane.Height) ? plane[x + rx, y + ry] : (byte)0;
                prediction[(ry * width) + rx] = sample;
            }
        }
    }

    // Whether the block has an overlappable (inter) neighbour on its top or left edge, which gates the
    // motion-mode syntax (dav1d's findoddzero over the neighbour intra flags at odd offsets).
    private bool HasOverlappableNeighbour(int row, int col, int width4, int height4, bool haveTop, bool haveLeft)
    {
        if (haveLeft)
        {
            int count = Math.Min(height4, 16) >> 1;
            for (int i = 0; i < count; i++)
            {
                int r = row + 1 + (i * 2);
                if (r < this.miRows && this.interNeighbours.LeftIntra(r) == 0)
                {
                    return true;
                }
            }
        }

        if (haveTop)
        {
            int count = Math.Min(width4, 16) >> 1;
            for (int i = 0; i < count; i++)
            {
                int c = col + 1 + (i * 2);
                if (c < this.miColumns && this.interNeighbours.AboveIntra(c) == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Reads the variable-transform split tree for the block, then reconstructs the luma residual by
    // walking the same tree and decoding each variable-sized transform leaf (a port of read_vartx_tree
    // followed by read_coef_tree). The tree read updates the transform-size neighbour arrays at the leaves.
    private void DecodeLumaVariableTransform(int row, int col, Av1BlockSize bsize, Av1TransformSize maxLumaTx)
    {
        int bw4 = bsize.GetWidth4();
        int bh4 = bsize.GetHeight4();
        int ytxW4 = maxLumaTx.GetWidth() >> 2;
        int ytxH4 = maxLumaTx.GetHeight() >> 2;
        ushort[] masks = new ushort[3];

        int yOff = 0;
        for (int y = 0; y < bh4; y += ytxH4, yOff++)
        {
            int xOff = 0;
            for (int x = 0; x < bw4; x += ytxW4, xOff++)
            {
                Av1TransformTreeReader.Read(
                    this.decoder, this.transformPartitionCdf, maxLumaTx, 0, xOff, yOff, masks,
                    this.interAboveTx, this.interLeftTx, col + x, row + y, this.miColumns, this.miRows);
            }
        }

        yOff = 0;
        for (int y = 0; y < bh4; y += ytxH4, yOff++)
        {
            int xOff = 0;
            for (int x = 0; x < bw4; x += ytxW4, xOff++)
            {
                this.ReconstructLumaTree(maxLumaTx, 0, masks, xOff, yOff, col + x, row + y, bsize);
            }
        }
    }

    private void ReconstructLumaTree(Av1TransformSize tx, int depth, ushort[] masks, int xOffset, int yOffset, int txCol, int txRow, Av1BlockSize bsize)
    {
        int txWidth4 = tx.GetWidth() >> 2;
        int txHeight4 = tx.GetHeight() >> 2;
        bool split = depth < 2 && (masks[depth] & (1 << ((yOffset * 4) + xOffset))) != 0;
        if (split)
        {
            Av1TransformSize sub = tx.GetSubSize();
            int subWidth4 = sub.GetWidth() >> 2;
            int subHeight4 = sub.GetHeight() >> 2;

            this.ReconstructLumaTree(sub, depth + 1, masks, (xOffset * 2) + 0, (yOffset * 2) + 0, txCol, txRow, bsize);
            if (txWidth4 >= txHeight4 && txCol + subWidth4 < this.miColumns)
            {
                this.ReconstructLumaTree(sub, depth + 1, masks, (xOffset * 2) + 1, (yOffset * 2) + 0, txCol + subWidth4, txRow, bsize);
            }

            if (txHeight4 >= txWidth4 && txRow + subHeight4 < this.miRows)
            {
                this.ReconstructLumaTree(sub, depth + 1, masks, (xOffset * 2) + 0, (yOffset * 2) + 1, txCol, txRow + subHeight4, bsize);
                if (txWidth4 >= txHeight4 && txCol + subWidth4 < this.miColumns)
                {
                    this.ReconstructLumaTree(sub, depth + 1, masks, (xOffset * 2) + 1, (yOffset * 2) + 1, txCol + subWidth4, txRow + subHeight4, bsize);
                }
            }
        }
        else
        {
            bool blockEqualsTx = txWidth4 == bsize.GetWidth4() && txHeight4 == bsize.GetHeight4();
            this.DecodeTransformBlock(this.luma, this.lumaLevels, 0, txCol, txRow, bsize, tx, blockEqualsTx, 0, 0, -1, 0, false, this.ReadInterLumaTransformType);
        }
    }


    // Reads the luma transform type of an inter transform block. A 64x64 transform or a lossless
    // (zero-quantizer) block implies DCT_DCT and codes no transform type.
    private Av1TransformType ReadInterLumaTransformType(Av1TransformSize transformSize)
    {
        int maxCategory = Math.Max(transformSize.GetWidthLog2() - 2, transformSize.GetHeightLog2() - 2);
        if (maxCategory >= 4 || this.frameHeader.BaseQIndex == 0)
        {
            return Av1TransformType.DctDct;
        }

        return Av1InterTransformTypeReader.Read(this.decoder, this.interTransformTypeCdf, transformSize, this.frameHeader.ReducedTxSet);
    }

    private void MotionCompensate(Av1Plane destination, Av1Plane reference, int row, int col, int width4, int height4, in Av1InterBlockInfo info, int subsamplingX, int subsamplingY)
    {
        int x = (col >> subsamplingX) * 4;
        int y = (row >> subsamplingY) * 4;
        int destinationOffset = (y * destination.Width) + x;
        Av1InterPredictor.Predict(
            destination.Samples,
            destinationOffset,
            destination.Width,
            reference.Samples,
            reference.CropWidth,
            reference.CropHeight,
            reference.Width,
            col,
            row,
            width4,
            height4,
            info.MotionVector,
            info.Filter0,
            info.Filter1,
            subsamplingX,
            subsamplingY);
    }
}
