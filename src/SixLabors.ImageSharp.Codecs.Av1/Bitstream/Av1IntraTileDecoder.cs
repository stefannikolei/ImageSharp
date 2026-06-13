// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the tiles of an intra (key) frame into reconstructed luma and chroma planes. This covers the
/// recursively-split partition tree, the intra block decode (mode info, transform-size and
/// transform-type selection, the transform-block loop with neighbour-derived contexts) and DC
/// reconstruction. Unsupported syntax raises <see cref="NotSupportedException"/> so that streams beyond
/// the current coverage fail loudly rather than producing incorrect pixels.
/// </summary>
internal sealed class Av1IntraTileDecoder
{
    // Intra mode context lookup (dav1d_intra_mode_context).
    private static readonly int[] IntraModeContext = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];

    private static readonly int[][] SkipContextTable =
    [
        [1, 2, 2, 2, 3],
        [2, 4, 4, 4, 5],
        [2, 4, 4, 4, 5],
        [2, 4, 4, 4, 5],
        [3, 5, 5, 5, 6],
    ];

    private const byte LevelContextBaseline = 0x40; // cul_level 0, dc-sign "zero".

    private readonly ObuSequenceHeader sequenceHeader;
    private readonly ObuFrameHeader frameHeader;
    private readonly Av1ModeInfoCdfContext modeCdf;
    private readonly Av1CoefficientCdfContext coefficientCdf;

    private readonly Av1Plane luma;
    private readonly Av1Plane chromaU;
    private readonly Av1Plane chromaV;

    private readonly int subsamplingX;
    private readonly int subsamplingY;
    private readonly int midGrey;

    // Neighbour context arrays in 4x4 units. The 'above' arrays span the frame width; the 'left'
    // arrays span the frame height and are reset at the start of each superblock row.
    private readonly byte[] abovePartition;
    private readonly byte[] leftPartition;
    private readonly byte[] aboveSkip;
    private readonly byte[] leftSkip;
    private readonly byte[] aboveMode;
    private readonly byte[] leftMode;
    private readonly byte[] aboveTx;
    private readonly byte[] leftTx;
    private readonly LevelContext lumaLevels;
    private readonly LevelContext chromaULevels;
    private readonly LevelContext chromaVLevels;

    // CDEF post-filter state, gathered during decode and consumed by ApplyCdef.
    private readonly int miColumns;
    private readonly int miRows;
    private readonly bool[] noskip;
    private readonly int[] cdefIndices;
    private readonly int cdefColumns64;

    private Av1SymbolDecoder decoder = default!;
    private bool cdefRead;

    public Av1IntraTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.modeCdf = Av1ModeInfoCdfContext.CreateDefault();
        this.coefficientCdf = Av1CoefficientCdfContext.CreateDefault(GetQuantizerContext(frameHeader.BaseQIndex));

        this.subsamplingX = sequenceHeader.SubsamplingX;
        this.subsamplingY = sequenceHeader.SubsamplingY;
        this.midGrey = 1 << (sequenceHeader.BitDepth - 1);

        int width = frameHeader.FrameWidth;
        int height = frameHeader.FrameHeight;
        int chromaWidth = (width + this.subsamplingX) >> this.subsamplingX;
        int chromaHeight = (height + this.subsamplingY) >> this.subsamplingY;

        this.luma = new Av1Plane(width, height);
        this.chromaU = new Av1Plane(chromaWidth, chromaHeight);
        this.chromaV = new Av1Plane(chromaWidth, chromaHeight);

        int miCols = frameHeader.ModeInfoColumns;
        int miRows = frameHeader.ModeInfoRows;
        this.abovePartition = new byte[(miCols >> 1) + 1];
        this.leftPartition = new byte[(miRows >> 1) + 1];
        this.aboveSkip = new byte[miCols];
        this.leftSkip = new byte[miRows];
        this.aboveMode = new byte[miCols];
        this.leftMode = new byte[miRows];
        this.aboveTx = new byte[miCols];
        this.leftTx = new byte[miRows];
        this.lumaLevels = new LevelContext(miCols, miRows);
        this.chromaULevels = new LevelContext((miCols >> this.subsamplingX) + 1, (miRows >> this.subsamplingY) + 1);
        this.chromaVLevels = new LevelContext((miCols >> this.subsamplingX) + 1, (miRows >> this.subsamplingY) + 1);

        this.miColumns = miCols;
        this.miRows = miRows;
        this.noskip = new bool[miCols * miRows];
        this.cdefColumns64 = (miCols + 15) >> 4;
        this.cdefIndices = new int[this.cdefColumns64 * ((miRows + 15) >> 4)];
        Array.Fill(this.cdefIndices, -1);
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

        int superblock4 = this.sequenceHeader.Use128x128Superblock ? 32 : 16;
        Av1BlockSize superblock = this.sequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;

        for (int row = 0; row < this.frameHeader.ModeInfoRows; row += superblock4)
        {
            Array.Clear(this.leftPartition);
            Array.Clear(this.leftSkip);
            Array.Clear(this.leftMode);
            Array.Clear(this.leftTx);
            this.lumaLevels.ClearLeft();
            this.chromaULevels.ClearLeft();
            this.chromaVLevels.ClearLeft();

            for (int col = 0; col < this.frameHeader.ModeInfoColumns; col += superblock4)
            {
                this.cdefRead = false;
                this.DecodePartition(row, col, superblock);
            }
        }

        this.ApplyCdef();
    }

    /// <summary>
    /// Applies the constrained directional enhancement filter to the reconstructed planes (a port of
    /// dav1d's <c>cdef_brow</c> for the single-tile, 64x64-superblock case). All neighbour taps are read
    /// from a pre-filter clone of each plane so the result is independent of the block iteration order.
    /// </summary>
    private void ApplyCdef()
    {
        if (this.sequenceHeader.Use128x128Superblock)
        {
            // The 64x64 CDEF preset grid differs for 128x128 superblocks; not handled yet.
            return;
        }

        ObuFrameHeader.Cdef cdef = this.frameHeader.CdefParameters;
        int bitDepthMin8 = this.sequenceHeader.BitDepth - 8;
        int damping = cdef.Damping + bitDepthMin8;
        bool hasChroma = this.sequenceHeader.NumPlanes > 1;

        // dav1d uv_dirs: identity for 4:2:0/4:4:4, a remap for 4:2:2.
        bool is422 = this.subsamplingX == 1 && this.subsamplingY == 0;
        ReadOnlySpan<byte> uvDir = is422 ? [7, 0, 2, 4, 5, 6, 6, 6] : [0, 1, 2, 3, 4, 5, 6, 7];

        byte[] lumaSrc = (byte[])this.luma.Samples.Clone();
        byte[] uSrc = hasChroma ? (byte[])this.chromaU.Samples.Clone() : [];
        byte[] vSrc = hasChroma ? (byte[])this.chromaV.Samples.Clone() : [];

        int bw4 = this.miColumns;
        int bh4 = this.miRows;

        for (int by = 0; by < bh4; by += 2)
        {
            for (int bx = 0; bx < bw4; bx += 2)
            {
                int cdefIndex = this.cdefIndices[((by >> 4) * this.cdefColumns64) + (bx >> 4)];
                if (cdefIndex < 0)
                {
                    continue;
                }

                int yPriLevel = cdef.YPrimary[cdefIndex] << bitDepthMin8;
                int ySecLevel = cdef.YSecondary[cdefIndex] << bitDepthMin8;
                int uvPriLevel = cdef.UvPrimary[cdefIndex] << bitDepthMin8;
                int uvSecLevel = cdef.UvSecondary[cdefIndex] << bitDepthMin8;
                if (yPriLevel == 0 && ySecLevel == 0 && uvPriLevel == 0 && uvSecLevel == 0)
                {
                    continue;
                }

                // Skip 8x8 blocks whose four 4x4 units were all coded as skip.
                if (!this.AnyNoskip(bx, by))
                {
                    continue;
                }

                Av1Cdef.EdgeFlags edges = 0;
                if (bx > 0)
                {
                    edges |= Av1Cdef.EdgeFlags.Left;
                }

                if (bx + 2 < bw4)
                {
                    edges |= Av1Cdef.EdgeFlags.Right;
                }

                if (by > 0)
                {
                    edges |= Av1Cdef.EdgeFlags.Top;
                }

                if (by + 2 < bh4)
                {
                    edges |= Av1Cdef.EdgeFlags.Bottom;
                }

                int dir = 0;
                int variance = 0;
                if (yPriLevel != 0 || uvPriLevel != 0)
                {
                    dir = Av1Cdef.FindDirection(lumaSrc, ((by * 4) * this.luma.Width) + (bx * 4), this.luma.Width, out variance);
                }

                // Luma: 8x8 block, primary strength scaled by the block variance.
                if (yPriLevel != 0)
                {
                    int adjusted = Av1Cdef.AdjustStrength(yPriLevel, variance);
                    if (adjusted != 0 || ySecLevel != 0)
                    {
                        FilterPlaneBlock(this.luma, lumaSrc, bx * 4, by * 4, 8, 8, adjusted, ySecLevel, dir, damping, edges);
                    }
                }
                else if (ySecLevel != 0)
                {
                    FilterPlaneBlock(this.luma, lumaSrc, bx * 4, by * 4, 8, 8, 0, ySecLevel, 0, damping, edges);
                }

                // Chroma: subsampled block, no variance adjustment, damping reduced by one.
                if (hasChroma && (uvPriLevel != 0 || uvSecLevel != 0))
                {
                    int uvDirection = uvPriLevel != 0 ? uvDir[dir] : 0;
                    int cw = 8 >> this.subsamplingX;
                    int ch = 8 >> this.subsamplingY;
                    int cx = (bx * 4) >> this.subsamplingX;
                    int cy = (by * 4) >> this.subsamplingY;
                    FilterPlaneBlock(this.chromaU, uSrc, cx, cy, cw, ch, uvPriLevel, uvSecLevel, uvDirection, damping - 1, edges);
                    FilterPlaneBlock(this.chromaV, vSrc, cx, cy, cw, ch, uvPriLevel, uvSecLevel, uvDirection, damping - 1, edges);
                }
            }
        }
    }

    private bool AnyNoskip(int bx, int by)
    {
        for (int dy = 0; dy < 2 && by + dy < this.miRows; dy++)
        {
            for (int dx = 0; dx < 2 && bx + dx < this.miColumns; dx++)
            {
                if (this.noskip[((by + dy) * this.miColumns) + bx + dx])
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Gathers the pre-filter edge spans for one CDEF block from the plane clone and filters in place.
    private static void FilterPlaneBlock(Av1Plane plane, byte[] src, int px, int py, int w, int h, int priStrength, int secStrength, int dir, int damping, Av1Cdef.EdgeFlags edges)
    {
        int stride = plane.Width;
        int clampW = Math.Min(w, plane.Width - px);
        int clampH = Math.Min(h, plane.Height - py);
        if (clampW <= 0 || clampH <= 0)
        {
            return;
        }

        int topWidth = clampW + 4;
        byte[] top = new byte[2 * topWidth];
        byte[] bottom = new byte[2 * topWidth];
        byte[] left = new byte[clampH * 2];

        for (int r = 0; r < 2; r++)
        {
            for (int c = 0; c < topWidth; c++)
            {
                top[(r * topWidth) + c] = Sample(src, stride, plane.Width, plane.Height, px - 2 + c, py - 2 + r);
                bottom[(r * topWidth) + c] = Sample(src, stride, plane.Width, plane.Height, px - 2 + c, py + clampH + r);
            }
        }

        for (int y = 0; y < clampH; y++)
        {
            left[(y * 2) + 0] = Sample(src, stride, plane.Width, plane.Height, px - 2, py + y);
            left[(y * 2) + 1] = Sample(src, stride, plane.Width, plane.Height, px - 1, py + y);
        }

        Av1Cdef.FilterBlock(
            plane.Samples,
            (py * stride) + px,
            stride,
            left,
            top,
            bottom,
            priStrength,
            secStrength,
            dir,
            damping,
            clampW,
            clampH,
            edges);
    }

    private static byte Sample(byte[] src, int stride, int width, int height, int x, int y)
    {
        int cx = Math.Clamp(x, 0, width - 1);
        int cy = Math.Clamp(y, 0, height - 1);
        return src[(cy * stride) + cx];
    }

    private void DecodePartition(int row, int col, Av1BlockSize bsize)
    {
        if (row >= this.frameHeader.ModeInfoRows || col >= this.frameHeader.ModeInfoColumns)
        {
            return;
        }

        int half = bsize.GetWidth4() >> 1;
        bool hasRows = row + half < this.frameHeader.ModeInfoRows;
        bool hasCols = col + half < this.frameHeader.ModeInfoColumns;

        Av1Partition partition = bsize == Av1BlockSize.Block4x4
            ? Av1Partition.None
            : this.ReadPartition(row, col, bsize, hasRows, hasCols);

        switch (partition)
        {
            case Av1Partition.None:
                this.DecodeBlock(row, col, bsize);
                break;
            case Av1Partition.Split:
                Av1BlockSize sub = bsize.GetSubSize(Av1Partition.Split);
                this.DecodePartition(row, col, sub);
                this.DecodePartition(row, col + half, sub);
                this.DecodePartition(row + half, col, sub);
                this.DecodePartition(row + half, col + half, sub);
                break;
            default:
                throw new NotSupportedException($"Partition type {partition} is not supported yet.");
        }
    }

    private Av1Partition ReadPartition(int row, int col, Av1BlockSize bsize, bool hasRows, bool hasCols)
    {
        if (!hasRows || !hasCols)
        {
            throw new NotSupportedException("Partition signalling at the frame edge is not supported yet.");
        }

        int blockLevel = bsize.GetPartitionLevel();
        int shift = 4 - blockLevel;
        int above = (this.abovePartition[col >> 1] >> shift) & 1;
        int left = (this.leftPartition[row >> 1] >> shift) & 1;
        return (Av1Partition)this.decoder.ReadSymbol(this.modeCdf.Partition[blockLevel][above + (left << 1)]);
    }

    private void DecodeBlock(int row, int col, Av1BlockSize bsize)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();

        // skip flag.
        int skipContext = this.aboveSkip[col] + this.leftSkip[row];
        int skip = this.decoder.ReadSymbol(this.modeCdf.Skip[skipContext]);

        // cdef index: read once per superblock for the first non-skip block.
        if (skip == 0 && !this.cdefRead)
        {
            this.cdefRead = true;
            int cdefIndex = this.frameHeader.CdefBits > 0 ? (int)this.decoder.ReadLiteral(this.frameHeader.CdefBits) : 0;
            this.cdefIndices[((row >> 4) * this.cdefColumns64) + (col >> 4)] = cdefIndex;
        }

        // Record the non-skip status of every 4x4 unit so CDEF can leave fully-skipped blocks alone.
        if (skip == 0)
        {
            for (int dy = 0; dy < height4 && row + dy < this.miRows; dy++)
            {
                for (int dx = 0; dx < width4 && col + dx < this.miColumns; dx++)
                {
                    this.noskip[((row + dy) * this.miColumns) + col + dx] = true;
                }
            }
        }

        // luma intra mode.
        int aboveModeContext = IntraModeContext[this.aboveMode[col]];
        int leftModeContext = IntraModeContext[this.leftMode[row]];
        int yMode = this.decoder.ReadSymbol(this.modeCdf.KeyFrameYMode[aboveModeContext][leftModeContext]);
        EnsureSupportedMode(yMode);
        int yAngleDelta = this.ReadAngleDelta(yMode, bsize);

        // chroma intra mode.
        bool cflAllowed = bsize <= Av1BlockSize.Block32x32;
        int uvMode = this.decoder.ReadSymbol(this.modeCdf.UvMode[cflAllowed ? 1 : 0][yMode]);
        EnsureSupportedMode(uvMode);
        int uvAngleDelta = this.ReadAngleDelta(uvMode, bsize);

        // filter_intra: coded for DC luma blocks up to 32x32 when enabled.
        int filterIntraMode = -1;
        if (this.sequenceHeader.EnableFilterIntra && bsize <= Av1BlockSize.Block32x32 && yMode == 0)
        {
            int useFilterIntra = this.decoder.ReadSymbol(this.modeCdf.UseFilterIntra[(int)bsize]);
            if (useFilterIntra != 0)
            {
                filterIntraMode = this.decoder.ReadSymbol(this.modeCdf.FilterIntraMode);
            }
        }

        // transform size (TX_MODE_LARGEST forces the largest; TX_MODE_SELECT codes a depth).
        Av1TransformSize lumaTx = this.ReadTransformSize(row, col, bsize);

        // luma transform-block loop.
        this.DecodePlane(this.luma, this.lumaLevels, 0, row, col, bsize, lumaTx, yMode, yAngleDelta, filterIntraMode);

        // chroma transform-block loop (single transform per plane for the sizes handled here).
        Av1TransformSize chromaTx = bsize.GetMaxChromaTransformSize(this.sequenceHeader);
        int chromaRow = row >> this.subsamplingY;
        int chromaCol = col >> this.subsamplingX;
        this.DecodePlane(this.chromaU, this.chromaULevels, 1, chromaRow, chromaCol, bsize, chromaTx, uvMode, uvAngleDelta, -1);
        this.DecodePlane(this.chromaV, this.chromaVLevels, 2, chromaRow, chromaCol, bsize, chromaTx, uvMode, uvAngleDelta, -1);

        // record block-level neighbour contexts.
        Fill(this.aboveSkip, col, width4, (byte)skip);
        Fill(this.leftSkip, row, height4, (byte)skip);
        Fill(this.aboveMode, col, width4, (byte)yMode);
        Fill(this.leftMode, row, height4, (byte)yMode);
        Fill(this.aboveTx, col, width4, (byte)(lumaTx.GetWidthLog2() - 2));
        Fill(this.leftTx, row, height4, (byte)(lumaTx.GetHeightLog2() - 2));

        byte partitionFill = bsize.PartitionContextFill();
        Fill(this.abovePartition, col >> 1, width4 >> 1, partitionFill);
        Fill(this.leftPartition, row >> 1, height4 >> 1, partitionFill);
    }

    private Av1TransformSize ReadTransformSize(int row, int col, Av1BlockSize bsize)
    {
        Av1TransformSize maxTx = bsize.GetMaxTransformSize();
        int maxIndex = maxTx.GetWidthLog2() - 2; // square: the .max field.
        if (this.frameHeader.TxMode != 2 || maxIndex == 0)
        {
            return maxTx;
        }

        int aboveTxContext = this.aboveTx[col] >= maxTx.GetWidthLog2() - 2 ? 1 : 0;
        int leftTxContext = this.leftTx[row] >= maxTx.GetHeightLog2() - 2 ? 1 : 0;
        int txContext = leftTxContext + aboveTxContext;
        int depth = this.decoder.ReadSymbol(this.modeCdf.TransformDepth[maxIndex - 1][txContext]);

        Av1TransformSize tx = maxTx;
        for (int i = 0; i < depth; i++)
        {
            tx = (Av1TransformSize)((int)tx - 1); // square sub-size.
        }

        return tx;
    }

    private void DecodePlane(Av1Plane plane, LevelContext levels, int planeIndex, int miRow, int miCol, Av1BlockSize bsize, Av1TransformSize tx, int intraMode, int angleDelta, int filterIntraMode)
    {
        int blockWidth4 = bsize.GetWidth4() >> (planeIndex == 0 ? 0 : this.subsamplingX);
        int blockHeight4 = bsize.GetHeight4() >> (planeIndex == 0 ? 0 : this.subsamplingY);
        int txWidth4 = tx.GetWidth() >> 2;
        int txHeight4 = tx.GetHeight() >> 2;
        bool blockEqualsTx = blockWidth4 == txWidth4 && blockHeight4 == txHeight4;

        int[] coefficientLevels = new int[Math.Min(tx.GetWidth(), 32) * Math.Min(tx.GetHeight(), 32)];

        for (int dy = 0; dy < blockHeight4; dy += txHeight4)
        {
            for (int dx = 0; dx < blockWidth4; dx += txWidth4)
            {
                int txRow = miRow + dy;
                int txCol = miCol + dx;
                int x = txCol * 4;
                int y = txRow * 4;
                if (x >= plane.Width || y >= plane.Height)
                {
                    continue;
                }

                int skipContext = planeIndex == 0
                    ? LumaCoefficientSkipContext(levels, txCol, txRow, txWidth4, txHeight4, blockEqualsTx)
                    : this.ChromaCoefficientSkipContext(levels, txCol, txRow, txWidth4, txHeight4, bsize, tx);
                int dcSignContext = DcSignContext(levels, txCol, txRow, txWidth4, txHeight4);

                Array.Clear(coefficientLevels);
                int eob = Av1CoefficientReader.ReadCoefficients(
                    this.decoder,
                    this.coefficientCdf,
                    tx,
                    Av1TransformType.DctDct,
                    planeIndex,
                    skipContext,
                    dcSignContext,
                    coefficientLevels,
                    planeIndex == 0 ? this.modeCdf : null,
                    intraMode,
                    this.frameHeader.ReducedTxSet);

                this.Reconstruct(plane, x, y, tx, coefficientLevels, eob, intraMode, angleDelta, filterIntraMode);

                byte resContext = LevelContextByte(coefficientLevels, eob);
                levels.Write(txCol, txRow, txWidth4, txHeight4, resContext);
            }
        }
    }

    private void Reconstruct(Av1Plane plane, int x, int y, Av1TransformSize tx, int[] levels, int eob, int intraMode, int angleDelta, int filterIntraMode)
    {
        int width = tx.GetWidth();
        int height = tx.GetHeight();

        byte[] prediction = new byte[width * height];
        this.Predict(plane, x, y, width, height, intraMode, angleDelta, filterIntraMode, prediction);

        int[] residual = new int[width * height];
        if (eob != Av1CoefficientReader.AllZero)
        {
            int codedHeight = Math.Min(height, 32);
            int[] coefficients = new int[width * height];
            for (int rc = 0; rc < levels.Length; rc++)
            {
                if (levels[rc] == 0)
                {
                    continue;
                }

                int rowInBlock = rc % codedHeight;
                int colInBlock = rc / codedHeight;
                coefficients[(rowInBlock * width) + colInBlock] =
                    Av1QuantizationLookup.Dequantize(levels[rc], rc == 0, this.frameHeader.BaseQIndex, this.sequenceHeader.BitDepth, tx);
            }

            Av1InverseTransform2d.Reconstruct(Av1TransformType.DctDct, tx, coefficients, residual, this.sequenceHeader.BitDepth);
        }

        int maxValue = (1 << this.sequenceHeader.BitDepth) - 1;
        for (int ry = 0; ry < height && y + ry < plane.Height; ry++)
        {
            for (int rx = 0; rx < width && x + rx < plane.Width; rx++)
            {
                plane[x + rx, y + ry] = (byte)Math.Clamp(prediction[(ry * width) + rx] + residual[(ry * width) + rx], 0, maxValue);
            }
        }
    }

    private void Predict(Av1Plane plane, int x, int y, int width, int height, int intraMode, int angleDelta, int filterIntraMode, byte[] prediction)
    {
        // Filter-intra (luma, DC blocks): predict each square unit from the prepared edges.
        if (filterIntraMode >= 0)
        {
            this.PrepareEdges(plane, x, y, width, height, out byte[] fAbove, out byte[] fLeft, out byte fTopLeft);
            Av1FilterIntraPrediction.Predict(fAbove, fLeft, fTopLeft, width, filterIntraMode, prediction);
            return;
        }

        // DC prediction is computed directly from the available neighbour averages.
        if (intraMode == 0)
        {
            byte dc = (byte)this.PredictDc(plane, x, y, width, height);
            Array.Fill(prediction, dc);
            return;
        }

        // Directional modes (VERT..VERT_LEFT) use the extended edges and the angular predictor.
        if (intraMode is >= 1 and <= 8)
        {
            this.PrepareDirectionalEdges(plane, x, y, width, out byte[] dAbove, out byte[] dLeft, out byte dTopLeft);
            Av1DirectionalPrediction.Predict(
                dAbove,
                dLeft,
                dTopLeft,
                width,
                intraMode,
                angleDelta,
                this.sequenceHeader.EnableIntraEdgeFilter,
                false,
                y > 0,
                x > 0,
                plane.Width - x,
                plane.Height - y,
                prediction);
            return;
        }

        this.PrepareEdges(plane, x, y, width, height, out byte[] above, out byte[] left, out byte topLeft);
        switch (intraMode)
        {
            case 9: // SMOOTH_PRED
                Av1IntraPrediction.SmoothPredict(prediction, width, width, height, above, left);
                break;
            case 10: // SMOOTH_V_PRED
                Av1IntraPrediction.SmoothVerticalPredict(prediction, width, width, height, above, left);
                break;
            case 11: // SMOOTH_H_PRED
                Av1IntraPrediction.SmoothHorizontalPredict(prediction, width, width, height, above, left);
                break;
            default: // 12 PAETH_PRED
                Av1IntraPrediction.PaethPredict(prediction, width, width, height, above, left, topLeft);
                break;
        }
    }

    // Gathers the extended reference edges (2*size above and left) for directional prediction, applying
    // the dav1d availability fills and frame-edge replication. Only square transforms are handled.
    private void PrepareDirectionalEdges(Av1Plane plane, int x, int y, int size, out byte[] above, out byte[] left, out byte topLeft)
    {
        bool hasAbove = y > 0;
        bool hasLeft = x > 0;
        int extent = 2 * size;
        above = new byte[extent];
        left = new byte[extent];

        if (hasAbove)
        {
            for (int i = 0; i < extent; i++)
            {
                int sx = Math.Min(x + i, plane.Width - 1);
                above[i] = plane[sx, y - 1];
            }
        }
        else
        {
            byte fill = hasLeft ? plane[x - 1, y] : (byte)(this.midGrey - 1);
            Array.Fill(above, fill);
        }

        if (hasLeft)
        {
            for (int i = 0; i < extent; i++)
            {
                int sy = Math.Min(y + i, plane.Height - 1);
                left[i] = plane[x - 1, sy];
            }
        }
        else
        {
            byte fill = hasAbove ? plane[x, y - 1] : (byte)(this.midGrey + 1);
            Array.Fill(left, fill);
        }

        topLeft = hasLeft
            ? hasAbove ? plane[x - 1, y - 1] : plane[x - 1, y]
            : hasAbove ? plane[x, y - 1] : (byte)this.midGrey;
    }

    private int ReadAngleDelta(int mode, Av1BlockSize bsize)
    {
        // Angle delta is coded for directional modes on blocks of at least 8x8.
        if (mode is >= 1 and <= 8 && bsize.GetWidthLog2() >= 1)
        {
            return this.decoder.ReadSymbol(this.modeCdf.AngleDelta[mode - 1]) - 3;
        }

        return 0;
    }

    private void PrepareEdges(Av1Plane plane, int x, int y, int width, int height, out byte[] above, out byte[] left, out byte topLeft)
    {
        bool hasAbove = y > 0;
        bool hasLeft = x > 0;
        byte mid = (byte)this.midGrey;
        above = new byte[width];
        left = new byte[height];

        if (hasAbove)
        {
            for (int i = 0; i < width; i++)
            {
                above[i] = plane[Math.Min(x + i, plane.Width - 1), y - 1];
            }
        }
        else
        {
            byte fill = hasLeft ? plane[x - 1, y] : (byte)(this.midGrey - 1);
            Array.Fill(above, fill);
        }

        if (hasLeft)
        {
            for (int i = 0; i < height; i++)
            {
                left[i] = plane[x - 1, Math.Min(y + i, plane.Height - 1)];
            }
        }
        else
        {
            byte fill = hasAbove ? plane[x, y - 1] : (byte)(this.midGrey + 1);
            Array.Fill(left, fill);
        }

        topLeft = hasLeft
            ? hasAbove ? plane[x - 1, y - 1] : plane[x - 1, y]
            : hasAbove ? plane[x, y - 1] : mid;
    }

    private static void EnsureSupportedMode(int mode)
    {
        // All 13 intra prediction modes (DC, the 8 directional and the 4 non-directional) are handled;
        // chroma-from-luma (CfL, uv mode 13) is not yet supported.
        if (mode is < 0 or > 12)
        {
            throw new NotSupportedException($"Intra prediction mode {mode} is not supported yet.");
        }
    }

    private int PredictDc(Av1Plane plane, int x, int y, int width, int height)
    {
        bool hasAbove = y > 0;
        bool hasLeft = x > 0;
        long sum = 0;
        int count = 0;

        if (hasAbove)
        {
            for (int i = 0; i < width && x + i < plane.Width; i++)
            {
                sum += plane[x + i, y - 1];
                count++;
            }
        }

        if (hasLeft)
        {
            for (int i = 0; i < height && y + i < plane.Height; i++)
            {
                sum += plane[x - 1, y + i];
                count++;
            }
        }

        return count == 0 ? this.midGrey : (int)((sum + (count >> 1)) / count);
    }

    private static int LumaCoefficientSkipContext(LevelContext levels, int txCol, int txRow, int txWidth4, int txHeight4, bool blockEqualsTx)
    {
        if (blockEqualsTx)
        {
            return 0;
        }

        int la = 0;
        for (int i = 0; i < txWidth4; i++)
        {
            la |= levels.Above(txCol + i);
        }

        int ll = 0;
        for (int i = 0; i < txHeight4; i++)
        {
            ll |= levels.Left(txRow + i);
        }

        return SkipContextTable[Math.Min(la & 0x3F, 4)][Math.Min(ll & 0x3F, 4)];
    }

    private int ChromaCoefficientSkipContext(LevelContext levels, int txCol, int txRow, int txWidth4, int txHeight4, Av1BlockSize bsize, Av1TransformSize tx)
    {
        int blockLwAdjusted = bsize.GetWidthLog2() - (this.subsamplingX != 0 ? 1 : 0);
        int blockLhAdjusted = bsize.GetWidthLog2() - (this.subsamplingY != 0 ? 1 : 0);
        bool notOneBlock = blockLwAdjusted > tx.GetWidthLog2() - 2 || blockLhAdjusted > tx.GetHeightLog2() - 2;

        int ca = 0;
        for (int i = 0; i < txWidth4; i++)
        {
            if (levels.Above(txCol + i) != LevelContextBaseline)
            {
                ca = 1;
                break;
            }
        }

        int cl = 0;
        for (int i = 0; i < txHeight4; i++)
        {
            if (levels.Left(txRow + i) != LevelContextBaseline)
            {
                cl = 1;
                break;
            }
        }

        return 7 + ((notOneBlock ? 1 : 0) * 3) + ca + cl;
    }

    private static int DcSignContext(LevelContext levels, int txCol, int txRow, int txWidth4, int txHeight4)
    {
        int sum = 0;
        for (int i = 0; i < txWidth4; i++)
        {
            sum += levels.Above(txCol + i) >> 6;
        }

        for (int i = 0; i < txHeight4; i++)
        {
            sum += levels.Left(txRow + i) >> 6;
        }

        int s = sum - txWidth4 - txHeight4;
        return s < 0 ? 1 : s > 0 ? 2 : 0;
    }

    private static byte LevelContextByte(int[] levels, int eob)
    {
        if (eob == Av1CoefficientReader.AllZero)
        {
            return LevelContextBaseline;
        }

        int culLevel = 0;
        for (int i = 0; i < levels.Length; i++)
        {
            culLevel += Math.Abs(levels[i]);
        }

        int dcSignLevel = levels[0] == 0 ? 0x40 : levels[0] > 0 ? 0x80 : 0x00;
        return (byte)(Math.Min(culLevel, 63) | dcSignLevel);
    }

    private static void Fill(byte[] context, int start, int count, byte value)
    {
        for (int i = 0; i < count && start + i < context.Length; i++)
        {
            context[start + i] = value;
        }
    }

    private static int GetQuantizerContext(int baseQIndex)
        => baseQIndex <= 20 ? 0 : baseQIndex <= 60 ? 1 : baseQIndex <= 120 ? 2 : 3;

    /// <summary>
    /// The coefficient level-context bytes for one plane: an 'above' row spanning the frame width and a
    /// 'left' column spanning the frame height (reset per superblock row), in 4x4 units.
    /// </summary>
    private sealed class LevelContext
    {
        private readonly byte[] above;
        private readonly byte[] left;

        public LevelContext(int cols, int rows)
        {
            this.above = new byte[cols];
            this.left = new byte[rows];
            Array.Fill(this.above, LevelContextBaseline);
            Array.Fill(this.left, LevelContextBaseline);
        }

        public byte Above(int col) => col < this.above.Length ? this.above[col] : LevelContextBaseline;

        public byte Left(int row) => row < this.left.Length ? this.left[row] : LevelContextBaseline;

        public void Write(int col, int row, int width4, int height4, byte value)
        {
            for (int i = 0; i < width4 && col + i < this.above.Length; i++)
            {
                this.above[col + i] = value;
            }

            for (int i = 0; i < height4 && row + i < this.left.Length; i++)
            {
                this.left[row + i] = value;
            }
        }

        public void ClearLeft() => Array.Fill(this.left, LevelContextBaseline);
    }
}
