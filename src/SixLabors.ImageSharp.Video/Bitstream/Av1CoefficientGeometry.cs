// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The per-transform-block geometry shared by the coefficient reader and its inverse: the level-buffer
/// stride and the scan/position decomposition for each transform class, plus the neighbour-derived
/// coeff_base context (a port of dav1d's <c>get_lo_ctx</c>). Keeping this in one place guarantees the
/// reader and any matching writer derive identical adaptation contexts.
/// </summary>
internal readonly struct Av1CoefficientGeometry
{
    // dav1d_lo_ctx_offsets[3][5][5]: [0] w==h, [1] w>h, [2] w<h.
    private static readonly byte[][][] LoContextOffsets =
    [
        [
            [0, 1, 6, 6, 21],
            [1, 6, 6, 21, 21],
            [6, 6, 21, 21, 21],
            [6, 21, 21, 21, 21],
            [21, 21, 21, 21, 21],
        ],
        [
            [0, 16, 6, 6, 21],
            [16, 16, 6, 21, 21],
            [16, 16, 21, 21, 21],
            [16, 16, 21, 21, 21],
            [16, 16, 21, 21, 21],
        ],
        [
            [0, 11, 11, 11, 11],
            [11, 11, 11, 11, 11],
            [6, 6, 21, 21, 21],
            [6, 21, 21, 21, 21],
            [21, 21, 21, 21, 21],
        ],
    ];

    private Av1CoefficientGeometry(Av1TransformClass txClass, int stride, int shift, int shift2, int mask, int levelRows, byte[][] contextOffsets)
    {
        this.TxClass = txClass;
        this.Stride = stride;
        this.Shift = shift;
        this.Shift2 = shift2;
        this.Mask = mask;
        this.LevelRows = levelRows;
        this.ContextOffsets = contextOffsets;
    }

    public Av1TransformClass TxClass { get; }

    public bool Is2d => this.TxClass == Av1TransformClass.TwoDimensional;

    public int Stride { get; }

    public int Shift { get; }

    public int Shift2 { get; }

    public int Mask { get; }

    public int LevelRows { get; }

    public byte[][] ContextOffsets { get; }

    /// <summary>Gets the required length of the (zero-padded) level buffer.</summary>
    public int LevelBufferLength => this.Stride * (this.LevelRows + 2);

    public static Av1CoefficientGeometry Create(Av1TransformSize transformSize, Av1TransformClass txClass)
    {
        int slw = Math.Min(transformSize.GetWidthLog2() - 2, 3);
        int slh = Math.Min(transformSize.GetHeightLog2() - 2, 3);
        switch (txClass)
        {
            case Av1TransformClass.TwoDimensional:
                int stride2d = 4 << slh;
                return new Av1CoefficientGeometry(txClass, stride2d, slh + 2, 0, stride2d - 1, 4 << slw, LoContextOffsets[NonSquareOffsetIndex(transformSize)]);
            case Av1TransformClass.Horizontal:
                return new Av1CoefficientGeometry(txClass, 16, slh + 2, 0, (4 << slh) - 1, 4 << slh, null!);
            default: // Vertical
                return new Av1CoefficientGeometry(txClass, 16, slw + 2, slh + 2, (4 << slw) - 1, 4 << slw, null!);
        }
    }

    /// <summary>Decomposes scan index <paramref name="i"/> into its column/row and raster position.</summary>
    public void DecodePosition(int i, ReadOnlySpan<ushort> scan, out int x, out int y, out int rc)
    {
        switch (this.TxClass)
        {
            case Av1TransformClass.TwoDimensional:
                rc = scan[i];
                x = rc >> this.Shift;
                y = rc & this.Mask;
                break;
            case Av1TransformClass.Horizontal:
                x = i & this.Mask;
                y = i >> this.Shift;
                rc = i;
                break;
            default: // Vertical
                x = i & this.Mask;
                y = i >> this.Shift;
                rc = (x << this.Shift2) | y;
                break;
        }
    }

    /// <summary>Gets the level-buffer index for a coefficient at column/row (x, y) with raster position rc.</summary>
    public int LevelIndex(int x, int y, int rc) => this.Is2d ? rc : (x * this.Stride) + y;

    /// <summary>
    /// Computes the coeff_base context from the already-visited higher-frequency neighbours, and the
    /// magnitude sum <paramref name="hiMag"/> used to derive the high-token context.
    /// </summary>
    public int GetLowContext(byte[] levels, int offset, int x, int y, out int hiMag)
    {
        int stride = this.Stride;
        int mag = levels[offset + (0 * stride) + 1] + levels[offset + (1 * stride) + 0];
        int contextOffset;
        if (this.Is2d)
        {
            mag += levels[offset + (1 * stride) + 1];
            hiMag = mag;
            mag += levels[offset + (0 * stride) + 2] + levels[offset + (2 * stride) + 0];
            contextOffset = this.ContextOffsets[Math.Min(y, 4)][Math.Min(x, 4)];
        }
        else
        {
            mag += levels[offset + (0 * stride) + 2];
            hiMag = mag;
            mag += levels[offset + (0 * stride) + 3] + levels[offset + (0 * stride) + 4];
            contextOffset = 26 + (y > 1 ? 10 : y * 5);
        }

        return contextOffset + (mag > 512 ? 4 : (mag + 64) >> 7);
    }

    private static int NonSquareOffsetIndex(Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        if (width == height)
        {
            return 0;
        }

        return width > height ? 1 : 2;
    }
}
