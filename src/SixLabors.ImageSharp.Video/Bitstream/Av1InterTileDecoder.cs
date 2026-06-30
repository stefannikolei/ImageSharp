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
/// supports both the uniform transform mode and the <c>TX_MODE_SELECT</c> variable-transform tree. The
/// implemented subset is single-reference, error-resilient frames (default CDFs, no temporal motion
/// vectors) with translation-only motion; compound prediction, warped/overlapped motion and intra blocks
/// inside inter frames raise <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class Av1InterTileDecoder : Av1TileDecoder
{
    private readonly Av1Plane referenceLuma;
    private readonly Av1Plane? referenceChromaU;
    private readonly Av1Plane? referenceChromaV;

    private readonly Av1InterModeCdfContext interCdf = Av1InterModeCdfContext.CreateDefault();
    private readonly Av1MotionVectorCdfContext mvCdf = Av1MotionVectorCdfContext.CreateDefault();
    private readonly Av1InterpolationFilterCdfContext filterCdf = Av1InterpolationFilterCdfContext.CreateDefault();
    private readonly Av1MotionModeCdfContext motionModeCdf = Av1MotionModeCdfContext.CreateDefault();
    private readonly Av1InterTransformTypeCdfContext interTransformTypeCdf = Av1InterTransformTypeCdfContext.CreateDefault();
    private readonly ushort[][][] transformPartitionCdf = CloneTransformPartitionCdf();

    // The inter variable-transform size neighbour context (dav1d's BlockContext.tx, distinct from the
    // intra tx_intra context in the base class). It stores the transform width/height category (log2 of
    // the size in 4x4 units) and is reset to TX_64X64 (category 4) at the frame top and each superblock row.
    private readonly sbyte[] interAboveTx;
    private readonly sbyte[] interLeftTx;
    private readonly Av1MotionVectorGrid grid;
    private readonly Av1InterNeighbourContext interNeighbours;
    private readonly Av1InterModeInfoOptions options;
    private readonly int interpolationFilter;

    // The current block's prediction source: set true around an inter block so the Predict override
    // substitutes the motion-compensated samples instead of an intra prediction.
    private bool currentBlockIsInter;

    /// <summary>Initializes a new instance of the <see cref="Av1InterTileDecoder"/> class.</summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The inter frame header.</param>
    /// <param name="reference">The single reference frame (used for every reference slot in this subset).</param>
    public Av1InterTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1ReferenceFrame reference)
        : base(sequenceHeader, frameHeader)
    {
        if (frameHeader.PrimaryRefFrame != 7)
        {
            throw new NotSupportedException("Inter frames with a primary reference frame are not supported yet.");
        }

        if (frameHeader.AllowWarpedMotion)
        {
            throw new NotSupportedException("Warped motion is not supported yet.");
        }

        if (frameHeader.ReferenceSelect)
        {
            throw new NotSupportedException("Compound (two-reference) prediction is not supported yet.");
        }

        this.referenceLuma = reference.Luma;
        this.referenceChromaU = reference.ChromaU;
        this.referenceChromaV = reference.ChromaV;
        this.interpolationFilter = frameHeader.InterpolationFilter;

        int columns4 = frameHeader.ModeInfoColumns;
        int rows4 = frameHeader.ModeInfoRows;
        this.interAboveTx = new sbyte[columns4];
        this.interLeftTx = new sbyte[rows4];
        Array.Fill(this.interAboveTx, (sbyte)4);
        Array.Fill(this.interLeftTx, (sbyte)4);
        this.grid = new Av1MotionVectorGrid(columns4, rows4);
        this.interNeighbours = new Av1InterNeighbourContext(columns4, rows4);
        this.options = new Av1InterModeInfoOptions(
            new Av1TileBounds(0, columns4, 0, rows4),
            columns4,
            rows4,
            allowHighPrecisionMv: frameHeader.AllowHighPrecisionMv,
            forceIntegerMv: false,
            filterSwitchable: frameHeader.InterpolationFilter == 4,
            dualFilter: sequenceHeader.EnableDualFilter,
            fixedFilter: frameHeader.InterpolationFilter == 4 ? 0 : frameHeader.InterpolationFilter,
            globalMv: default,
            globalMvSubstitution: false,
            globalMvIsTranslation: false,
            signBias: new int[7]);
    }

    private protected override void OnSuperblockRowStart() => Array.Fill(this.interLeftTx, (sbyte)4);

    private protected override void DecodeBlock(int row, int col, Av1BlockSize bsize)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();
        bool haveTop = row > 0;
        bool haveLeft = col > 0;

        int skip = this.ReadSkipFlag(row, col, width4, height4);

        int intraContext = Av1IsInterReader.GetIntraContext(
            this.interNeighbours.LeftIntra(row), this.interNeighbours.AboveIntra(col), haveLeft, haveTop);
        bool isInter = Av1IsInterReader.ReadIsInter(this.decoder, this.interCdf, intraContext);
        if (!isInter)
        {
            throw new NotSupportedException("Intra blocks inside inter frames are not supported yet.");
        }

        // Motion mode is coded for blocks at least 8x8 that have an overlappable (inter) neighbour. Warped
        // motion is rejected at construction, so only the binary OBMC flag is read (allowWarp is false).
        bool readMotionMode = this.frameHeader.IsMotionModeSwitchable
            && Math.Min(width4, height4) >= 2
            && this.HasOverlappableNeighbour(row, col, width4, height4, haveTop, haveLeft);

        Av1InterBlockInfo info = Av1InterModeInfoDecoder.Decode(
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
            topRightAvailable: false,
            readMotionMode,
            allowWarp: false,
            skipMode: false);

        if (info.MotionMode != Av1MotionMode.Translation)
        {
            throw new NotSupportedException("OBMC and warped motion compensation are not supported yet.");
        }

        // Motion-compensate every plane from the reference frame into the output planes.
        this.MotionCompensate(this.luma, this.referenceLuma, row, col, width4, height4, info, 0, 0);
        bool hasChroma = this.sequenceHeader.NumPlanes > 1 &&
                         (width4 > this.subsamplingX || (col & 1) != 0) &&
                         (height4 > this.subsamplingY || (row & 1) != 0);
        if (hasChroma && this.referenceChromaU is not null && this.referenceChromaV is not null)
        {
            this.MotionCompensate(this.chromaU, this.referenceChromaU, row, col, width4, height4, info, this.subsamplingX, this.subsamplingY);
            this.MotionCompensate(this.chromaV, this.referenceChromaV, row, col, width4, height4, info, this.subsamplingX, this.subsamplingY);
        }

        // Add the residual through the shared transform-block loop, substituting the motion-compensated
        // prediction via the Predict override. A skipped block carries no residual.
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
            this.DecodePlane(this.chromaU, this.chromaULevels, 1, chromaRow, chromaCol, bsize, chromaTx, 0, 0, -1, 0, blockSkip);
            this.DecodePlane(this.chromaV, this.chromaVLevels, 2, chromaRow, chromaCol, bsize, chromaTx, 0, 0, -1, 0, blockSkip);
        }

        this.currentBlockIsInter = false;

        // Record the shared skip neighbour context for the next block's skip flag.
        Fill(this.aboveSkip, col, width4, (byte)skip);
        Fill(this.leftSkip, row, height4, (byte)skip);
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

    private static ushort[][][] CloneTransformPartitionCdf()
    {
        ushort[][][] source = Av1DefaultTransformPartitionCdf.Split;
        ushort[][][] result = new ushort[source.Length][][];
        for (int i = 0; i < source.Length; i++)
        {
            result[i] = new ushort[source[i].Length][];
            for (int j = 0; j < source[i].Length; j++)
            {
                result[i][j] = (ushort[])source[i][j].Clone();
            }
        }

        return result;
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
            reference.Width,
            reference.Height,
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
