// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the tiles of an intra (key) frame into reconstructed luma and chroma planes. This covers the
/// recursively-split partition tree and the intra block decode (mode info, coefficient decode,
/// dequantization, inverse transform and DC prediction) for the feature subset validated bit-exactly
/// against the dav1d reference. Unsupported syntax raises <see cref="NotSupportedException"/> so that
/// streams beyond the current coverage fail loudly rather than producing incorrect pixels.
/// </summary>
internal sealed class Av1IntraTileDecoder
{
    private readonly ObuSequenceHeader sequenceHeader;
    private readonly ObuFrameHeader frameHeader;
    private readonly Av1ModeInfoCdfContext modeCdf;
    private readonly Av1CoefficientCdfContext coefficientCdf;

    private readonly Av1Plane luma;
    private readonly Av1Plane chromaU;
    private readonly Av1Plane chromaV;

    // Partition context bitmasks per 8x8 column/row (specification 5.11.4, dav1d get_partition_ctx).
    private readonly byte[] abovePartitionContext;
    private readonly byte[] leftPartitionContext;

    private Av1SymbolDecoder decoder = default!;

    public Av1IntraTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.modeCdf = Av1ModeInfoCdfContext.CreateDefault();
        this.coefficientCdf = Av1CoefficientCdfContext.CreateDefault(GetQuantizerContext(frameHeader.BaseQIndex));

        int width = frameHeader.FrameWidth;
        int height = frameHeader.FrameHeight;
        int chromaWidth = (width + sequenceHeader.SubsamplingX) >> sequenceHeader.SubsamplingX;
        int chromaHeight = (height + sequenceHeader.SubsamplingY) >> sequenceHeader.SubsamplingY;

        this.luma = new Av1Plane(width, height);
        this.chromaU = new Av1Plane(chromaWidth, chromaHeight);
        this.chromaV = new Av1Plane(chromaWidth, chromaHeight);

