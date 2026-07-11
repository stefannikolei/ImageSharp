// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the tiles of a frame into reconstructed luma and chroma planes: the recursively-split
/// partition tree, the per-block decode and the shared residual reconstruction and post-filter pipeline
/// (deblocking, CDEF and loop restoration). The base implementation decodes intra (key) frames; the
/// per-block decode is <see langword="virtual"/> so an inter tile decoder can reuse the reconstruction
/// surface and override only the prediction. Unsupported syntax raises <see cref="NotSupportedException"/>
/// so that streams beyond the current coverage fail loudly rather than producing incorrect pixels.
/// </summary>
internal class Av1TileDecoder
{
    // Intra mode context lookup (dav1d_intra_mode_context).
    private protected static readonly int[] IntraModeContext = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];

    private static readonly int[][] SkipContextTable =
    [
        [1, 2, 2, 2, 3],
        [2, 4, 4, 4, 5],
        [2, 4, 4, 4, 5],
        [2, 4, 4, 4, 5],
        [3, 5, 5, 5, 6],
    ];

    // dav1d_txfm_dimensions[].sub: the transform size one split-depth smaller, indexed by Av1TransformSize.
    private static readonly Av1TransformSize[] SubTransformSize =
    [
        Av1TransformSize.Size4x4, Av1TransformSize.Size4x4, Av1TransformSize.Size8x8, Av1TransformSize.Size16x16,
        Av1TransformSize.Size32x32, Av1TransformSize.Size4x4, Av1TransformSize.Size4x4, Av1TransformSize.Size8x8,
        Av1TransformSize.Size8x8, Av1TransformSize.Size16x16, Av1TransformSize.Size16x16, Av1TransformSize.Size32x32,
        Av1TransformSize.Size32x32, Av1TransformSize.Size4x8, Av1TransformSize.Size8x4, Av1TransformSize.Size8x16,
        Av1TransformSize.Size16x8, Av1TransformSize.Size16x32, Av1TransformSize.Size32x16,
    ];

    // dav1d_filter_mode_to_y_mode: the intra direction used for a filter-intra block's transform-type CDF.
    private static readonly int[] FilterModeToYMode = [0, 1, 2, 6, 0];

    // dav1d_sgr_params: the two radii per self-guided restoration index (used to gate weight coding).
    private static readonly int[][] SgrParams =
    [
        [140, 3236], [112, 2158], [93, 1618], [80, 1438], [70, 1295], [58, 1177], [47, 1079], [37, 996],
        [30, 925], [25, 863], [0, 2589], [0, 1618], [0, 1177], [0, 925], [56, 0], [22, 0],
    ];

    // Running loop-restoration reference values per plane (Wiener taps and SGR weights), for the subexp
    // delta coding; reset to the spec defaults at the start of the tile.
    private readonly int[][] lrRefFilterV = [new int[3], new int[3], new int[3]];
    private readonly int[][] lrRefFilterH = [new int[3], new int[3], new int[3]];
    private readonly int[][] lrRefSgrWeights = [new int[2], new int[2], new int[2]];

    // Per-restoration-unit decoded filter, keyed by (plane, unit top-left x, unit top-left y) in plane
    // pixels; consumed by the loop-restoration filtering pass after deblocking and CDEF.
    private readonly Dictionary<(int Plane, int X, int Y), LrUnit> lrUnits = [];

    // The pre-CDEF (deblocked) and pre-LR (CDEF) plane snapshots, captured by the post-filter pipeline so
    // loop restoration can read stripe-boundary rows from the deblocked image and interior rows from the
    // CDEF image, exactly as dav1d's lr_lpf_line / frame buffer split requires.
    private readonly Av1Plane?[] deblockSnapshot = new Av1Plane?[3];

    private const byte LevelContextBaseline = 0x40; // cul_level 0, dc-sign "zero".

    private protected readonly ObuSequenceHeader sequenceHeader;
    private protected readonly ObuFrameHeader frameHeader;
    private protected Av1ModeInfoCdfContext modeCdf;
    private protected Av1CoefficientCdfContext coefficientCdf;

    // The bounds of the tile currently being decoded, in 4x4 units (the whole frame for single-tile
    // frames). Availability at the tile's top/left edges follows these, not the frame edges.
    private protected Av1TileBounds tileBounds;

    private protected Av1Plane luma;
    private protected Av1Plane chromaU;
    private protected Av1Plane chromaV;

    private protected readonly int subsamplingX;
    private protected readonly int subsamplingY;
    private readonly int midGrey;

    // Neighbour context arrays in 4x4 units. The 'above' arrays span the frame width; the 'left'
    // arrays span the frame height and are reset at the start of each superblock row.
    private readonly byte[] abovePartition;
    private readonly byte[] leftPartition;
    private protected readonly byte[] aboveSkip;
    private protected readonly byte[] leftSkip;
    private protected readonly byte[] aboveMode;
    private protected readonly byte[] leftMode;
    private readonly byte[] aboveUvMode;
    private readonly byte[] leftUvMode;
    private protected readonly sbyte[] aboveTx;
    private protected readonly sbyte[] leftTx;

    // Whether the current block's above/left neighbour uses a smooth prediction mode, which reduces the
    // directional-prediction edge-filter strength (dav1d's ANGLE_SMOOTH_EDGE_FLAG / is_sm). Computed per
    // block before reconstruction; the luma flag uses neighbour luma modes, the chroma flag uv modes.
    private bool lumaEdgeSmooth;
    private protected byte blockIntraEdgeFlags = Av1IntraEdgeFlags.AllTrAndBl;

    // Palette neighbour state (dav1d BlockContext.pal_sz, t->pal_sz_uv and t->al_pal): the luma and
    // chroma palette sizes per 4x4 neighbour cell, and the palette colours (three planes of up to
    // eight entries per cell) the size caches read from.
    private readonly byte[] abovePalSize;
    private readonly byte[] leftPalSize;
    private readonly byte[] abovePalUvSize;
    private readonly byte[] leftPalUvSize;
    private readonly ushort[] abovePalette;
    private readonly ushort[] leftPalette;

    // Whether the current block painted its planes from a palette: the transform loop then uses the
    // painted samples as the prediction (dav1d's skip_y_pred/skip_uv_pred).
    private bool currentBlockPaletteY;
    private bool currentBlockPaletteUv;

    /// <summary>Gets a value indicating whether the block currently decoding is inter-coded (the
    /// inter decoder overrides this; skipped inter blocks deblock no interior transform edges).</summary>
    private protected virtual bool CurrentBlockIsInter => false;
    private bool transformHasTopRight = true;
    private bool transformHasBottomLeft = true;
    private bool chromaEdgeSmooth;
    private protected readonly LevelContext lumaLevels;
    private protected readonly LevelContext chromaULevels;
    private protected readonly LevelContext chromaVLevels;

    // CDEF post-filter state, gathered during decode and consumed by ApplyCdef.
    private protected readonly int miColumns;
    private protected readonly int miRows;
    private readonly bool[] noskip;
    private readonly int[] cdefIndices;
    private readonly int cdefColumns64;

    // Per-4x4 luma "has been reconstructed" map, used to determine intra reference-sample availability
    // (above-right / below-left samples are replicated when their source has not been decoded yet).
    private readonly bool[] lumaDecoded;

    // Per-4x4 deblocking filter levels (dav1d's lf.level): four bytes per luma-grid cell — luma vertical,
    // luma horizontal, chroma U and chroma V — filled per block from the frame level and the block's
    // reference/mode deltas. Chroma values live at the chroma-subsampled cell positions in the same grid.
    private readonly byte[] lfLevels;

    // Per-4x4 deblocking metadata: the transform-size log2 (in 4-unit width/height) covering each cell and
    // whether the cell begins a transform block (a vertical/horizontal filter edge), for luma and chroma.
    private readonly byte[] lumaTxLw;
    private readonly byte[] lumaTxLh;
    private readonly bool[] lumaEdgeV;
    private readonly bool[] lumaEdgeH;
    private readonly byte[] chromaTxLw;
    private readonly byte[] chromaTxLh;
    private readonly bool[] chromaEdgeV;
    private readonly bool[] chromaEdgeH;
    private readonly int chromaStride4;
    private readonly int chromaRows4;

    // Deblocking iteration bounds in 4x4 units, derived from the visible (crop) size: the overhanging
    // reconstruction area is never filtered (dav1d filters up to f->w4/h4).
    private readonly int lumaCropCols4;
    private readonly int lumaCropRows4;
    private readonly int chromaCropCols4;
    private readonly int chromaCropRows4;

    private protected Av1SymbolDecoder decoder = default!;

    // Segmentation state: the frame's parameters, the segment map coded by this frame (4x4 resolution)
    // and the current block's segment id.
    private readonly ObuSegmentationParams segmentation;
    private readonly byte[] segmentMap;
    private int currentSegmentId;

    // Whether each segment codes losslessly (4x4 Walsh-Hadamard transforms, dav1d's
    // segmentation.lossless array).
    private readonly bool[] losslessSegments = new bool[8];

    private protected bool CurrentBlockLossless => this.losslessSegments[this.currentSegmentId];

    // Per-superblock quantizer/loop-filter delta state (dav1d ts->last_qidx / last_delta_lf), reset per
    // tile, and the current block's effective per-plane DC/AC quantizer indices.
    private int currentQIndex;
    private readonly int[] currentDeltaLf = new int[4];
    private readonly int[] blockDcQIndex = new int[3];
    private readonly int[] blockAcQIndex = new int[3];

    public Av1TileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader)
        : this(sequenceHeader, frameHeader, Av1FrameCdfSet.CreateDefault(frameHeader.BaseQIndex))
    {
    }

    public Av1TileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1FrameCdfSet cdfs)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.Cdfs = cdfs;
        this.modeCdf = cdfs.ModeInfo;
        this.coefficientCdf = cdfs.Coefficient;

        this.subsamplingX = sequenceHeader.SubsamplingX;
        this.subsamplingY = sequenceHeader.SubsamplingY;
        this.midGrey = 1 << (sequenceHeader.BitDepth - 1);

        int width = frameHeader.FrameWidth;
        int height = frameHeader.FrameHeight;
        int chromaWidth = (width + this.subsamplingX) >> this.subsamplingX;
        int chromaHeight = (height + this.subsamplingY) >> this.subsamplingY;

        // The planes cover the frame rounded up to whole superblocks so blocks and transform blocks that
        // overhang the visible frame reconstruct fully (the reference decoder pads its picture buffers the
        // same way; chroma-from-luma reads those samples at frame edges). The crop dimensions carry the
        // visible size, which also bounds motion-compensation edge replication.
        int superblockSize = sequenceHeader.Use128x128Superblock ? 128 : 64;
        int alignedWidth = (width + superblockSize - 1) & ~(superblockSize - 1);
        int alignedHeight = (height + superblockSize - 1) & ~(superblockSize - 1);
        int alignedChromaWidth = alignedWidth >> this.subsamplingX;
        int alignedChromaHeight = alignedHeight >> this.subsamplingY;
        this.luma = new Av1Plane(alignedWidth, alignedHeight, width, height);
        this.chromaU = new Av1Plane(alignedChromaWidth, alignedChromaHeight, chromaWidth, chromaHeight);
        this.chromaV = new Av1Plane(alignedChromaWidth, alignedChromaHeight, chromaWidth, chromaHeight);

        // A monochrome stream codes no chroma: the placeholder planes stay at neutral grey so the
        // colour conversion of the displayed frame yields grey without a special path.
        if (sequenceHeader.NumPlanes == 1)
        {
            Array.Fill(this.chromaU.Samples, (ushort)this.midGrey);
            Array.Fill(this.chromaV.Samples, (ushort)this.midGrey);
        }

        int miCols = frameHeader.ModeInfoColumns;
        int miRows = frameHeader.ModeInfoRows;
        this.abovePartition = new byte[(miCols >> 1) + 1];
        this.leftPartition = new byte[(miRows >> 1) + 1];
        this.aboveSkip = new byte[miCols];
        this.leftSkip = new byte[miRows];
        this.aboveMode = new byte[miCols];
        this.leftMode = new byte[miRows];
        this.aboveUvMode = new byte[miCols];
        this.leftUvMode = new byte[miRows];
        this.aboveTx = new sbyte[miCols];
        this.leftTx = new sbyte[miRows];
        this.abovePalSize = new byte[miCols];
        this.leftPalSize = new byte[miRows];
        this.abovePalUvSize = new byte[miCols];
        this.leftPalUvSize = new byte[miRows];
        this.abovePalette = new ushort[miCols * 24];
        this.leftPalette = new ushort[miRows * 24];

        // The intra tx-size context is initialised to -1 at the frame edge (dav1d's tx_intra reset),
        // so an unavailable neighbour never satisfies the ">= current tx category" comparison.
        Array.Fill(this.aboveTx, (sbyte)-1);
        // The chroma context length is the subsampled-and-rounded-up mi count ((f->bw + ss_hor) >>
        // ss_hor): the reference decoder clips context stores there, so a transform overhanging the
        // frame edge leaves the cells beyond it at the baseline for later context reads.
        this.lumaLevels = new LevelContext(miCols, miRows);
        this.chromaULevels = new LevelContext((miCols + this.subsamplingX) >> this.subsamplingX, (miRows + this.subsamplingY) >> this.subsamplingY);
        this.chromaVLevels = new LevelContext((miCols + this.subsamplingX) >> this.subsamplingX, (miRows + this.subsamplingY) >> this.subsamplingY);

        this.miColumns = miCols;
        this.miRows = miRows;
        this.noskip = new bool[miCols * miRows];
        this.cdefColumns64 = (miCols + 15) >> 4;
        this.cdefIndices = new int[this.cdefColumns64 * ((miRows + 15) >> 4)];
        Array.Fill(this.cdefIndices, -1);
        this.lumaDecoded = new bool[miCols * miRows];
        this.lfLevels = new byte[miCols * miRows * 4];

        this.lumaTxLw = new byte[miCols * miRows];
        this.lumaTxLh = new byte[miCols * miRows];
        this.lumaEdgeV = new bool[miCols * miRows];
        this.lumaEdgeH = new bool[miCols * miRows];
        this.chromaStride4 = alignedChromaWidth >> 2;
        this.chromaRows4 = alignedChromaHeight >> 2;
        this.lumaCropCols4 = (width + 3) >> 2;
        this.lumaCropRows4 = (height + 3) >> 2;
        this.chromaCropCols4 = (chromaWidth + 3) >> 2;
        this.chromaCropRows4 = (chromaHeight + 3) >> 2;
        int chromaCells = Math.Max(1, this.chromaStride4 * this.chromaRows4);
        this.chromaTxLw = new byte[chromaCells];
        this.chromaTxLh = new byte[chromaCells];
        this.chromaEdgeV = new bool[chromaCells];
        this.chromaEdgeH = new bool[chromaCells];

        this.tileBounds = new Av1TileBounds(0, miCols, 0, miRows);

        this.segmentation = frameHeader.SegmentationParams ?? ObuSegmentationParams.Disabled;
        this.segmentMap = new byte[miCols * miRows];

        // Per-segment lossless (dav1d segmentation.lossless): the segment-adjusted base quantizer is
        // zero and no per-plane quantizer delta applies; such blocks use 4x4 Walsh-Hadamard
        // transforms.
        bool zeroDeltas = frameHeader.DeltaQYDc == 0 && frameHeader.DeltaQUDc == 0 && frameHeader.DeltaQUAc == 0
            && frameHeader.DeltaQVDc == 0 && frameHeader.DeltaQVAc == 0;
        for (int seg = 0; seg < 8; seg++)
        {
            int qidx = this.segmentation.Enabled
                ? Math.Clamp(frameHeader.BaseQIndex + this.segmentation.DeltaQ[seg], 0, 255)
                : frameHeader.BaseQIndex;
            this.losslessSegments[seg] = qidx == 0 && zeroDeltas;
        }
        if (this.segmentation.Enabled)
        {
            if (!this.segmentation.UpdateMap || this.segmentation.TemporalUpdate)
            {
                throw new NotSupportedException("Segment maps predicted from a previous frame are not supported yet.");
            }

            if (this.segmentation.PreSkip)
            {
                throw new NotSupportedException("The segmentation reference, skip and global-mv features are not supported yet.");
            }

            for (int i = 0; i <= this.segmentation.LastActiveSegmentId; i++)
            {
                if (Math.Clamp(frameHeader.BaseQIndex + this.segmentation.DeltaQ[i], 0, 255) == 0)
                {
                    throw new NotSupportedException("Lossless segments are not supported yet.");
                }
            }
        }

        this.currentQIndex = frameHeader.BaseQIndex;
        this.UpdateBlockQIndex();
    }

    /// <summary>
    /// Gets the frame's CDF set: the initial state passed in (defaults or a primary reference's saved
    /// state) that the decode adapts in place, becoming the state saved at the frame end.
    /// </summary>
    public Av1FrameCdfSet Cdfs { get; }

    /// <summary>Gets the reconstructed luma plane.</summary>
    /// <summary>Gets the stream bit depth the planes are reconstructed at.</summary>
    public int BitDepth => this.sequenceHeader.BitDepth;

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
        => this.DecodeTiles([tileData]);

    /// <summary>
    /// Decodes every tile of the frame (in raster order) and applies the frame-wide post filters. Each
    /// tile starts from the frame's initial CDF state; the frame-end CDF state is the adapted state of
    /// the tile selected by <c>context_update_tile_id</c>.
    /// </summary>
    /// <param name="tiles">The compressed bytes of each tile, in tile raster order.</param>
    public void DecodeTiles(IReadOnlyList<ReadOnlyMemory<byte>> tiles)
    {
        int[] columnStarts = this.frameHeader.TileColumnStarts ?? [];
        int[] rowStarts = this.frameHeader.TileRowStarts ?? [];
        int tileColumns = Math.Max(1, columnStarts.Length - 1);
        int tileRows = Math.Max(1, rowStarts.Length - 1);
        if (tiles.Count != tileColumns * tileRows)
        {
            throw new InvalidDataException($"Expected {tileColumns * tileRows} tiles, got {tiles.Count}.");
        }

        // Every tile adapts from the frame's initial CDF state; the context-update tile decodes
        // directly into the frame set (which the reference store saves at the frame end), the others
        // into throwaway clones taken before any tile adapts anything.
        Av1FrameCdfSet[] tileCdfs = new Av1FrameCdfSet[tiles.Count];
        for (int i = 0; i < tiles.Count; i++)
        {
            tileCdfs[i] = i == this.frameHeader.ContextUpdateTileId ? this.Cdfs : this.Cdfs.Clone();
        }

        for (int tileRow = 0; tileRow < tileRows; tileRow++)
        {
            for (int tileColumn = 0; tileColumn < tileColumns; tileColumn++)
            {
                int index = (tileRow * tileColumns) + tileColumn;
                this.tileBounds = new Av1TileBounds(
                    columnStarts.Length > 1 ? columnStarts[tileColumn] : 0,
                    columnStarts.Length > 1 ? columnStarts[tileColumn + 1] : this.miColumns,
                    rowStarts.Length > 1 ? rowStarts[tileRow] : 0,
                    rowStarts.Length > 1 ? rowStarts[tileRow + 1] : this.miRows);
                this.BindCdfs(tileCdfs[index]);
                this.DecodeTileData(tiles[index]);
            }
        }

        this.BindCdfs(this.Cdfs);
        this.ApplyPostFilters();
    }

    // Rebinds the per-tile CDF contexts read during symbol decoding (each tile adapts its own set).
    private protected virtual void BindCdfs(Av1FrameCdfSet cdfs)
    {
        this.modeCdf = cdfs.ModeInfo;
        this.coefficientCdf = cdfs.Coefficient;
    }

    // Resets the neighbour contexts above the current tile's columns (the specification's
    // clear_above_context, over the tile range) and the in-tile running state at the tile start.
    private protected virtual void OnTileStart()
    {
        int start = this.tileBounds.ColumnStart;
        int count = this.tileBounds.ColumnEnd - start;
        Array.Clear(this.abovePartition, start >> 1, ((this.tileBounds.ColumnEnd + 1) >> 1) - (start >> 1));
        Array.Clear(this.aboveSkip, start, count);
        Array.Clear(this.aboveMode, start, count);
        int chromaModeStart = start >> this.subsamplingX;
        Array.Clear(this.aboveUvMode, chromaModeStart, ((this.tileBounds.ColumnEnd + this.subsamplingX) >> this.subsamplingX) - chromaModeStart);
        Array.Fill(this.aboveTx, (sbyte)-1, start, count);
        Array.Clear(this.abovePalSize, start, count);
        Array.Clear(this.abovePalUvSize, start, count);
        this.lumaLevels.ClearAbove(start, count);
        int chromaStart = start >> this.subsamplingX;
        int chromaCount = ((this.tileBounds.ColumnEnd + this.subsamplingX) >> this.subsamplingX) - chromaStart;
        this.chromaULevels.ClearAbove(chromaStart, chromaCount);
        this.chromaVLevels.ClearAbove(chromaStart, chromaCount);

        this.currentQIndex = this.frameHeader.BaseQIndex;
        Array.Clear(this.currentDeltaLf);
        this.currentSegmentId = 0;
        this.UpdateBlockQIndex();

        for (int p = 0; p < 3; p++)
        {
            this.lrRefFilterV[p][0] = 3;
            this.lrRefFilterV[p][1] = -7;
            this.lrRefFilterV[p][2] = 15;
            this.lrRefFilterH[p][0] = 3;
            this.lrRefFilterH[p][1] = -7;
            this.lrRefFilterH[p][2] = 15;
            this.lrRefSgrWeights[p][0] = -32;
            this.lrRefSgrWeights[p][1] = 31;
        }
    }

    // Decodes one tile's superblocks from its compressed data over the current tile bounds.
    private void DecodeTileData(ReadOnlyMemory<byte> tileData)
    {
        this.decoder = new Av1SymbolDecoder(tileData);
        this.OnTileStart();

        int superblock4 = this.sequenceHeader.Use128x128Superblock ? 32 : 16;
        Av1BlockSize superblock = this.sequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;

        for (int row = this.tileBounds.RowStart; row < this.tileBounds.RowEnd; row += superblock4)
        {
            Array.Clear(this.leftPartition);
            Array.Clear(this.leftSkip);
            Array.Clear(this.leftMode);
            Array.Fill(this.leftTx, (sbyte)-1);
            Array.Clear(this.leftPalSize);
            Array.Clear(this.leftPalUvSize);
            this.lumaLevels.ClearLeft();
            this.chromaULevels.ClearLeft();
            this.chromaVLevels.ClearLeft();
            this.OnSuperblockRowStart();

            for (int col = this.tileBounds.ColumnStart; col < this.tileBounds.ColumnEnd; col += superblock4)
            {
                this.ReadRestorationUnits(row, col);
                this.DecodePartition(row, col, superblock, this.sequenceHeader.Use128x128Superblock ? Av1IntraEdgeTree.Root128 : Av1IntraEdgeTree.Root64);
            }
        }
    }

    // Applies the frame-wide post filters (deblocking, CDEF, loop restoration) after every tile has
    // been decoded.
    private void ApplyPostFilters()
    {
        this.ApplyDeblock();

        // Snapshot the deblocked planes that have loop restoration before CDEF overwrites them; loop
        // restoration reads stripe-boundary rows from this pre-CDEF image.
        ObuFrameHeader.LoopRestoration lrParams = this.frameHeader.LoopRestorationParameters;
        bool anyRestoration = false;
        if (lrParams.Types is not null)
        {
            for (int p = 0; p < this.sequenceHeader.NumPlanes; p++)
            {
                if (lrParams.Types[p] != 0)
                {
                    anyRestoration = true;
                    this.deblockSnapshot[p] = ClonePlane(this.PlaneFor(p));
                }
            }
        }

        this.ApplyCdef();

        // Super-resolution: upscale the CDEF output (and the pre-CDEF snapshot loop restoration
        // reads at stripe boundaries) to the output width; loop restoration then runs upscaled.
        if (this.frameHeader.UseSuperres)
        {
            int bitDepth = this.sequenceHeader.BitDepth;
            int lumaReadWidth = this.frameHeader.ModeInfoColumns << 2;
            int chromaReadWidth = lumaReadWidth >> this.subsamplingX;
            this.luma = Av1SuperRes.Upscale(this.luma, this.frameHeader.UpscaledWidth, bitDepth, lumaReadWidth);
            if (this.sequenceHeader.NumPlanes > 1)
            {
                int upscaledChroma = (this.frameHeader.UpscaledWidth + this.subsamplingX) >> this.subsamplingX;
                this.chromaU = Av1SuperRes.Upscale(this.chromaU, upscaledChroma, bitDepth, chromaReadWidth);
                this.chromaV = Av1SuperRes.Upscale(this.chromaV, upscaledChroma, bitDepth, chromaReadWidth);
            }

            for (int pl = 0; pl < 3; pl++)
            {
                if (this.deblockSnapshot[pl] is { } snapshot)
                {
                    int upscaled = pl == 0
                        ? this.frameHeader.UpscaledWidth
                        : (this.frameHeader.UpscaledWidth + this.subsamplingX) >> this.subsamplingX;
                    this.deblockSnapshot[pl] = Av1SuperRes.Upscale(snapshot, upscaled, bitDepth, pl == 0 ? lumaReadWidth : chromaReadWidth);
                }
            }
        }

        if (anyRestoration)
        {
            this.ApplyLoopRestoration();
        }
    }

    private Av1Plane PlaneFor(int plane) => plane == 0 ? this.luma : plane == 1 ? this.chromaU : this.chromaV;

    // Applies loop restoration (specification section 7.17) to every plane that signalled it. The frame is
    // processed in 64-row (56 for the first) stripes per superblock row; for each stripe the applicable
    // restoration unit selects the filter. Interior rows read the CDEF output, stripe-boundary rows the
    // deblocked image (dav1d's lr_lpf_line split).
    private void ApplyLoopRestoration()
    {
        ObuFrameHeader.LoopRestoration lr = this.frameHeader.LoopRestorationParameters;
        int sbSize = this.sequenceHeader.Use128x128Superblock ? 128 : 64;
        int lumaHeight = this.frameHeader.FrameHeight;
        int sbRows = (lumaHeight + sbSize - 1) / sbSize;

        for (int p = 0; p < this.sequenceHeader.NumPlanes; p++)
        {
            if (lr.Types[p] == 0)
            {
                continue;
            }

            Av1Plane plane = this.PlaneFor(p);
            ushort[] dst = plane.Samples;
            ushort[] cdef = (ushort[])dst.Clone();
            ushort[] deblock = this.deblockSnapshot[p]!.Samples;
            int width = plane.CropWidth;
            int height = plane.CropHeight;
            int stride = plane.Width;
            int ssVer = p != 0 ? this.subsamplingY : 0;
            int ssHor = p != 0 ? this.subsamplingX : 0;
            int unitSize = 1 << lr.UnitSizeLog2[p != 0 ? 1 : 0];
            int maxUnit = unitSize + (unitSize >> 1);
            int half = unitSize >> 1;
            int sbStep = sbSize >> ssVer;

            // Loop-restoration stripes are always 64 luma rows tall (the first is 8 shorter), independent
            // of the superblock size; only the superblock-row stepping uses sbStep.
            int stripeStep = 64 >> ssVer;
            int topOffset = 8 >> ssVer;

            for (int sby = 0; sby < sbRows; sby++)
            {
                bool notLast = sby + 1 < sbRows;
                int yStripe = (sby * sbStep) - (sby > 0 ? topOffset : 0);
                int rowH = Math.Min(((sby + 1) * sbStep) - (notLast ? topOffset : 0), height);

                int rowY = sby * sbStep;
                int alignedY = rowY & ~(unitSize - 1);
                if (alignedY != 0 && alignedY + half > height)
                {
                    alignedY -= unitSize;
                }

                int y = yStripe;
                int stripeH = Math.Min(stripeStep - (y == 0 ? topOffset : 0), rowH - y);
                while (y + stripeH <= rowH && stripeH > 0)
                {
                    bool haveTop = y > 0;
                    bool haveBottom = notLast || (y + stripeH != rowH);
                    this.RestoreStripeColumns(p, dst, cdef, deblock, width, stride, unitSize, maxUnit, alignedY, y, y + stripeH, haveTop, haveBottom);

                    y += stripeH;
                    stripeH = Math.Min(stripeStep, rowH - y);
                }
            }
        }
    }

    // Filters every restoration unit column intersecting one stripe.
    private void RestoreStripeColumns(int plane, ushort[] dst, ushort[] cdef, ushort[] deblock, int width, int stride, int unitSize, int maxUnit, int alignedY, int stripeTop, int stripeEnd, bool haveTop, bool haveBottom)
    {
        int x = 0;
        while (true)
        {
            bool isLast = x + maxUnit > width;
            int unitWidth = isLast ? width - x : unitSize;
            bool haveRight = !isLast;
            bool haveLeft = x > 0;

            if (this.lrUnits.TryGetValue((plane, x, alignedY), out LrUnit unit) && unit.Type != 0)
            {
                if (unit.Type == 2)
                {
                    Av1WienerFilter.Stripe(dst, cdef, deblock, stride, x, unitWidth, stripeTop, stripeEnd, haveTop, haveBottom, haveLeft, haveRight, unit.FilterH, unit.FilterV, this.sequenceHeader.BitDepth);
                }
                else
                {
                    Av1SelfGuidedFilter.Stripe(dst, cdef, deblock, width, stride, x, unitWidth, stripeTop, stripeEnd, haveTop, haveBottom, haveLeft, haveRight, SgrParams[unit.SgrIdx][0], SgrParams[unit.SgrIdx][1], unit.SgrW0, unit.SgrW1, this.sequenceHeader.BitDepth);
                }
            }

            if (isLast)
            {
                break;
            }

            x += unitSize;
        }
    }

    // A decoded loop-restoration unit's filter parameters.
    private struct LrUnit
    {
        public int Type;
        public int[] FilterH;
        public int[] FilterV;
        public int SgrIdx;
        public int SgrW0;
        public int SgrW1;
    }

    // Reads the per-superblock loop-restoration unit coefficients (a port of dav1d's
    // read_restoration_info dispatch in the tile loop) and records the decoded per-unit filter, keyed by
    // the unit's plane-pixel top-left position, for the loop-restoration filtering pass.
    private void ReadRestorationUnits(int row, int col)
    {
        ObuFrameHeader.LoopRestoration lr = this.frameHeader.LoopRestorationParameters;
        for (int p = 0; p < this.sequenceHeader.NumPlanes; p++)
        {
            if (lr.Types[p] == 0)
            {
                continue;
            }

            int ssVer = p != 0 ? this.subsamplingY : 0;
            int ssHor = p != 0 ? this.subsamplingX : 0;
            int unitSizeLog2 = lr.UnitSizeLog2[p != 0 ? 1 : 0];
            int unitSize = 1 << unitSizeLog2;
            int mask = unitSize - 1;
            int halfUnit = unitSize >> 1;

            int y = (row * 4) >> ssVer;
            int h = (this.frameHeader.FrameHeight + ssVer) >> ssVer;
            if ((y & mask) != 0 || (y != 0 && y + halfUnit > h))
            {
                continue;
            }

            if (this.frameHeader.UseSuperres)
            {
                // With super-resolution the units live in the upscaled domain: this superblock covers
                // the units whose upscaled positions project into its coded span (dav1d read_lr).
                int w = (this.frameHeader.UpscaledWidth + ssHor) >> ssHor;
                int numUnits = Math.Max(1, (w + halfUnit) >> unitSizeLog2);
                int d = this.frameHeader.SuperresDenominator;
                int rnd = (unitSize * 8) - 1;
                int shift = unitSizeLog2 + 3;
                int sbStep = this.sequenceHeader.Use128x128Superblock ? 32 : 16;
                int x0 = (((4 * col * d) >> ssHor) + rnd) >> shift;
                int x1 = (((4 * (col + sbStep) * d) >> ssHor) + rnd) >> shift;
                for (int u = x0; u < Math.Min(x1, numUnits); u++)
                {
                    this.ReadRestorationInfo(p, lr.Types[p], u << unitSizeLog2, y);
                }

                continue;
            }

            int x = (col * 4) >> ssHor;
            int w2 = (this.frameHeader.FrameWidth + ssHor) >> ssHor;
            if ((x & mask) != 0 || (x != 0 && x + halfUnit > w2))
            {
                continue;
            }

            this.ReadRestorationInfo(p, lr.Types[p], x, y);
        }
    }

    // Decodes one restoration unit's filter coefficients (dav1d read_restoration_info) and stores them.
    private void ReadRestorationInfo(int plane, int frameType, int unitX, int unitY)
    {
        int unitType;
        if (frameType == 1)
        {
            // Switchable: none / Wiener / SGR.
            int filter = this.decoder.ReadSymbol(this.modeCdf.RestoreSwitchable);
            unitType = filter + (filter != 0 ? 1 : 0);
        }
        else
        {
            int present = this.decoder.ReadSymbol(frameType == 2 ? this.modeCdf.RestoreWiener : this.modeCdf.RestoreSgrProj);
            unitType = present != 0 ? frameType : 0;
        }

        if (unitType == 2)
        {
            int[] fv = this.lrRefFilterV[plane];
            int[] fh = this.lrRefFilterH[plane];
            fv[0] = plane != 0 ? 0 : this.DecodeSubexp(fv[0] + 5, 16, 1) - 5;
            fv[1] = this.DecodeSubexp(fv[1] + 23, 32, 2) - 23;
            fv[2] = this.DecodeSubexp(fv[2] + 17, 64, 3) - 17;
            fh[0] = plane != 0 ? 0 : this.DecodeSubexp(fh[0] + 5, 16, 1) - 5;
            fh[1] = this.DecodeSubexp(fh[1] + 23, 32, 2) - 23;
            fh[2] = this.DecodeSubexp(fh[2] + 17, 64, 3) - 17;
            this.lrUnits[(plane, unitX, unitY)] = new LrUnit { Type = 2, FilterH = [fh[0], fh[1], fh[2]], FilterV = [fv[0], fv[1], fv[2]] };
        }
        else if (unitType == 3)
        {
            int idx = (int)this.decoder.ReadLiteral(4);
            int[] w = this.lrRefSgrWeights[plane];
            w[0] = SgrParams[idx][0] != 0 ? this.DecodeSubexp(w[0] + 96, 128, 4) - 96 : 0;
            w[1] = SgrParams[idx][1] != 0 ? this.DecodeSubexp(w[1] + 32, 128, 4) - 32 : 95;
            this.lrUnits[(plane, unitX, unitY)] = new LrUnit { Type = 3, SgrIdx = idx, SgrW0 = w[0], SgrW1 = 128 - (w[0] + w[1]) };
        }
    }

    // dav1d_msac_decode_subexp: subexponential delta decode referenced to 'reference' (n >> k == 8).
    private int DecodeSubexp(int reference, int n, int k)
    {
        int a = 0;
        if (this.decoder.ReadLiteral(1) != 0)
        {
            if (this.decoder.ReadLiteral(1) != 0)
            {
                k += (int)this.decoder.ReadLiteral(1) + 1;
            }

            a = 1 << k;
        }

        int v = (int)this.decoder.ReadLiteral(k) + a;
        return (reference * 2) <= n ? InvRecenter(reference, v) : n - 1 - InvRecenter(n - 1 - reference, v);
    }

    private static int InvRecenter(int r, int v)
    {
        if (v > (r << 1))
        {
            return v;
        }

        return (v & 1) == 0 ? (v >> 1) + r : r - ((v + 1) >> 1);
    }

    // Records the transform-size and edge metadata for one transform block, used by the deblocking pass.
    // A skipped inter block codes no residual, so its interior transform-grid edges are not
    // deblocked (dav1d mask_edges_inter's !skip gate); only the block's own left/top edges are.
    private void RecordTxEdges(int planeIndex, int txCol, int txRow, int txWidth4, int txHeight4, bool edgeLeftAllowed = true, bool edgeTopAllowed = true)
    {
        byte lw = (byte)System.Numerics.BitOperations.Log2((uint)txWidth4);
        byte lh = (byte)System.Numerics.BitOperations.Log2((uint)txHeight4);

        if (planeIndex == 0)
        {
            for (int dy = 0; dy < txHeight4 && txRow + dy < this.miRows; dy++)
            {
                for (int dx = 0; dx < txWidth4 && txCol + dx < this.miColumns; dx++)
                {
                    int mi = ((txRow + dy) * this.miColumns) + txCol + dx;
                    this.lumaTxLw[mi] = lw;
                    this.lumaTxLh[mi] = lh;
                    this.lumaEdgeV[mi] = dx == 0 && edgeLeftAllowed;
                    this.lumaEdgeH[mi] = dy == 0 && edgeTopAllowed;
                }
            }
        }
        else if (planeIndex == 1)
        {
            for (int dy = 0; dy < txHeight4 && txRow + dy < this.chromaRows4; dy++)
            {
                for (int dx = 0; dx < txWidth4 && txCol + dx < this.chromaStride4; dx++)
                {
                    int mi = ((txRow + dy) * this.chromaStride4) + txCol + dx;
                    this.chromaTxLw[mi] = lw;
                    this.chromaTxLh[mi] = lh;
                    this.chromaEdgeV[mi] = dx == 0 && edgeLeftAllowed;
                    this.chromaEdgeH[mi] = dy == 0 && edgeTopAllowed;
                }
            }
        }
    }

    /// <summary>
    /// Applies the deblocking loop filter to the reconstructed planes (specification section 7.14) for
    /// the intra-frame case, where every block references the intra frame so the per-plane filter level
    /// is uniform. Vertical edges are filtered first, then horizontal edges.
    /// </summary>
    private void ApplyDeblock()
    {
        ObuFrameHeader.LoopFilter lf = this.frameHeader.LoopFilterParameters;
        if (lf.Levels is null || (lf.Levels[0] == 0 && lf.Levels[1] == 0))
        {
            return;
        }

        int[] limit = new int[64];
        int[] blimit = new int[64];
        int sharp = lf.Sharpness;
        for (int level = 0; level < 64; level++)
        {
            int lim = level;
            if (sharp > 0)
            {
                lim >>= (sharp + 3) >> 2;
                lim = Math.Min(lim, 9 - sharp);
            }

            lim = Math.Max(lim, 1);
            limit[level] = lim;
            blimit[level] = (2 * (level + 2)) + lim;
        }

        this.DeblockPlane(this.luma, this.miColumns, this.lumaCropCols4, this.lumaCropRows4, this.lumaTxLw, this.lumaTxLh, this.lumaEdgeV, this.lumaEdgeH, 0, 1, true, limit, blimit);

        if (this.sequenceHeader.NumPlanes > 1)
        {
            this.DeblockPlane(this.chromaU, this.chromaStride4, this.chromaCropCols4, this.chromaCropRows4, this.chromaTxLw, this.chromaTxLh, this.chromaEdgeV, this.chromaEdgeH, 2, 2, false, limit, blimit);
            this.DeblockPlane(this.chromaV, this.chromaStride4, this.chromaCropCols4, this.chromaCropRows4, this.chromaTxLw, this.chromaTxLh, this.chromaEdgeV, this.chromaEdgeH, 3, 3, false, limit, blimit);
        }
    }

    // Records a block's four deblocking filter levels (luma vertical/horizontal, chroma U/V) into the
    // per-4x4 level cache, a port of the level fill in dav1d's create_lf_mask_intra/inter driven by
    // calc_lf_values. The reference index is 0 for an intra block and the block's reference plus one for
    // an inter block; the mode index selects mode_deltas[0] for GLOBALMV and mode_deltas[1] otherwise.
    private protected void RecordLoopFilterLevels(int row, int col, Av1BlockSize bsize, bool hasChroma, int reference, int modeIndex)
    {
        ObuFrameHeader.LoopFilter lf = this.frameHeader.LoopFilterParameters;
        if (lf.Levels is null || (lf.Levels[0] == 0 && lf.Levels[1] == 0))
        {
            return;
        }

        bool multiLf = this.frameHeader.DeltaLfMulti;
        int seg = this.currentSegmentId;
        bool hasSeg = this.segmentation.Enabled;
        byte lumaV = (byte)this.CalcLfLevel(lf.Levels[0], this.currentDeltaLf[0], hasSeg ? this.segmentation.DeltaLfYVertical[seg] : 0, reference, modeIndex, isChroma: false);
        byte lumaH = (byte)this.CalcLfLevel(lf.Levels[1], this.currentDeltaLf[multiLf ? 1 : 0], hasSeg ? this.segmentation.DeltaLfYHorizontal[seg] : 0, reference, modeIndex, isChroma: false);
        int width4 = Math.Min(bsize.GetWidth4(), this.miColumns - col);
        int height4 = Math.Min(bsize.GetHeight4(), this.miRows - row);
        for (int y = 0; y < height4; y++)
        {
            int cell = (((row + y) * this.miColumns) + col) * 4;
            for (int x = 0; x < width4; x++, cell += 4)
            {
                this.lfLevels[cell] = lumaV;
                this.lfLevels[cell + 1] = lumaH;
            }
        }

        if (!hasChroma || this.sequenceHeader.NumPlanes == 1)
        {
            return;
        }

        byte chromaU = (byte)this.CalcLfLevel(lf.Levels[2], this.currentDeltaLf[multiLf ? 2 : 0], hasSeg ? this.segmentation.DeltaLfU[seg] : 0, reference, modeIndex, isChroma: true);
        byte chromaV = (byte)this.CalcLfLevel(lf.Levels[3], this.currentDeltaLf[multiLf ? 3 : 0], hasSeg ? this.segmentation.DeltaLfV[seg] : 0, reference, modeIndex, isChroma: true);
        // The clamp uses the mi-grid chroma dimensions (not the superblock-aligned transform grid,
        // which can be wider than the mi grid): the cells live in the luma-mi-strided level array.
        int chromaCol = col >> this.subsamplingX;
        int chromaRow = row >> this.subsamplingY;
        int chromaWidth4 = Math.Min((bsize.GetWidth4() + this.subsamplingX) >> this.subsamplingX, ((this.miColumns + this.subsamplingX) >> this.subsamplingX) - chromaCol);
        int chromaHeight4 = Math.Min((bsize.GetHeight4() + this.subsamplingY) >> this.subsamplingY, ((this.miRows + this.subsamplingY) >> this.subsamplingY) - chromaRow);
        for (int y = 0; y < chromaHeight4; y++)
        {
            int cell = (((chromaRow + y) * this.miColumns) + chromaCol) * 4;
            for (int x = 0; x < chromaWidth4; x++, cell += 4)
            {
                this.lfLevels[cell + 2] = chromaU;
                this.lfLevels[cell + 3] = chromaV;
            }
        }
    }

    // dav1d calc_lf_value: the block's filter level is the frame level adjusted by the superblock
    // loop-filter delta, the segment delta and the reference/mode deltas. A zero chroma base level is
    // never adjusted.
    private int CalcLfLevel(int baseLevel, int lfDelta, int segDelta, int reference, int modeIndex, bool isChroma)
    {
        if (isChroma && baseLevel == 0)
        {
            return 0;
        }

        int adjusted = Math.Clamp(Math.Clamp(baseLevel + lfDelta, 0, 63) + segDelta, 0, 63);
        ObuFrameHeader.LoopFilter lf = this.frameHeader.LoopFilterParameters;
        if (!lf.DeltaEnabled)
        {
            return adjusted;
        }

        int shift = adjusted >= 32 ? 1 : 0;
        int delta = reference == 0 ? lf.RefDeltas[0] : lf.ModeDeltas[modeIndex] + lf.RefDeltas[reference];
        return Math.Clamp(adjusted + (delta * (1 << shift)), 0, 63);
    }

    private void DeblockPlane(Av1Plane plane, int stride4, int cols4, int rows4, byte[] txLw, byte[] txLh, bool[] edgeV, bool[] edgeH, int levelOffsetV, int levelOffsetH, bool isLuma, int[] limit, int[] blimit)
    {
        int stride = plane.Width;
        int maxIdx = isLuma ? 2 : 1;

        for (int r4 = 0; r4 < rows4; r4++)
        {
            for (int c4 = 1; c4 < cols4; c4++)
            {
                int mi = (r4 * stride4) + c4;
                if (!edgeV[mi])
                {
                    continue;
                }

                // The edge filters with the current cell's level, falling back to the left neighbour's
                // when it is zero (dav1d: L = l[0] ? l[0] : l[-1]); zero on both sides means no filter.
                int cell = ((r4 * this.miColumns) + c4) * 4;
                int level = this.lfLevels[cell + levelOffsetV];
                if (level == 0)
                {
                    level = this.lfLevels[cell - 4 + levelOffsetV];
                }

                if (level == 0)
                {
                    continue;
                }

                int idx = Math.Min(maxIdx, Math.Min(txLw[mi], txLw[mi - 1]));
                int wd = isLuma ? 4 << idx : 4 + (2 * idx);
                int px = c4 * 4;
                int py = r4 * 4;
                if (px < plane.Width && py < plane.Height)
                {
                    Av1LoopFilter.FilterEdge(plane.Samples, (py * stride) + px, stride, 1, blimit[level], limit[level], level >> 4, wd, this.sequenceHeader.BitDepth);
                }
            }
        }

        for (int r4 = 1; r4 < rows4; r4++)
        {
            for (int c4 = 0; c4 < cols4; c4++)
            {
                int mi = (r4 * stride4) + c4;
                if (!edgeH[mi])
                {
                    continue;
                }

                int cell = ((r4 * this.miColumns) + c4) * 4;
                int level = this.lfLevels[cell + levelOffsetH];
                if (level == 0)
                {
                    level = this.lfLevels[cell - (this.miColumns * 4) + levelOffsetH];
                }

                if (level == 0)
                {
                    continue;
                }

                int idx = Math.Min(maxIdx, Math.Min(txLh[mi], txLh[mi - stride4]));
                int wd = isLuma ? 4 << idx : 4 + (2 * idx);
                int px = c4 * 4;
                int py = r4 * 4;
                if (px < plane.Width && py < plane.Height)
                {
                    Av1LoopFilter.FilterEdge(plane.Samples, (py * stride) + px, 1, stride, blimit[level], limit[level], level >> 4, wd, this.sequenceHeader.BitDepth);
                }
            }
        }
    }

    /// <summary>
    /// Applies the constrained directional enhancement filter to the reconstructed planes (a port of
    /// dav1d's <c>cdef_brow</c> for the single-tile, 64x64-superblock case). All neighbour taps are read
    /// from a pre-filter clone of each plane so the result is independent of the block iteration order.
    /// </summary>
    private void ApplyCdef()
    {
        ObuFrameHeader.Cdef cdef = this.frameHeader.CdefParameters;
        int bitDepthMin8 = this.sequenceHeader.BitDepth - 8;
        int damping = cdef.Damping + bitDepthMin8;
        bool hasChroma = this.sequenceHeader.NumPlanes > 1;

        // dav1d uv_dirs: identity for 4:2:0/4:4:4, a remap for 4:2:2.
        bool is422 = this.subsamplingX == 1 && this.subsamplingY == 0;
        ReadOnlySpan<byte> uvDir = is422 ? [7, 0, 2, 4, 5, 6, 6, 6] : [0, 1, 2, 3, 4, 5, 6, 7];

        ushort[] lumaSrc = (ushort[])this.luma.Samples.Clone();
        ushort[] uSrc = hasChroma ? (ushort[])this.chromaU.Samples.Clone() : [];
        ushort[] vSrc = hasChroma ? (ushort[])this.chromaV.Samples.Clone() : [];

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
                    dir = Av1Cdef.FindDirection(lumaSrc, ((by * 4) * this.luma.Width) + (bx * 4), this.luma.Width, out variance, this.sequenceHeader.BitDepth);
                }

                // Luma: 8x8 block, primary strength scaled by the block variance.
                if (yPriLevel != 0)
                {
                    int adjusted = Av1Cdef.AdjustStrength(yPriLevel, variance);
                    if (adjusted != 0 || ySecLevel != 0)
                    {
                        FilterPlaneBlock(this.luma, lumaSrc, this.sequenceHeader.BitDepth, bx * 4, by * 4, 8, 8, adjusted, ySecLevel, dir, damping, edges);
                    }
                }
                else if (ySecLevel != 0)
                {
                    FilterPlaneBlock(this.luma, lumaSrc, this.sequenceHeader.BitDepth, bx * 4, by * 4, 8, 8, 0, ySecLevel, 0, damping, edges);
                }

                // Chroma: subsampled block, no variance adjustment, damping reduced by one.
                if (hasChroma && (uvPriLevel != 0 || uvSecLevel != 0))
                {
                    int uvDirection = uvPriLevel != 0 ? uvDir[dir] : 0;
                    int cw = 8 >> this.subsamplingX;
                    int ch = 8 >> this.subsamplingY;
                    int cx = (bx * 4) >> this.subsamplingX;
                    int cy = (by * 4) >> this.subsamplingY;
                    FilterPlaneBlock(this.chromaU, uSrc, this.sequenceHeader.BitDepth, cx, cy, cw, ch, uvPriLevel, uvSecLevel, uvDirection, damping - 1, edges);
                    FilterPlaneBlock(this.chromaV, vSrc, this.sequenceHeader.BitDepth, cx, cy, cw, ch, uvPriLevel, uvSecLevel, uvDirection, damping - 1, edges);
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
    private static void FilterPlaneBlock(Av1Plane plane, ushort[] src, int bitDepth, int px, int py, int w, int h, int priStrength, int secStrength, int dir, int damping, Av1Cdef.EdgeFlags edges)
    {
        int stride = plane.Width;
        int clampW = Math.Min(w, plane.Width - px);
        int clampH = Math.Min(h, plane.Height - py);
        if (clampW <= 0 || clampH <= 0)
        {
            return;
        }

        int topWidth = clampW + 4;
        ushort[] top = new ushort[2 * topWidth];
        ushort[] bottom = new ushort[2 * topWidth];
        ushort[] left = new ushort[clampH * 2];

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
            edges,
            bitDepth);
    }

    private static ushort Sample(ushort[] src, int stride, int width, int height, int x, int y)
    {
        int cx = Math.Clamp(x, 0, width - 1);
        int cy = Math.Clamp(y, 0, height - 1);
        return src[(cy * stride) + cx];
    }

    // Propagates the "top-right neighbour already decoded" flag through the partition recursion, a port of
    // dav1d's precomputed intra_edge_tree (restricted to the single I444 top-has-right bit, the only one
    // any consumer currently needs). The rule is uniform across every partition depth: a quad split's
    // top-left and bottom-left children always have it, the bottom-right child never does, and the
    // top-right child (and any single "wide" side of a 2-way split on the same edge) inherits the parent's
    // value unchanged. A block gains it unconditionally the moment its right edge sits at a horizontal
    // split boundary (the classic "left half of a vertical split" / "first strip of a 4-way split" case).
    // Dispatches each block's intra-edge availability flags from the edge tree (dav1d decode_sb),
    // then decodes the block; the flags feed both the intra above-right/left-bottom edge extensions
    // and the motion-vector scan's top-right availability.
    private void DecodeEdgeBlock(int row, int col, Av1BlockSize bsize, byte edgeFlags)
    {
        this.blockIntraEdgeFlags = edgeFlags;
        this.DecodeBlock(row, col, bsize, (edgeFlags & Av1IntraEdgeFlags.I444TopHasRight) != 0);
    }

    private void DecodePartition(int row, int col, Av1BlockSize bsize, Av1EdgeNode node)
    {
        if (row >= this.frameHeader.ModeInfoRows || col >= this.frameHeader.ModeInfoColumns)
        {
            return;
        }

        int side = bsize.GetWidth4();
        int half = side >> 1;
        int quarter = side >> 2;
        bool hasRows = row + half < this.frameHeader.ModeInfoRows;
        bool hasCols = col + half < this.frameHeader.ModeInfoColumns;

        Av1Partition partition = bsize == Av1BlockSize.Block4x4
            ? Av1Partition.None
            : this.ReadPartition(row, col, bsize, hasRows, hasCols);

        // Sub-sizes are only needed for splitting partitions; a 4x4 block is always a NONE leaf.
        Av1BlockSize sub = Av1BlockSize.Block4x4;
        Av1BlockSize horz = Av1BlockSize.Block4x4;
        Av1BlockSize vert = Av1BlockSize.Block4x4;
        if (side >= 2)
        {
            sub = bsize.GetSplitSubSize();
            horz = Av1BlockSizeExtensions.FromDimensions(side, half);
            vert = Av1BlockSizeExtensions.FromDimensions(half, side);
        }

        switch (partition)
        {
            case Av1Partition.None:
                this.DecodeEdgeBlock(row, col, bsize, node.O);
                break;
            case Av1Partition.Split:
                if (bsize == Av1BlockSize.Block8x8)
                {
                    // The four 4x4 leaves are decoded directly; dav1d does not recurse to a 4x4 level.
                    // The top-left leaf's interpolation filter is saved and restored before the
                    // bottom-right leaf, whose sub-8x8 chroma prediction needs it (dav1d tl_4x4_filter).
                    Av1EdgeTip tip = (Av1EdgeTip)node;
                    this.DecodeEdgeBlock(row, col, sub, Av1IntraEdgeFlags.AllTrAndBl);
                    (int F0, int F1) topLeftFilter = this.TopLeft4x4Filter;
                    this.DecodeEdgeBlock(row, col + half, sub, tip.Split[0]);
                    this.DecodeEdgeBlock(row + half, col, sub, tip.Split[1]);
                    this.TopLeft4x4Filter = topLeftFilter;
                    this.DecodeEdgeBlock(row + half, col + half, sub, tip.Split[2]);
                }
                else
                {
                    Av1EdgeBranch branch = (Av1EdgeBranch)node;
                    this.DecodePartition(row, col, sub, branch.Children[0]);
                    this.DecodePartition(row, col + half, sub, branch.Children[1]);
                    this.DecodePartition(row + half, col, sub, branch.Children[2]);
                    this.DecodePartition(row + half, col + half, sub, branch.Children[3]);
                }

                break;
            case Av1Partition.Horizontal:
                this.DecodeEdgeBlock(row, col, horz, node.H[0]);
                if (hasRows)
                {
                    this.DecodeEdgeBlock(row + half, col, horz, node.H[1]);
                }

                break;
            case Av1Partition.Vertical:
                this.DecodeEdgeBlock(row, col, vert, node.V[0]);
                if (hasCols)
                {
                    this.DecodeEdgeBlock(row, col + half, vert, node.V[1]);
                }

                break;
            case Av1Partition.HorizontalA: // split top, wide bottom (dav1d T_TOP_SPLIT).
                this.DecodeEdgeBlock(row, col, sub, Av1IntraEdgeFlags.AllTrAndBl);
                this.DecodeEdgeBlock(row, col + half, sub, node.V[1]);
                this.DecodeEdgeBlock(row + half, col, horz, node.H[1]);
                break;
            case Av1Partition.HorizontalB: // wide top, split bottom (dav1d T_BOTTOM_SPLIT).
                this.DecodeEdgeBlock(row, col, horz, node.H[0]);
                this.DecodeEdgeBlock(row + half, col, sub, node.V[0]);
                this.DecodeEdgeBlock(row + half, col + half, sub, 0);
                break;
            case Av1Partition.VerticalA: // split left, tall right (dav1d T_LEFT_SPLIT).
                this.DecodeEdgeBlock(row, col, sub, Av1IntraEdgeFlags.AllTrAndBl);
                this.DecodeEdgeBlock(row + half, col, sub, node.H[1]);
                this.DecodeEdgeBlock(row, col + half, vert, node.V[1]);
                break;
            case Av1Partition.VerticalB: // tall left, split right (dav1d T_RIGHT_SPLIT).
                this.DecodeEdgeBlock(row, col, vert, node.V[0]);
                this.DecodeEdgeBlock(row, col + half, sub, node.H[0]);
                this.DecodeEdgeBlock(row + half, col + half, sub, 0);
                break;
            case Av1Partition.Horizontal4:
            {
                Av1BlockSize h4 = Av1BlockSizeExtensions.FromDimensions(side, quarter);
                Av1EdgeBranch h4Branch = (Av1EdgeBranch)node;
                ReadOnlySpan<byte> h4Flags = [node.H[0], h4Branch.H4, Av1IntraEdgeFlags.AllLeftHasBottom, node.H[1]];
                for (int i = 0; i < 4; i++)
                {
                    int r = row + (i * quarter);
                    if (r < this.frameHeader.ModeInfoRows)
                    {
                        this.DecodeEdgeBlock(r, col, h4, h4Flags[i]);
                    }
                }

                break;
            }

            case Av1Partition.Vertical4:
            {
                Av1BlockSize v4 = Av1BlockSizeExtensions.FromDimensions(quarter, side);
                Av1EdgeBranch v4Branch = (Av1EdgeBranch)node;
                ReadOnlySpan<byte> v4Flags = [node.V[0], v4Branch.V4, Av1IntraEdgeFlags.AllTopHasRight, node.V[1]];
                for (int i = 0; i < 4; i++)
                {
                    int c = col + (i * quarter);
                    if (c < this.frameHeader.ModeInfoColumns)
                    {
                        this.DecodeEdgeBlock(row, c, v4, v4Flags[i]);
                    }
                }

                break;
            }

            default:
                throw new NotSupportedException($"Partition type {partition} is not supported yet.");
        }

        // Record the partition neighbour context over the square region (dav1d set_ctx); for a non-8x8
        // SPLIT the recursion has already filled it.
        if (partition != Av1Partition.Split || bsize == Av1BlockSize.Block8x8)
        {
            Fill(this.abovePartition, col >> 1, half, bsize.AbovePartitionContext(partition));
            Fill(this.leftPartition, row >> 1, half, bsize.LeftPartitionContext(partition));
        }
    }

    private Av1Partition ReadPartition(int row, int col, Av1BlockSize bsize, bool hasRows, bool hasCols)
    {
        int blockLevel = bsize.GetPartitionLevel();
        int shift = 4 - blockLevel;
        int above = (this.abovePartition[col >> 1] >> shift) & 1;
        int left = (this.leftPartition[row >> 1] >> shift) & 1;
        ushort[] cdf = this.modeCdf.Partition[blockLevel][above + (left << 1)];

        if (hasRows && hasCols)
        {
            return (Av1Partition)this.decoder.ReadSymbol(cdf);
        }

        if (!hasRows && !hasCols)
        {
            // Neither half fits: an implicit split, no bits are coded.
            return Av1Partition.Split;
        }

        // Only one direction fits (a frame edge): a single non-adaptive boolean chooses between a split
        // and the sole partition whose blocks lie inside the frame, decoded with a probability gathered
        // from the full partition CDF (dav1d gather_top/left_partition_prob). The edge cases only occur
        // above the 8x8 level, where the frame's even 4x4 dimensions guarantee both halves fit.
        if (!hasRows)
        {
            uint probability = GatherTopSplitProbability(cdf, blockLevel);
            return this.decoder.ReadBool(probability) != 0 ? Av1Partition.Split : Av1Partition.Horizontal;
        }

        uint probabilityLeft = GatherLeftSplitProbability(cdf, blockLevel);
        return this.decoder.ReadBool(probabilityLeft) != 0 ? Av1Partition.Split : Av1Partition.Vertical;
    }

    // dav1d gather_top_partition_prob: the summed probability of every partition with a vertical split
    // boundary (V, SPLIT, T_TOP, T_LEFT, T_RIGHT, V4), read from the inverse-CDF boundaries.
    private static uint GatherTopSplitProbability(ushort[] cdf, int blockLevel)
    {
        uint result = (uint)(cdf[(int)Av1Partition.Vertical - 1] - cdf[(int)Av1Partition.HorizontalA]);
        result += cdf[(int)Av1Partition.VerticalA - 1];
        if (blockLevel != 0)
        {
            result += (uint)(cdf[(int)Av1Partition.Vertical4 - 1] - cdf[(int)Av1Partition.VerticalB]);
        }

        return result;
    }

    // dav1d gather_left_partition_prob: the summed probability of every partition with a horizontal
    // split boundary (H, SPLIT, T_TOP, T_BOTTOM, T_LEFT, H4).
    private static uint GatherLeftSplitProbability(ushort[] cdf, int blockLevel)
    {
        uint result = (uint)(cdf[(int)Av1Partition.Horizontal - 1] - cdf[(int)Av1Partition.Horizontal]);
        result += (uint)(cdf[(int)Av1Partition.Split - 1] - cdf[(int)Av1Partition.VerticalA]);
        if (blockLevel != 0)
        {
            result += (uint)(cdf[(int)Av1Partition.Horizontal4 - 1] - cdf[(int)Av1Partition.Horizontal4]);
        }

        return result;
    }

    /// <summary>
    /// Reads the block skip flag and performs the prediction-independent block bookkeeping shared by
    /// the intra and inter paths: reading the CDEF index once per 64x64 region and recording the
    /// non-skip status of every 4x4 unit the block covers.
    /// </summary>
    /// <param name="row">The block's top 4x4 row.</param>
    /// <param name="col">The block's left 4x4 column.</param>
    /// <param name="width4">The block width in 4x4 units.</param>
    /// <param name="height4">The block height in 4x4 units.</param>
    /// <returns>The decoded skip flag (0 or 1).</returns>
    private protected int ReadSkipFlag(int row, int col, int width4, int height4, int? forcedSkip = null)
    {
        int skip;
        if (forcedSkip is { } forced)
        {
            skip = forced;
        }
        else
        {
            int skipContext = this.aboveSkip[col] + this.leftSkip[row];
            skip = this.decoder.ReadSymbol(this.modeCdf.Skip[skipContext]);
        }

        // Post-skip segment id: predicted from the neighbouring map cells; a skipped block takes the
        // prediction without coding a symbol (the pre-skip position is rejected at construction).
        if (this.segmentation.Enabled)
        {
            this.ReadSegmentId(row, col, width4, height4, skip);
        }
        else
        {
            this.currentSegmentId = 0;
        }

        // cdef index: read once per 64x64 region at its first non-skip block, then propagated to every
        // 64x64 cell the block covers (dav1d reads cdef_idx per 64x64, even within a 128x128 superblock).
        if (skip == 0 && this.cdefIndices[((row >> 4) * this.cdefColumns64) + (col >> 4)] == -1)
        {
            int cdefIndex = this.frameHeader.CdefBits > 0 ? (int)this.decoder.ReadLiteral(this.frameHeader.CdefBits) : 0;
            for (int cr = row >> 4; cr <= (row + height4 - 1) >> 4; cr++)
            {
                for (int cc = col >> 4; cc <= (col + width4 - 1) >> 4; cc++)
                {
                    this.cdefIndices[(cr * this.cdefColumns64) + cc] = cdefIndex;
                }
            }
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

        this.ReadSuperblockDeltas(row, col, width4, height4, skip);
        this.UpdateBlockQIndex();
        return skip;
    }

    // Reads the block's segment id from the spatial prediction (dav1d's post-skip segment_id branch)
    // and splats it into the segment map.
    private void ReadSegmentId(int row, int col, int width4, int height4, int skip)
    {
        (int predSegId, int segCtx) = this.PredictSegmentId(row, col);
        int segId;
        if (skip != 0)
        {
            segId = predSegId;
        }
        else
        {
            int diff = this.decoder.ReadSymbol(this.modeCdf.SegId[segCtx]);
            segId = NegDeinterleave(diff, predSegId, this.segmentation.LastActiveSegmentId + 1);
            if (segId > this.segmentation.LastActiveSegmentId || segId >= ObuSegmentationParams.SegmentCount)
            {
                segId = 0;
            }
        }

        this.currentSegmentId = segId;
        for (int dy = 0; dy < height4 && row + dy < this.miRows; dy++)
        {
            int rowBase = (row + dy) * this.miColumns;
            for (int dx = 0; dx < width4 && col + dx < this.miColumns; dx++)
            {
                this.segmentMap[rowBase + col + dx] = (byte)segId;
            }
        }
    }

    // The spatial segment-id prediction and its context (dav1d get_cur_frame_segid): from the left,
    // above and above-left map cells, with tile-relative availability.
    private (int PredSegId, int Context) PredictSegmentId(int row, int col)
    {
        bool haveTop = row > this.tileBounds.RowStart;
        bool haveLeft = col > this.tileBounds.ColumnStart;
        int position = (row * this.miColumns) + col;
        if (haveLeft && haveTop)
        {
            int l = this.segmentMap[position - 1];
            int a = this.segmentMap[position - this.miColumns];
            int al = this.segmentMap[position - this.miColumns - 1];
            int ctx = l == a && al == l ? 2 : l == a || al == l || a == al ? 1 : 0;
            return (a == al ? a : l, ctx);
        }

        return (haveLeft ? this.segmentMap[position - 1] : haveTop ? this.segmentMap[position - this.miColumns] : 0, 0);
    }

    // dav1d neg_deinterleave: recovers the segment id from the coded difference and the prediction.
    private static int NegDeinterleave(int diff, int reference, int max)
    {
        if (reference == 0)
        {
            return diff;
        }

        if (reference >= max - 1)
        {
            return max - diff - 1;
        }

        if (2 * reference < max)
        {
            if (diff <= 2 * reference)
            {
                return (diff & 1) != 0 ? reference + ((diff + 1) >> 1) : reference - (diff >> 1);
            }

            return diff;
        }

        if (diff <= 2 * (max - reference - 1))
        {
            return (diff & 1) != 0 ? reference + ((diff + 1) >> 1) : reference - (diff >> 1);
        }

        return max - (diff + 1);
    }

    // Reads the per-superblock quantizer and loop-filter deltas at the first block of each superblock
    // (dav1d's delta-q/lf block in decode_b) and accumulates them into the running tile state.
    private void ReadSuperblockDeltas(int row, int col, int width4, int height4, int skip)
    {
        int sbMask = this.sequenceHeader.Use128x128Superblock ? 31 : 15;
        if (((col | row) & sbMask) != 0)
        {
            return;
        }

        int sb4 = sbMask + 1;
        bool haveDeltaQ = this.frameHeader.DeltaQPresent && (width4 != sb4 || height4 != sb4 || skip == 0);
        if (!haveDeltaQ)
        {
            return;
        }

        int deltaQ = this.ReadDeltaToken(this.modeCdf.DeltaQ, this.frameHeader.DeltaQResolution);
        this.currentQIndex = Math.Clamp(this.currentQIndex + deltaQ, 1, 255);


        if (this.frameHeader.DeltaLfPresent)
        {
            bool multi = this.frameHeader.DeltaLfMulti;
            int count = multi ? (this.sequenceHeader.NumPlanes > 1 ? 4 : 2) : 1;
            for (int i = 0; i < count; i++)
            {
                int deltaLf = this.ReadDeltaToken(this.modeCdf.DeltaLf[i + (multi ? 1 : 0)], this.frameHeader.DeltaLfResolution);
                this.currentDeltaLf[i] = Math.Clamp(this.currentDeltaLf[i] + deltaLf, -63, 63);

            }
        }
    }

    // Reads one delta token: a four-way symbol whose last value opens an escape with a coded bit
    // length, then an equi-probable sign, scaled by the frame's delta resolution.
    private int ReadDeltaToken(ushort[] cdf, int resolutionLog2)
    {
        int delta = this.decoder.ReadSymbol(cdf);
        if (delta == 3)
        {
            int bits = 1 + (int)this.decoder.ReadLiteral(3);
            delta = (int)this.decoder.ReadLiteral(bits) + 1 + (1 << bits);
        }

        if (delta != 0)
        {
            if (this.decoder.ReadBool() != 0)
            {
                delta = -delta;
            }

            delta *= 1 << resolutionLog2;
        }

        return delta;
    }

    // Derives the current block's effective per-plane DC/AC quantizer indices from the running
    // superblock quantizer, the block's segment quantizer delta and the frame's per-plane deltas
    // (dav1d init_quant_tables for the active segment).
    private void UpdateBlockQIndex()
    {
        int baseQ = this.currentQIndex;
        if (this.segmentation.Enabled)
        {
            baseQ = Math.Clamp(baseQ + this.segmentation.DeltaQ[this.currentSegmentId], 0, 255);
        }

        this.blockDcQIndex[0] = Math.Clamp(baseQ + this.frameHeader.DeltaQYDc, 0, 255);
        this.blockAcQIndex[0] = baseQ;
        this.blockDcQIndex[1] = Math.Clamp(baseQ + this.frameHeader.DeltaQUDc, 0, 255);
        this.blockAcQIndex[1] = Math.Clamp(baseQ + this.frameHeader.DeltaQUAc, 0, 255);
        this.blockDcQIndex[2] = Math.Clamp(baseQ + this.frameHeader.DeltaQVDc, 0, 255);
        this.blockAcQIndex[2] = Math.Clamp(baseQ + this.frameHeader.DeltaQVAc, 0, 255);
    }

    private protected virtual void DecodeBlock(int row, int col, Av1BlockSize bsize, bool topRightAvailable)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();

        // skip flag, plus the shared cdef-index and non-skip recording.
        int skip = this.ReadSkipFlag(row, col, width4, height4);

        // luma intra mode (key-frame path: coded with the above/left neighbour-mode context).
        int aboveModeContext = IntraModeContext[this.aboveMode[col]];
        int leftModeContext = IntraModeContext[this.leftMode[row]];
        int yMode = this.decoder.ReadSymbol(this.modeCdf.KeyFrameYMode[aboveModeContext][leftModeContext]);


        this.DecodeIntraBlockBody(row, col, bsize, skip, yMode);
    }

    // Decodes the body of an intra block once its luma mode is known: the luma angle delta, chroma mode,
    // filter-intra, transform size, the luma and chroma residual + reconstruction, and the neighbour-context
    // updates. Shared by the key-frame path and the intra-block-in-inter-frame path (which read the luma
    // mode differently).
    private protected void DecodeIntraBlockBody(int row, int col, Av1BlockSize bsize, int skip, int yMode)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();

        EnsureSupportedMode(yMode);
        int yAngleDelta = this.ReadAngleDelta(yMode, bsize);


        // Chroma is decoded once per chroma unit; sub-sampled sub-8x8 luma blocks share it (dav1d
        // has_chroma).
        bool hasChroma = this.sequenceHeader.NumPlanes > 1 &&
                         (width4 > this.subsamplingX || (col & 1) != 0) &&
                         (height4 > this.subsamplingY || (row & 1) != 0);

        // chroma intra mode. For a lossless block CfL is only allowed when the chroma unit is a
        // single 4x4 block (dav1d: cbw4 == 1 && cbh4 == 1); otherwise the usual up-to-32x32 rule.
        bool lossless = this.CurrentBlockLossless;
        bool cflAllowed = lossless
            ? ((width4 + this.subsamplingX) >> this.subsamplingX) == 1 && ((height4 + this.subsamplingY) >> this.subsamplingY) == 1
            : bsize.GetWidth4() <= 8 && bsize.GetHeight4() <= 8;
        int uvMode = 0;
        int uvAngleDelta = 0;
        int cflAlphaU = 0;
        int cflAlphaV = 0;
        if (hasChroma)
        {
            uvMode = this.decoder.ReadSymbol(this.modeCdf.UvMode[cflAllowed ? 1 : 0][yMode]);

            if (uvMode == 13)
            {
                this.ReadCflAlphas(out cflAlphaU, out cflAlphaV);
            }
            else
            {
                EnsureSupportedMode(uvMode);
                uvAngleDelta = this.ReadAngleDelta(uvMode, bsize);
            }
        }

        // palette_mode_info: DC blocks of 8x8..64x64 may paint from a coded palette instead of an
        // intra prediction (dav1d's read_pal_plane/read_pal_uv).
        int paletteSizeY = 0;
        int paletteSizeUv = 0;
        ushort[]? paletteY = null;
        ushort[]? paletteU = null;
        ushort[]? paletteV = null;
        if (this.frameHeader.AllowScreenContentTools
            && Math.Max(width4, height4) <= 16 && width4 + height4 >= 4)
        {
            int sizeContext = bsize.GetWidthLog2() + bsize.GetHeightLog2() - 2;
            if (yMode == 0)
            {
                int palContext = (this.abovePalSize[col] > 0 ? 1 : 0) + (this.leftPalSize[row] > 0 ? 1 : 0);
                if (this.decoder.ReadSymbol(this.modeCdf.PaletteY[sizeContext][palContext]) != 0)
                {
                    paletteY = new ushort[8];
                    paletteSizeY = this.ReadPalettePlane(paletteY, 0, sizeContext, row, col);
                }
            }

            if (hasChroma && uvMode == 0)
            {
                int palContext = paletteSizeY > 0 ? 1 : 0;
                if (this.decoder.ReadSymbol(this.modeCdf.PaletteUv[palContext]) != 0)
                {
                    paletteU = new ushort[8];
                    paletteV = new ushort[8];
                    paletteSizeUv = this.ReadPalettePlane(paletteU, 1, sizeContext, row, col);
                    this.ReadPaletteV(paletteV, paletteSizeUv);
                }
            }
        }

        // filter_intra: coded for DC luma blocks up to 32x32 when enabled (never for palette blocks).
        int filterIntraMode = -1;
        if (this.sequenceHeader.EnableFilterIntra && paletteSizeY == 0
            && Math.Max(bsize.GetWidthLog2(), bsize.GetHeightLog2()) <= 3 && yMode == 0)
        {
            int useFilterIntra = this.decoder.ReadSymbol(this.modeCdf.UseFilterIntra[(int)bsize]);
            if (useFilterIntra != 0)
            {
                filterIntraMode = this.decoder.ReadSymbol(this.modeCdf.FilterIntraMode);
            }
        }

        // Palette index maps (read after filter_intra, before the transform size), then paint the
        // block; the transform loop uses the painted samples as the prediction.
        if (paletteSizeY > 0)
        {
            int w4 = Math.Min(width4, this.miColumns - col);
            int h4 = Math.Min(height4, this.miRows - row);
            byte[] indices = this.ReadPaletteIndices(paletteSizeY, 0, w4, h4, width4, height4);
            PaintPalette(this.luma, col * 4, row * 4, width4 * 4, height4 * 4, indices, paletteY!);
        }

        if (paletteSizeUv > 0)
        {
            int w4 = Math.Min(width4, this.miColumns - col);
            int h4 = Math.Min(height4, this.miRows - row);
            int cw4 = (w4 + this.subsamplingX) >> this.subsamplingX;
            int ch4 = (h4 + this.subsamplingY) >> this.subsamplingY;
            int cbw4 = (width4 + this.subsamplingX) >> this.subsamplingX;
            int cbh4 = (height4 + this.subsamplingY) >> this.subsamplingY;
            byte[] indices = this.ReadPaletteIndices(paletteSizeUv, 1, cw4, ch4, cbw4, cbh4);
            int cx = (col >> this.subsamplingX) * 4;
            int cy = (row >> this.subsamplingY) * 4;
            PaintPalette(this.chromaU, cx, cy, cbw4 * 4, cbh4 * 4, indices, paletteU!);
            PaintPalette(this.chromaV, cx, cy, cbw4 * 4, cbh4 * 4, indices, paletteV!);
        }

        this.currentBlockPaletteY = paletteSizeY > 0;
        this.currentBlockPaletteUv = paletteSizeUv > 0;

        // transform size (TX_MODE_LARGEST forces the largest; TX_MODE_SELECT codes a depth).
        Av1TransformSize lumaTx = this.ReadTransformSize(row, col, bsize);

        // Smooth-neighbour edge-filter flags (dav1d is_sm): read the not-yet-overwritten neighbour modes.
        this.lumaEdgeSmooth = IsSmoothMode(this.aboveMode[col]) || IsSmoothMode(this.leftMode[row]);
        this.chromaEdgeSmooth = IsSmoothMode(this.aboveUvMode[col >> this.subsamplingX]) || IsSmoothMode(this.leftUvMode[row >> this.subsamplingY]);

        // luma transform-block loop.
        // Blocks wider or taller than 64 pixels are processed in 64x64 chunks, each decoding its luma
        // transforms and then its chroma transforms (dav1d recon_b_intra's init_x/init_y loops); the
        // interleave matters both for the coefficient neighbour contexts and for CfL, which reads
        // reconstructed luma per chunk.
        Av1TransformSize chromaTxSize = lossless ? Av1TransformSize.Size4x4 : bsize.GetMaxChromaTransformSize(this.sequenceHeader);
        int chromaRow4 = row >> this.subsamplingY;
        int chromaCol4 = col >> this.subsamplingX;
        int uvModeForTxtp = uvMode;
        Av1TransformType ChromaTxtp(Av1TransformSize t, int tc, int tr)
            => lossless ? Av1TransformType.WhtWht : Av1ChromaTransformType.FromIntra(t, uvModeForTxtp);
        int chromaW4 = (width4 + this.subsamplingX) >> this.subsamplingX;
        int chromaH4 = (height4 + this.subsamplingY) >> this.subsamplingY;
        for (int initY = 0; initY < height4; initY += 16)
        {
            for (int initX = 0; initX < width4; initX += 16)
            {
                // Chunk-level above-right/left-bottom availability (dav1d sb_has_tr / sb_has_bl):
                // interior chunk edges are available; the block's outer edges follow the edge tree.
                bool sbHasTr = initX + 16 < width4 || (initY == 0 && (this.blockIntraEdgeFlags & Av1IntraEdgeFlags.I444TopHasRight) != 0);
                bool sbHasBl = initX == 0 && (initY + 16 < height4 || (this.blockIntraEdgeFlags & Av1IntraEdgeFlags.I444LeftHasBottom) != 0);
                this.DecodePlane(
                    this.luma, this.lumaLevels, 0, row, col, bsize, lumaTx, yMode, yAngleDelta, filterIntraMode, 0,
                    interTransformTypeReader: lossless ? static _ => Av1TransformType.WhtWht : null,
                    chunkX4: initX, chunkY4: initY, chunkEndX4: initX + 16, chunkEndY4: initY + 16,
                    sbHasTopRight: sbHasTr, sbHasBottomLeft: sbHasBl);

                if (hasChroma)
                {
                    int cx0 = initX >> this.subsamplingX;
                    int cy0 = initY >> this.subsamplingY;
                    int cx1 = (initX + 16) >> this.subsamplingX;
                    int cy1 = (initY + 16) >> this.subsamplingY;

                    // The chroma flags use the layout-specific edge-tree bits (dav1d reads
                    // EDGE_I420_* >> (layout - 1), selecting the I420, I422 or I444 bit).
                    int layoutShift = 2 - this.subsamplingX - this.subsamplingY;
                    bool uvSbHasTr = cx1 < chromaW4 || (initY == 0 && (this.blockIntraEdgeFlags & (Av1IntraEdgeFlags.I420TopHasRight >> layoutShift)) != 0);
                    bool uvSbHasBl = initX == 0 && (cy1 < chromaH4 || (this.blockIntraEdgeFlags & (Av1IntraEdgeFlags.I420LeftHasBottom >> layoutShift)) != 0);
                    this.DecodePlane(this.chromaU, this.chromaULevels, 1, chromaRow4, chromaCol4, bsize, chromaTxSize, uvMode, uvAngleDelta, -1, cflAlphaU, chromaTransformTypeProvider: ChromaTxtp, chunkX4: cx0, chunkY4: cy0, chunkEndX4: cx1, chunkEndY4: cy1, sbHasTopRight: uvSbHasTr, sbHasBottomLeft: uvSbHasBl);
                    this.DecodePlane(this.chromaV, this.chromaVLevels, 2, chromaRow4, chromaCol4, bsize, chromaTxSize, uvMode, uvAngleDelta, -1, cflAlphaV, chromaTransformTypeProvider: ChromaTxtp, chunkX4: cx0, chunkY4: cy0, chunkEndX4: cx1, chunkEndY4: cy1, sbHasTopRight: uvSbHasTr, sbHasBottomLeft: uvSbHasBl);
                }
            }
        }

        // record block-level neighbour contexts.
        Fill(this.aboveSkip, col, width4, (byte)skip);
        Fill(this.leftSkip, row, height4, (byte)skip);
        Fill(this.aboveMode, col, width4, (byte)yMode);
        Fill(this.leftMode, row, height4, (byte)yMode);
        if (hasChroma)
        {
            // The chroma-mode neighbour context lives at chroma granularity (dav1d writes
            // uvmode[cbx4] over cbw4 cells): two sub-sampled luma cells share one entry.
            Fill(this.aboveUvMode, col >> this.subsamplingX, (width4 + this.subsamplingX) >> this.subsamplingX, (byte)uvMode);
            Fill(this.leftUvMode, row >> this.subsamplingY, (height4 + this.subsamplingY) >> this.subsamplingY, (byte)uvMode);
        }
        Fill(this.aboveTx, col, width4, (sbyte)(lumaTx.GetWidthLog2() - 2));
        Fill(this.leftTx, row, height4, (sbyte)(lumaTx.GetHeightLog2() - 2));
        this.RecordPaletteContexts(row, col, width4, height4, hasChroma, paletteSizeY, paletteSizeUv, paletteY, paletteU, paletteV);
        this.currentBlockPaletteY = false;
        this.currentBlockPaletteUv = false;

        this.RecordLoopFilterLevels(row, col, bsize, hasChroma, reference: 0, modeIndex: 0);
        this.OnIntraBlockDecoded(row, col, bsize, skip, yMode, lumaTx);
    }


    // Reads one plane's palette (dav1d read_pal_plane): the size, the reuse flags against the sorted
    // cache of the neighbour palettes, and the ascending delta-coded new entries, merged sorted.
    private int ReadPalettePlane(ushort[] palette, int plane, int sizeContext, int row, int col)
    {
        int paletteSize = this.decoder.ReadSymbol(this.modeCdf.PaletteSize[plane][sizeContext]) + 2;

        int leftCount = plane == 0 ? this.leftPalSize[row] : this.leftPalUvSize[row];
        // The above palette is not reused across 64-pixel superblock row boundaries.
        int aboveCount = (row & 15) != 0 ? (plane == 0 ? this.abovePalSize[col] : this.abovePalUvSize[col]) : 0;
        int leftBase = (row * 24) + (plane * 8);
        int aboveBase = (col * 24) + (plane * 8);

        // Merge the two sorted neighbour palettes into a deduplicated cache.
        Span<ushort> cache = stackalloc ushort[16];
        int cacheCount = 0;
        int l = 0;
        int a = 0;
        while (l < leftCount && a < aboveCount)
        {
            ushort lv = this.leftPalette[leftBase + l];
            ushort av = this.abovePalette[aboveBase + a];
            if (lv < av)
            {
                if (cacheCount == 0 || cache[cacheCount - 1] != lv)
                {
                    cache[cacheCount++] = lv;
                }

                l++;
            }
            else
            {
                if (av == lv)
                {
                    l++;
                }

                if (cacheCount == 0 || cache[cacheCount - 1] != av)
                {
                    cache[cacheCount++] = av;
                }

                a++;
            }
        }

        for (; l < leftCount; l++)
        {
            ushort lv = this.leftPalette[leftBase + l];
            if (cacheCount == 0 || cache[cacheCount - 1] != lv)
            {
                cache[cacheCount++] = lv;
            }
        }

        for (; a < aboveCount; a++)
        {
            ushort av = this.abovePalette[aboveBase + a];
            if (cacheCount == 0 || cache[cacheCount - 1] != av)
            {
                cache[cacheCount++] = av;
            }
        }

        // One reuse flag per cache entry, then the new entries: a raw first value and ascending
        // deltas (strictly ascending for luma/U via the +1 bias), clamped at the sample maximum.
        Span<ushort> usedCache = stackalloc ushort[8];
        int usedCount = 0;
        for (int n = 0; n < cacheCount && usedCount < paletteSize; n++)
        {
            if (this.decoder.ReadBool() != 0)
            {
                usedCache[usedCount++] = cache[n];
            }
        }

        int i = usedCount;
        if (i < paletteSize)
        {
            int bitDepth = this.sequenceHeader.BitDepth;
            Span<ushort> fresh = stackalloc ushort[8];
            int freshBase = i;
            int prev = fresh[i++] = (ushort)this.decoder.ReadLiteral(bitDepth);
            if (i < paletteSize)
            {
                int bits = bitDepth - 3 + (int)this.decoder.ReadLiteral(2);
                int max = (1 << bitDepth) - 1;
                int bias = plane == 0 ? 1 : 0;
                while (i < paletteSize)
                {
                    int delta = (int)this.decoder.ReadLiteral(bits);
                    prev = Math.Min(prev + delta + bias, max);
                    fresh[i++] = (ushort)prev;
                    if (prev + bias >= max)
                    {
                        for (; i < paletteSize; i++)
                        {
                            fresh[i] = (ushort)max;
                        }

                        break;
                    }

                    bits = Math.Min(bits, 1 + Log2(max - prev - bias));
                }
            }

            // Merge the used cache entries and the new entries in ascending order.
            int n2 = 0;
            int m2 = freshBase;
            for (i = 0; i < paletteSize; i++)
            {
                if (n2 < usedCount && (m2 >= paletteSize || usedCache[n2] <= fresh[m2]))
                {
                    palette[i] = usedCache[n2++];
                }
                else
                {
                    palette[i] = fresh[m2++];
                }
            }
        }
        else
        {
            usedCache[..usedCount].CopyTo(palette);
        }

        return paletteSize;
    }

    // Reads the V-plane palette (dav1d read_pal_uv's second half): either signed-delta coded with a
    // wrap-around, or raw sample values, chosen by an equiprobable bit.
    private void ReadPaletteV(ushort[] palette, int paletteSize)
    {
        int bitDepth = this.sequenceHeader.BitDepth;
        if (this.decoder.ReadBool() != 0)
        {
            int bits = bitDepth - 4 + (int)this.decoder.ReadLiteral(2);
            int prev = (int)this.decoder.ReadLiteral(bitDepth);
            palette[0] = (ushort)prev;
            int max = (1 << bitDepth) - 1;
            for (int i = 1; i < paletteSize; i++)
            {
                int delta = (int)this.decoder.ReadLiteral(bits);
                if (delta != 0 && this.decoder.ReadBool() != 0)
                {
                    delta = -delta;
                }

                prev = (prev + delta) & max;
                palette[i] = (ushort)prev;
            }
        }
        else
        {
            for (int i = 0; i < paletteSize; i++)
            {
                palette[i] = (ushort)this.decoder.ReadLiteral(bitDepth);
            }
        }
    }

    // Reads a block's palette index map (dav1d read_pal_indices): the first sample is near-uniform,
    // then anti-diagonal wavefronts code each sample against its top/left/top-left neighbour pattern.
    // The map covers the full block (bw4*4 x bh4*4); samples beyond the frame-clipped w4*4/h4*4 area
    // replicate the last coded column/row (dav1d pal_idx_finish).
    private byte[] ReadPaletteIndices(int paletteSize, int plane, int w4, int h4, int bw4, int bh4)
    {
        int stride = bw4 * 4;
        byte[] indices = new byte[stride * (bh4 * 4)];
        indices[0] = (byte)this.decoder.ReadUniform(paletteSize);
        ushort[][] colorMapCdf = this.modeCdf.PaletteColorMap[plane][paletteSize - 2];
        int w = w4 * 4;
        int h = h4 * 4;
        Span<byte> order = stackalloc byte[8];
        for (int i = 1; i < (4 * (w4 + h4)) - 1; i++)
        {
            int first = Math.Min(i, w - 1);
            int last = Math.Max(0, i - h + 1);
            for (int j = first; j >= last; j--)
            {
                int y = i - j;
                int pos = (y * stride) + j;
                bool haveLeft = j > 0;
                bool haveTop = y > 0;
                int context;
                int orderCount = 0;
                int mask = 0;
                void Add(int v, Span<byte> ord)
                {
                    ord[orderCount++] = (byte)v;
                    mask |= 1 << v;
                }

                if (!haveLeft)
                {
                    context = 0;
                    Add(indices[pos - stride], order);
                }
                else if (!haveTop)
                {
                    context = 0;
                    Add(indices[pos - 1], order);
                }
                else
                {
                    int left = indices[pos - 1];
                    int top = indices[pos - stride];
                    int topLeft = indices[pos - stride - 1];
                    if (top == left && top == topLeft)
                    {
                        context = 4;
                        Add(top, order);
                    }
                    else if (top == left)
                    {
                        context = 3;
                        Add(top, order);
                        Add(topLeft, order);
                    }
                    else if (top == topLeft || left == topLeft)
                    {
                        context = 2;
                        Add(topLeft, order);
                        Add(top == topLeft ? left : top, order);
                    }
                    else
                    {
                        context = 1;
                        Add(Math.Min(top, left), order);
                        Add(Math.Max(top, left), order);
                        Add(topLeft, order);
                    }
                }

                for (int bit = 0; bit < 8; bit++)
                {
                    if ((mask & (1 << bit)) == 0)
                    {
                        order[orderCount++] = (byte)bit;
                    }
                }

                int colorIndex = this.decoder.ReadSymbol(colorMapCdf[context]);
                indices[pos] = order[colorIndex];
            }
        }

        // Replicate the coded area over the block overhang.
        for (int y = 0; y < h; y++)
        {
            for (int x = w; x < stride; x++)
            {
                indices[(y * stride) + x] = indices[(y * stride) + w - 1];
            }
        }

        for (int y = h; y < bh4 * 4; y++)
        {
            Array.Copy(indices, (h - 1) * stride, indices, y * stride, stride);
        }

        return indices;
    }

    // Paints a palette block into the plane (dav1d pal_pred), clamped at the padded plane bounds.
    private static void PaintPalette(Av1Plane plane, int x0, int y0, int width, int height, byte[] indices, ushort[] palette)
    {
        for (int y = 0; y < height && y0 + y < plane.Height; y++)
        {
            for (int x = 0; x < width && x0 + x < plane.Width; x++)
            {
                plane[x0 + x, y0 + y] = palette[indices[(y * width) + x]];
            }
        }
    }

    // Records the palette sizes (zero for non-palette blocks) and colours into the neighbour state
    // (dav1d set_ctx pal_sz/pal_sz_uv plus copy_pal_block).
    private protected void RecordPaletteContexts(int row, int col, int width4, int height4, bool hasChroma, int paletteSizeY, int paletteSizeUv, ushort[]? paletteY = null, ushort[]? paletteU = null, ushort[]? paletteV = null)
    {
        Fill(this.abovePalSize, col, width4, (byte)paletteSizeY);
        Fill(this.leftPalSize, row, height4, (byte)paletteSizeY);
        Fill(this.abovePalUvSize, col, width4, (byte)(hasChroma ? paletteSizeUv : 0));
        Fill(this.leftPalUvSize, row, height4, (byte)(hasChroma ? paletteSizeUv : 0));

        for (int i = 0; i < width4 && col + i < this.miColumns; i++)
        {
            int cellBase = (col + i) * 24;
            paletteY?.CopyTo(this.abovePalette, cellBase);
            paletteU?.CopyTo(this.abovePalette, cellBase + 8);
            paletteV?.CopyTo(this.abovePalette, cellBase + 16);
        }

        for (int i = 0; i < height4 && row + i < this.miRows; i++)
        {
            int cellBase = (row + i) * 24;
            paletteY?.CopyTo(this.leftPalette, cellBase);
            paletteU?.CopyTo(this.leftPalette, cellBase + 8);
            paletteV?.CopyTo(this.leftPalette, cellBase + 16);
        }
    }

    private static int Log2(int value) => 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)Math.Max(value, 1));

    // Called after an intra block is fully decoded. The inter decoder overrides this to record the
    // inter-specific neighbour state (intra flag, transform-size context and motion-vector grid) that an
    // intra block contributes when it appears inside an inter frame.
    private protected virtual void OnIntraBlockDecoded(int row, int col, Av1BlockSize bsize, int skip, int yMode, Av1TransformSize lumaTx)
    {
    }

    // The interpolation filter of the most recently decoded inter block (dav1d's tl_4x4_filter). The
    // partition recursion saves it after the top-left leaf of an 8x8 split and restores it before the
    // bottom-right leaf, where the inter decoder's sub-8x8 chroma prediction reads it as the top-left
    // quadrant's filter. The intra-only base decoder has no such state.
    private protected virtual (int F0, int F1) TopLeft4x4Filter
    {
        get => default;
        set { }
    }

    // Records the intra-side neighbour contexts an INTER block contributes, matching dav1d's inter-branch
    // set_ctx: the tx_intra context takes the block-dimension categories (log2 of width/height in 4x4
    // units, NOT the transform size), the mode context takes the inter mode (numerically 0..3, never one
    // of the smooth intra modes, so zero is written) and the chroma-mode context resets to DC_PRED. These
    // feed the transform-depth context and the smooth-edge filter flags of later intra blocks.
    private protected void RecordInterBlockIntraContexts(int row, int col, Av1BlockSize bsize, bool hasChroma)
    {
        this.RecordPaletteContexts(row, col, bsize.GetWidth4(), bsize.GetHeight4(), hasChroma, 0, 0);
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();
        Fill(this.aboveTx, col, width4, (sbyte)bsize.GetWidthLog2());
        Fill(this.leftTx, row, height4, (sbyte)bsize.GetHeightLog2());
        Fill(this.aboveMode, col, width4, 0);
        Fill(this.leftMode, row, height4, 0);
        if (hasChroma)
        {
            Fill(this.aboveUvMode, col >> this.subsamplingX, (width4 + this.subsamplingX) >> this.subsamplingX, 0);
            Fill(this.leftUvMode, row >> this.subsamplingY, (height4 + this.subsamplingY) >> this.subsamplingY, 0);
        }
    }

    private Av1TransformSize ReadTransformSize(int row, int col, Av1BlockSize bsize)
    {
        // A lossless block (and TX_MODE_ONLY_4X4) always uses 4x4 transforms.
        if (this.CurrentBlockLossless || this.frameHeader.TxMode == 0)
        {
            return Av1TransformSize.Size4x4;
        }

        Av1TransformSize maxTx = bsize.GetMaxTransformSize();
        int lw = maxTx.GetWidthLog2() - 2;
        int lh = maxTx.GetHeightLog2() - 2;
        int maxField = Math.Max(lw, lh); // dav1d TxfmInfo.max.
        if (this.frameHeader.TxMode != 2 || maxField == 0)
        {
            return maxTx;
        }

        int aboveTxContext = this.aboveTx[col] >= lw ? 1 : 0;
        int leftTxContext = this.leftTx[row] >= lh ? 1 : 0;
        int txContext = leftTxContext + aboveTxContext;
        int depth = this.decoder.ReadSymbol(this.modeCdf.TransformDepth[maxField - 1][txContext]);

        Av1TransformSize tx = maxTx;
        for (int i = 0; i < depth; i++)
        {
            tx = SubTransformSize[(int)tx];
        }

        return tx;
    }

    private protected void DecodePlane(Av1Plane plane, LevelContext levels, int planeIndex, int miRow, int miCol, Av1BlockSize bsize, Av1TransformSize tx, int intraMode, int angleDelta, int filterIntraMode, int cflAlpha, bool skip = false, Func<Av1TransformSize, Av1TransformType>? interTransformTypeReader = null, Func<Av1TransformSize, int, int, Av1TransformType>? chromaTransformTypeProvider = null, int chunkX4 = 0, int chunkY4 = 0, int chunkEndX4 = int.MaxValue, int chunkEndY4 = int.MaxValue, bool sbHasTopRight = true, bool sbHasBottomLeft = true)
    {
        int blockWidth4 = planeIndex == 0 ? bsize.GetWidth4() : (bsize.GetWidth4() + this.subsamplingX) >> this.subsamplingX;
        int blockHeight4 = planeIndex == 0 ? bsize.GetHeight4() : (bsize.GetHeight4() + this.subsamplingY) >> this.subsamplingY;
        int txWidth4 = tx.GetWidth() >> 2;
        int txHeight4 = tx.GetHeight() >> 2;
        bool blockEqualsTx = blockWidth4 == txWidth4 && blockHeight4 == txHeight4;
        int endX4 = Math.Min(blockWidth4, chunkEndX4);
        int endY4 = Math.Min(blockHeight4, chunkEndY4);

        for (int dy = chunkY4; dy < endY4; dy += txHeight4)
        {
            for (int dx = chunkX4; dx < endX4; dx += txWidth4)
            {
                // Per-transform intra edge availability within the chunk (dav1d recon_b_intra).
                this.transformHasTopRight = !((dy > chunkY4 || !sbHasTopRight) && (dx + txWidth4 >= endX4));
                this.transformHasBottomLeft = !(dx > chunkX4 || (!sbHasBottomLeft && dy + txHeight4 >= endY4));
                bool interSkip = skip && this.CurrentBlockIsInter;
                this.DecodeTransformBlock(
                    plane, levels, planeIndex, miCol + dx, miRow + dy, bsize, tx, blockEqualsTx,
                    intraMode, angleDelta, filterIntraMode, cflAlpha, skip, interTransformTypeReader, chromaTransformTypeProvider,
                    edgeLeftAllowed: !interSkip || dx == 0, edgeTopAllowed: !interSkip || dy == 0);
            }
        }
    }

    // Decodes a single transform block at the given 4x4 position: records deblock edges, reads the
    // coefficient skip/level/dc-sign syntax (or treats it as all-zero for a skipped block), reconstructs
    // prediction + residual, and updates the decoded and coefficient-level neighbour contexts.
    private protected void DecodeTransformBlock(
        Av1Plane plane,
        LevelContext levels,
        int planeIndex,
        int txCol,
        int txRow,
        Av1BlockSize bsize,
        Av1TransformSize tx,
        bool blockEqualsTx,
        int intraMode,
        int angleDelta,
        int filterIntraMode,
        int cflAlpha,
        bool skip,
        Func<Av1TransformSize, Av1TransformType>? interTransformTypeReader,
        Func<Av1TransformSize, int, int, Av1TransformType>? chromaTransformTypeProvider = null,
        bool edgeLeftAllowed = true,
        bool edgeTopAllowed = true)
    {
        int txWidth4 = tx.GetWidth() >> 2;
        int txHeight4 = tx.GetHeight() >> 2;
        int x = txCol * 4;
        int y = txRow * 4;
        if (x >= plane.Width || y >= plane.Height)
        {
            return;
        }

        this.RecordTxEdges(planeIndex, txCol, txRow, txWidth4, txHeight4, edgeLeftAllowed, edgeTopAllowed);

        int skipContext = planeIndex == 0
            ? LumaCoefficientSkipContext(levels, txCol, txRow, txWidth4, txHeight4, blockEqualsTx)
            : this.ChromaCoefficientSkipContext(levels, txCol, txRow, txWidth4, txHeight4, bsize, tx);
        int dcSignContext = DcSignContext(levels, txCol, txRow, txWidth4, txHeight4);

        // Chroma never codes its transform type: it is derived (from the chroma mode for an intra block,
        // or the co-located luma type for an inter block). Luma reads it inside ReadCoefficients.
        Av1TransformType chromaType = Av1TransformType.DctDct;
        if (planeIndex != 0 && chromaTransformTypeProvider is not null)
        {
            chromaType = chromaTransformTypeProvider(tx, txCol, txRow);
        }

        int[] coefficientLevels = new int[Math.Min(tx.GetWidth(), 32) * Math.Min(tx.GetHeight(), 32)];
        int eob;
        Av1TransformType txType;
        if (skip)
        {
            // A skipped block codes no coefficients: the residual is zero and the all-zero flag is
            // implied rather than read.
            eob = Av1CoefficientReader.AllZero;
            txType = Av1TransformType.DctDct;
        }
        else
        {
            eob = Av1CoefficientReader.ReadCoefficients(
                this.decoder,
                this.coefficientCdf,
                tx,
                chromaType,
                planeIndex,
                skipContext,
                dcSignContext,
                coefficientLevels,
                planeIndex == 0 ? this.modeCdf : null,
                filterIntraMode >= 0 ? FilterModeToYMode[filterIntraMode] : intraMode,
                this.frameHeader.ReducedTxSet,
                out txType,
                planeIndex == 0 ? interTransformTypeReader : null);
        }

        if (planeIndex == 0)
        {
            this.RecordLumaTransformType(txCol, txRow, txWidth4, txHeight4, txType);
        }


        this.Reconstruct(plane, x, y, tx, txType, coefficientLevels, eob, intraMode, angleDelta, filterIntraMode, cflAlpha);

        if (planeIndex == 0)
        {
            for (int my = 0; my < txHeight4 && txRow + my < this.miRows; my++)
            {
                for (int mx = 0; mx < txWidth4 && txCol + mx < this.miColumns; mx++)
                {
                    this.lumaDecoded[((txRow + my) * this.miColumns) + txCol + mx] = true;
                }
            }
        }

        byte resContext = LevelContextByte(coefficientLevels, eob);
        levels.Write(txCol, txRow, txWidth4, txHeight4, resContext);
    }

    private protected void Reconstruct(Av1Plane plane, int x, int y, Av1TransformSize tx, Av1TransformType txType, int[] levels, int eob, int intraMode, int angleDelta, int filterIntraMode, int cflAlpha)
    {
        int width = tx.GetWidth();
        int height = tx.GetHeight();

        ushort[] prediction = new ushort[width * height];
        this.Predict(plane, x, y, width, height, intraMode, angleDelta, filterIntraMode, cflAlpha, prediction);

        int[] residual = new int[width * height];
        if (eob != Av1CoefficientReader.AllZero)
        {
            int codedHeight = Math.Min(height, 32);
            int planeIndex = ReferenceEquals(plane, this.luma) ? 0 : ReferenceEquals(plane, this.chromaU) ? 1 : 2;
            // A quantizer matrix weights the dequantizer per coefficient; identity-family transforms
            // never use one (dav1d gates on txtp < IDTX).
            ReadOnlySpan<byte> qmWeights = default;
            if (this.frameHeader.UsingQMatrix && txType < Av1TransformType.Identity)
            {
                int qmLevel = planeIndex == 0 ? this.frameHeader.QmY : planeIndex == 1 ? this.frameHeader.QmU : this.frameHeader.QmV;
                qmWeights = Av1QuantizerMatrices.Get(qmLevel, planeIndex, tx);
            }

            int[] coefficients = new int[width * height];
            for (int rc = 0; rc < levels.Length; rc++)
            {
                if (levels[rc] == 0)
                {
                    continue;
                }

                int rowInBlock = rc % codedHeight;
                int colInBlock = rc / codedHeight;
                int qindex = rc == 0 ? this.blockDcQIndex[planeIndex] : this.blockAcQIndex[planeIndex];
                coefficients[(rowInBlock * width) + colInBlock] =
                    Av1QuantizationLookup.Dequantize(levels[rc], rc == 0, qindex, this.sequenceHeader.BitDepth, tx, qmWeights.IsEmpty ? 0 : qmWeights[rc]);
            }

            Av1InverseTransform2d.Reconstruct(txType, tx, coefficients, residual, this.sequenceHeader.BitDepth);
        }

        int maxValue = (1 << this.sequenceHeader.BitDepth) - 1;
        for (int ry = 0; ry < height && y + ry < plane.Height; ry++)
        {
            for (int rx = 0; rx < width && x + rx < plane.Width; rx++)
            {
                plane[x + rx, y + ry] = (ushort)Math.Clamp(prediction[(ry * width) + rx] + residual[(ry * width) + rx], 0, maxValue);
            }
        }
    }

    private protected virtual void Predict(Av1Plane plane, int x, int y, int width, int height, int intraMode, int angleDelta, int filterIntraMode, int cflAlpha, ushort[] prediction)
    {
        // A palette block was painted before the transform loop; those samples are the prediction
        // (dav1d's skip_y_pred/skip_uv_pred).
        if (ReferenceEquals(plane, this.luma) ? this.currentBlockPaletteY : this.currentBlockPaletteUv)
        {
            for (int ry = 0; ry < height; ry++)
            {
                for (int rx = 0; rx < width; rx++)
                {
                    prediction[(ry * width) + rx] = x + rx < plane.Width && y + ry < plane.Height ? plane[x + rx, y + ry] : (ushort)0;
                }
            }

            return;
        }

        // Filter-intra (luma, DC blocks): predict each square unit from the prepared edges.
        if (filterIntraMode >= 0)
        {
            this.PrepareEdges(plane, x, y, width, height, out ushort[] fAbove, out ushort[] fLeft, out ushort fTopLeft);
            Av1FilterIntraPrediction.Predict(fAbove, fLeft, fTopLeft, width, height, filterIntraMode, prediction, this.sequenceHeader.BitDepth);
            return;
        }

        // Chroma-from-luma: DC chroma prediction plus the signed-alpha-scaled luma AC contribution.
        if (intraMode == 13)
        {
            int chromaDc = this.PredictDc(plane, x, y, width, height);
            int[] ac = new int[width * height];
            int lumaOffset = ((y << this.subsamplingY) * this.luma.Width) + (x << this.subsamplingX);
            Av1ChromaFromLuma.ComputeAc(this.luma.Samples, lumaOffset, this.luma.Width, width, height, this.subsamplingX, this.subsamplingY, ac);
            Av1ChromaFromLuma.Predict(chromaDc, cflAlpha, ac, width, height, prediction, this.sequenceHeader.BitDepth);
            return;
        }

        // DC prediction is computed directly from the available neighbour averages.
        if (intraMode == 0)
        {
            ushort dc = (ushort)this.PredictDc(plane, x, y, width, height);
            Array.Fill(prediction, dc);
            return;
        }

        // Directional modes (VERT..VERT_LEFT) use the extended edges and the angular predictor.
        if (intraMode is >= 1 and <= 8)
        {
            this.PrepareDirectionalEdges(plane, x, y, width, height, out ushort[] dAbove, out ushort[] dLeft, out ushort dTopLeft);
            Av1DirectionalPrediction.Predict(
                dAbove,
                dLeft,
                dTopLeft,
                width,
                height,
                intraMode,
                angleDelta,
                this.sequenceHeader.EnableIntraEdgeFilter,
                plane == this.luma ? this.lumaEdgeSmooth : this.chromaEdgeSmooth,
                y > 0,
                x > 0,
                plane.Width - x,
                plane.Height - y,
                prediction,
                this.sequenceHeader.BitDepth);
            return;
        }

        this.PrepareEdges(plane, x, y, width, height, out ushort[] above, out ushort[] left, out ushort topLeft);
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

    // The pixel row of the current tile's top edge in the given plane: intra reference samples above
    // it belong to another tile and are unavailable (dav1d have_top = by > tiling.row_start).
    private int TileTopPixel(Av1Plane plane)
        => ReferenceEquals(plane, this.luma) ? this.tileBounds.RowStart * 4 : (this.tileBounds.RowStart * 4) >> this.subsamplingY;

    // The pixel column of the current tile's left edge in the given plane.
    private int TileLeftPixel(Av1Plane plane)
        => ReferenceEquals(plane, this.luma) ? this.tileBounds.ColumnStart * 4 : (this.tileBounds.ColumnStart * 4) >> this.subsamplingX;

    // One past the last pixel column of the current tile in the given plane (dav1d passes
    // tiling.col_end >> ss_hor in 4x4 units into prepare_intra_edges).
    private int TileRightPixel(Av1Plane plane)
        => ReferenceEquals(plane, this.luma) ? this.tileBounds.ColumnEnd * 4 : (this.tileBounds.ColumnEnd >> this.subsamplingX) * 4;

    // One past the last pixel row of the current tile in the given plane.
    private int TileBottomPixel(Av1Plane plane)
        => ReferenceEquals(plane, this.luma) ? this.tileBounds.RowEnd * 4 : (this.tileBounds.RowEnd >> this.subsamplingY) * 4;

    // Gathers the extended reference edges (2*size above and left) for directional prediction, applying
    // the dav1d availability fills and frame-edge replication. Only square transforms are handled.
    private void PrepareDirectionalEdges(Av1Plane plane, int x, int y, int width, int height, out ushort[] above, out ushort[] left, out ushort topLeft)
    {
        bool hasAbove = y > this.TileTopPixel(plane);
        bool hasLeft = x > this.TileLeftPixel(plane);
        int extent = width + height;
        above = new ushort[extent];
        left = new ushort[extent];
        int tileRight = Math.Min(this.TileRightPixel(plane), plane.Width);
        int tileBottom = Math.Min(this.TileBottomPixel(plane), plane.Height);


        if (hasAbove)
        {
            // The transform-width edge is valid to the tile's right bound; the above-right extension
            // (one further transform width) additionally needs the edge-tree flag (dav1d
            // prepare_intra_edges needs_topright).
            int pxHave = Math.Min(width, tileRight - x);
            for (int i = 0; i < pxHave; i++)
            {
                above[i] = plane[x + i, y - 1];
            }

            for (int i = pxHave; i < width; i++)
            {
                above[i] = above[pxHave - 1];
            }

            if (this.transformHasTopRight && x + width < tileRight)
            {
                int extHave = Math.Min(Math.Min(width, extent - width), tileRight - x - width);
                for (int i = 0; i < extHave; i++)
                {
                    above[width + i] = plane[x + width + i, y - 1];
                }

                for (int i = width + extHave; i < extent; i++)
                {
                    above[i] = above[width + extHave - 1];
                }
            }
            else
            {
                for (int i = width; i < extent; i++)
                {
                    above[i] = above[width - 1];
                }
            }
        }
        else
        {
            ushort fill = hasLeft ? plane[x - 1, y] : (ushort)(this.midGrey - 1);
            Array.Fill(above, fill);
        }

        if (hasLeft)
        {
            int pxHave = Math.Min(height, tileBottom - y);
            for (int i = 0; i < pxHave; i++)
            {
                left[i] = plane[x - 1, y + i];
            }

            for (int i = pxHave; i < height; i++)
            {
                left[i] = left[pxHave - 1];
            }

            if (this.transformHasBottomLeft && y + height < tileBottom)
            {
                int extHave = Math.Min(Math.Min(height, extent - height), tileBottom - y - height);
                for (int i = 0; i < extHave; i++)
                {
                    left[height + i] = plane[x - 1, y + height + i];
                }

                for (int i = height + extHave; i < extent; i++)
                {
                    left[i] = left[height + extHave - 1];
                }
            }
            else
            {
                for (int i = height; i < extent; i++)
                {
                    left[i] = left[height - 1];
                }
            }
        }
        else
        {
            ushort fill = hasAbove ? plane[x, y - 1] : (ushort)(this.midGrey + 1);
            Array.Fill(left, fill);
        }

        topLeft = hasLeft
            ? hasAbove ? plane[x - 1, y - 1] : plane[x - 1, y]
            : hasAbove ? plane[x, y - 1] : (ushort)this.midGrey;
    }

    // Gathers up to 'count' samples of the row above, reading reconstructed samples and replicating the
    // last available one once the source (above-right) has not been decoded yet (dav1d edge availability).
    private void GatherAbove(Av1Plane plane, int x, int y, int count, ushort[] dst)
    {
        bool isLuma = ReferenceEquals(plane, this.luma);
        ushort last = plane[x, y - 1];
        bool available = true;
        for (int i = 0; i < count; i++)
        {
            int sx = x + i;
            if (available && sx < plane.Width && this.IsDecoded(sx, y - 1, isLuma))
            {
                last = plane[sx, y - 1];
            }
            else
            {
                available = false;
            }

            dst[i] = last;
        }
    }

    // Gathers up to 'count' samples of the column to the left, replicating once the source (below-left)
    // has not been decoded yet.
    private void GatherLeft(Av1Plane plane, int x, int y, int count, ushort[] dst)
    {
        bool isLuma = ReferenceEquals(plane, this.luma);
        ushort last = plane[x - 1, y];
        bool available = true;
        for (int i = 0; i < count; i++)
        {
            int sy = y + i;
            if (available && sy < plane.Height && this.IsDecoded(x - 1, sy, isLuma))
            {
                last = plane[x - 1, sy];
            }
            else
            {
                available = false;
            }

            dst[i] = last;
        }
    }

    // Whether the reconstructed sample at the given plane coordinate is available as an intra reference.
    private bool IsDecoded(int px, int py, bool isLuma)
    {
        int lumaCol = (isLuma ? px : px << this.subsamplingX) >> 2;
        int lumaRow = (isLuma ? py : py << this.subsamplingY) >> 2;
        if (lumaCol < 0 || lumaRow < 0 || lumaCol >= this.miColumns || lumaRow >= this.miRows)
        {
            return false;
        }

        return this.lumaDecoded[(lumaRow * this.miColumns) + lumaCol];
    }

    private int ReadAngleDelta(int mode, Av1BlockSize bsize)
    {
        // Angle delta is coded for directional modes when the block has at least 8 samples per side
        // total (dav1d: w_log2 + h_log2 >= 2).
        if (mode is >= 1 and <= 8 && bsize.GetWidthLog2() + bsize.GetHeightLog2() >= 2)
        {
            return this.decoder.ReadSymbol(this.modeCdf.AngleDelta[mode - 1]) - 3;
        }

        return 0;
    }

    private void PrepareEdges(Av1Plane plane, int x, int y, int width, int height, out ushort[] above, out ushort[] left, out ushort topLeft)
    {
        bool hasAbove = y > this.TileTopPixel(plane);
        bool hasLeft = x > this.TileLeftPixel(plane);
        ushort mid = (ushort)this.midGrey;
        above = new ushort[width];
        left = new ushort[height];

        if (hasAbove)
        {
            this.GatherAbove(plane, x, y, width, above);
        }
        else
        {
            ushort fill = hasLeft ? plane[x - 1, y] : (ushort)(this.midGrey - 1);
            Array.Fill(above, fill);
        }

        if (hasLeft)
        {
            this.GatherLeft(plane, x, y, height, left);
        }
        else
        {
            ushort fill = hasAbove ? plane[x, y - 1] : (ushort)(this.midGrey + 1);
            Array.Fill(left, fill);
        }

        topLeft = hasLeft
            ? hasAbove ? plane[x - 1, y - 1] : plane[x - 1, y]
            : hasAbove ? plane[x, y - 1] : mid;
    }

    // Reads the CfL joint sign and per-plane alpha magnitudes (specification 5.11.45, dav1d order).
    private void ReadCflAlphas(out int alphaU, out int alphaV)
    {
        int sign = this.decoder.ReadSymbol(this.modeCdf.CflSign) + 1;
        int signU = (sign * 0x56) >> 8;
        int signV = sign - (signU * 3);

        alphaU = 0;
        if (signU != 0)
        {
            int ctx = ((signU == 2 ? 1 : 0) * 3) + signV;
            alphaU = this.decoder.ReadSymbol(this.modeCdf.CflAlpha[ctx]) + 1;
            if (signU == 1)
            {
                alphaU = -alphaU;
            }
        }

        alphaV = 0;
        if (signV != 0)
        {
            int ctx = ((signV == 2 ? 1 : 0) * 3) + signU;
            alphaV = this.decoder.ReadSymbol(this.modeCdf.CflAlpha[ctx]) + 1;
            if (signV == 1)
            {
                alphaV = -alphaV;
            }
        }
    }

    private static void EnsureSupportedMode(int mode)
    {
        // All 13 intra prediction modes (DC, the 8 directional and the 4 non-directional) plus CfL (uv
        // mode 13) are handled.
        if (mode is < 0 or > 13)
        {
            throw new NotSupportedException($"Intra prediction mode {mode} is not supported yet.");
        }
    }

    private int PredictDc(Av1Plane plane, int x, int y, int width, int height)
    {
        bool hasAbove = y > this.TileTopPixel(plane);
        bool hasLeft = x > this.TileLeftPixel(plane);
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
        int blockLhAdjusted = bsize.GetHeightLog2() - (this.subsamplingY != 0 ? 1 : 0);
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

    // Called at the start of each superblock row, after the shared left-context arrays have been reset.
    // Subclasses override this to reset their own per-row left contexts (e.g. the inter variable-transform
    // size context, which dav1d resets to TX_64X64 once per superblock row).
    private protected virtual void OnSuperblockRowStart()
    {
    }

    // Called after each luma transform block's transform type is known. The inter decoder overrides this
    // to record the type into its per-4x4 map, from which co-located chroma transform types are inferred.
    private protected virtual void RecordLumaTransformType(int txCol, int txRow, int txWidth4, int txHeight4, Av1TransformType txType)
    {
    }

    private static Av1Plane ClonePlane(Av1Plane source)
    {
        Av1Plane plane = new(source.Width, source.Height, source.CropWidth, source.CropHeight);
        source.Samples.CopyTo(plane.Samples, 0);
        return plane;
    }

    private protected static void Fill(byte[] context, int start, int count, byte value)
    {
        for (int i = 0; i < count && start + i < context.Length; i++)
        {
            context[start + i] = value;
        }
    }

    private protected static void Fill(sbyte[] context, int start, int count, sbyte value)
    {
        for (int i = 0; i < count && start + i < context.Length; i++)
        {
            context[start + i] = value;
        }
    }

    // Whether a neighbour intra mode is one of the smooth predictors (SMOOTH/SMOOTH_V/SMOOTH_H),
    // matching dav1d's sm_flag; such neighbours reduce the directional edge-filter strength.
    private static bool IsSmoothMode(int mode) => mode is 9 or 10 or 11;

    /// <summary>
    /// The coefficient level-context bytes for one plane: an 'above' row spanning the frame width and a
    /// 'left' column spanning the frame height (reset per superblock row), in 4x4 units.
    /// </summary>
    private protected sealed class LevelContext
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

        public void ClearAbove(int col, int count)
            => Array.Fill(this.above, LevelContextBaseline, col, Math.Min(count, this.above.Length - col));
    }
}