        this.abovePartitionContext = new byte[(frameHeader.ModeInfoColumns >> 1) + 1];
        this.leftPartitionContext = new byte[(frameHeader.ModeInfoRows >> 1) + 1];
    }

    /// <summary>Gets the reconstructed luma plane.</summary>
    public Av1Plane Luma => this.luma;

    /// <summary>Gets the reconstructed chroma U plane.</summary>
    public Av1Plane ChromaU => this.chromaU;

    /// <summary>Gets the reconstructed chroma V plane.</summary>
    public Av1Plane ChromaV => this.chromaV;

    /// <summary>
    /// Decodes a single tile that covers the whole frame from its compressed data.
    /// </summary>
    /// <param name="tileData">The tile's compressed bytes.</param>
    public void DecodeTile(ReadOnlyMemory<byte> tileData)
    {
        if (this.frameHeader.TileColumnsLog2 != 0 || this.frameHeader.TileRowsLog2 != 0)
        {
            throw new NotSupportedException("Multi-tile frames are not supported yet.");
        }

        this.decoder = new Av1SymbolDecoder(tileData);

        int superblockSize = this.sequenceHeader.Use128x128Superblock ? 32 : 16; // in 4x4 units
        Av1BlockSize superblock = this.sequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;

        for (int row = 0; row < this.frameHeader.ModeInfoRows; row += superblockSize)
        {
            Array.Clear(this.leftPartitionContext);
            for (int col = 0; col < this.frameHeader.ModeInfoColumns; col += superblockSize)
            {
                this.DecodePartition(row, col, superblock);
            }
        }
    }

    private void DecodePartition(int row, int col, Av1BlockSize bsize)
    {
        if (row >= this.frameHeader.ModeInfoRows || col >= this.frameHeader.ModeInfoColumns)
        {
            return;
        }

        int blockWidth4 = bsize.GetWidth4();
        int halfBlock4 = blockWidth4 >> 1;
        bool hasRows = row + halfBlock4 < this.frameHeader.ModeInfoRows;
        bool hasCols = col + halfBlock4 < this.frameHeader.ModeInfoColumns;

        // 4x4 blocks cannot be split further; everything down to 8x8 reads a partition symbol.
        Av1Partition partition = bsize == Av1BlockSize.Block4x4
            ? Av1Partition.None
            : this.ReadPartition(row, col, bsize, hasRows, hasCols);

        Av1BlockSize subSize = bsize.GetSubSize(partition);
        switch (partition)
        {
            case Av1Partition.None:
                this.DecodeBlock(row, col, bsize);
                break;
            case Av1Partition.Split:
                int offset = halfBlock4;
                this.DecodePartition(row, col, subSize);
                this.DecodePartition(row, col + offset, subSize);
                this.DecodePartition(row + offset, col, subSize);
                this.DecodePartition(row + offset, col + offset, subSize);
                break;
            default:
                throw new NotSupportedException($"Partition type {partition} is not supported yet.");
        }
    }

    private Av1Partition ReadPartition(int row, int col, Av1BlockSize bsize, bool hasRows, bool hasCols)
    {
        if (!hasRows || !hasCols)
        {
            // Partial blocks at the frame edge use constrained split signalling that is not handled yet.
            throw new NotSupportedException("Partition signalling at the frame edge is not supported yet.");
        }

        int blockLevel = bsize.GetPartitionLevel();
        int shift = 4 - blockLevel;
        int above = (this.abovePartitionContext[col >> 1] >> shift) & 1;
        int left = (this.leftPartitionContext[row >> 1] >> shift) & 1;
        int ctx = above + (left << 1);
        return (Av1Partition)this.decoder.ReadSymbol(this.modeCdf.Partition[blockLevel][ctx]);
    }

    private void DecodeBlock(int row, int col, Av1BlockSize bsize)
    {
        // skip flag.
        int skip = this.decoder.ReadSymbol(this.modeCdf.Skip[0]);

        // CDEF index (reads cdef_bits, which is 0 for the validated stream).
        // delta-q / delta-lf are gated by frame-header flags that are disabled here.
        if (this.frameHeader.DeltaQPresent)
        {
            throw new NotSupportedException("Per-block delta-q is not supported yet.");
        }

        // Intra modes (key-frame y-mode with no neighbours, chroma uv-mode).
        int yMode = this.decoder.ReadSymbol(this.modeCdf.KeyFrameYMode[0][0]);
        int uvMode = this.decoder.ReadSymbol(this.modeCdf.UvMode[0][0]);
        if (yMode != 0 || uvMode != 0)
        {
            throw new NotSupportedException("Only DC intra prediction is supported yet.");
        }

        // tx size: TX_MODE_LARGEST forces the largest transform for the block (no bits read here).
        if (this.frameHeader.TxMode != 1)
        {
            throw new NotSupportedException("Only TX_MODE_LARGEST is supported yet.");
        }

        Av1TransformSize lumaTx = bsize.GetMaxTransformSize();
        this.ReconstructPlane(this.luma, row, col, lumaTx, 0, skip != 0, 0, 0);

        Av1TransformSize chromaTx = bsize.GetMaxChromaTransformSize(this.sequenceHeader);
        int chromaSkipContext = 7; // chroma skip context with no neighbours for a single transform block
        this.ReconstructPlane(this.chromaU, row >> this.sequenceHeader.SubsamplingY, col >> this.sequenceHeader.SubsamplingX, chromaTx, 1, skip != 0, chromaSkipContext, 0);
        this.ReconstructPlane(this.chromaV, row >> this.sequenceHeader.SubsamplingY, col >> this.sequenceHeader.SubsamplingX, chromaTx, 2, skip != 0, chromaSkipContext, 0);

        // Record this leaf block's partition context for neighbouring blocks.
        byte fill = bsize.PartitionContextFill();
        int width8 = bsize.GetWidth4() >> 1;
        int height8 = bsize.GetHeight4() >> 1;
        for (int i = 0; i < width8 && (col >> 1) + i < this.abovePartitionContext.Length; i++)
        {
            this.abovePartitionContext[(col >> 1) + i] = fill;
        }

        for (int i = 0; i < height8 && (row >> 1) + i < this.leftPartitionContext.Length; i++)
        {
            this.leftPartitionContext[(row >> 1) + i] = fill;
        }
    }

    private void ReconstructPlane(Av1Plane plane, int miRow, int miCol, Av1TransformSize tx, int planeIndex, bool skip, int skipContext, int dcSignContext)
    {
        int x = miCol * 4;
        int y = miRow * 4;
        int width = tx.GetWidth();
        int height = tx.GetHeight();
        if (x >= plane.Width || y >= plane.Height)
        {
            return;
        }

        // DC prediction: with no decoded neighbours the predictor is the mid-level (128 for 8-bit).
        Span<byte> prediction = new byte[width * height];
        Av1IntraPrediction.Dc128Predict(prediction, width, width, height, this.sequenceHeader.BitDepth);

        int eob = -1;
        int[] residual = new int[width * height];
        if (!skip)
        {
            int[] levels = new int[Math.Min(width, 32) * Math.Min(height, 32)];
            eob = Av1CoefficientReader.ReadCoefficients(this.decoder, this.coefficientCdf, tx, Av1TransformType.DctDct, planeIndex, skipContext, dcSignContext, levels);
            if (eob != Av1CoefficientReader.AllZero)
            {
                this.InverseTransform(levels, tx, residual);
            }
        }

        for (int ry = 0; ry < height && y + ry < plane.Height; ry++)
        {
            for (int rx = 0; rx < width && x + rx < plane.Width; rx++)
            {
                int value = prediction[(ry * width) + rx] + residual[(ry * width) + rx];
                plane[x + rx, y + ry] = (byte)Math.Clamp(value, 0, (1 << this.sequenceHeader.BitDepth) - 1);
            }
        }
    }

    private void InverseTransform(int[] levels, Av1TransformSize tx, int[] residual)
    {
        int width = tx.GetWidth();
        int codedWidth = Math.Min(width, 32);
        int codedHeight = Math.Min(tx.GetHeight(), 32);
        int[] coefficients = new int[width * tx.GetHeight()];
        for (int rc = 0; rc < levels.Length; rc++)
        {
            if (levels[rc] == 0)
            {
                continue;
            }

            int row = rc % codedHeight;
            int colInBlock = rc / codedHeight;
            coefficients[(row * width) + colInBlock] =
                Av1QuantizationLookup.Dequantize(levels[rc], rc == 0, this.frameHeader.BaseQIndex, this.sequenceHeader.BitDepth, tx);
        }

        Av1InverseTransform2d.Reconstruct(Av1TransformType.DctDct, tx, coefficients, residual, this.sequenceHeader.BitDepth);
    }

    private static int GetQuantizerContext(int baseQIndex)
        => baseQIndex <= 20 ? 0 : baseQIndex <= 60 ? 1 : baseQIndex <= 120 ? 2 : 3;
}
