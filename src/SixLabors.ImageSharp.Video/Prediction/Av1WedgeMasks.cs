// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The wedge-compound blend masks (a port of dav1d's <c>dav1d_init_ii_wedge_masks</c> wedge half):
/// six 64x64 master templates cut into sixteen wedge shapes per eligible block size, with the
/// chroma masks derived by sign-aware subsampling. Masks are indexed by the wedge block-size
/// context (the same ordering as the wedge CDF context lookup), the wedge index and, for chroma,
/// the mask sign.
/// </summary>
internal static class Av1WedgeMasks
{
    private const int WedgeHorizontal = 0;
    private const int WedgeVertical = 1;
    private const int WedgeOblique27 = 2;
    private const int WedgeOblique63 = 3;
    private const int WedgeOblique117 = 4;
    private const int WedgeOblique153 = 5;

    // (direction, xOffset, yOffset) codebooks (dav1d wedge_codebook_16_*).
    private static readonly (int Direction, int X, int Y)[] CodebookHeightGreater =
    [
        (WedgeOblique27, 4, 4), (WedgeOblique63, 4, 4), (WedgeOblique117, 4, 4), (WedgeOblique153, 4, 4),
        (WedgeHorizontal, 4, 2), (WedgeHorizontal, 4, 4), (WedgeHorizontal, 4, 6), (WedgeVertical, 4, 4),
        (WedgeOblique27, 4, 2), (WedgeOblique27, 4, 6), (WedgeOblique153, 4, 2), (WedgeOblique153, 4, 6),
        (WedgeOblique63, 2, 4), (WedgeOblique63, 6, 4), (WedgeOblique117, 2, 4), (WedgeOblique117, 6, 4),
    ];

    private static readonly (int Direction, int X, int Y)[] CodebookHeightLess =
    [
        (WedgeOblique27, 4, 4), (WedgeOblique63, 4, 4), (WedgeOblique117, 4, 4), (WedgeOblique153, 4, 4),
        (WedgeVertical, 2, 4), (WedgeVertical, 4, 4), (WedgeVertical, 6, 4), (WedgeHorizontal, 4, 4),
        (WedgeOblique27, 4, 2), (WedgeOblique27, 4, 6), (WedgeOblique153, 4, 2), (WedgeOblique153, 4, 6),
        (WedgeOblique63, 2, 4), (WedgeOblique63, 6, 4), (WedgeOblique117, 2, 4), (WedgeOblique117, 6, 4),
    ];

    private static readonly (int Direction, int X, int Y)[] CodebookHeightEqual =
    [
        (WedgeOblique27, 4, 4), (WedgeOblique63, 4, 4), (WedgeOblique117, 4, 4), (WedgeOblique153, 4, 4),
        (WedgeHorizontal, 4, 2), (WedgeHorizontal, 4, 6), (WedgeVertical, 2, 4), (WedgeVertical, 6, 4),
        (WedgeOblique27, 4, 2), (WedgeOblique27, 4, 6), (WedgeOblique153, 4, 2), (WedgeOblique153, 4, 6),
        (WedgeOblique63, 2, 4), (WedgeOblique63, 6, 4), (WedgeOblique117, 2, 4), (WedgeOblique117, 6, 4),
    ];

    // Sizes ordered by the wedge context (dav1d_wedge_ctx_lut values): the context doubles as the
    // mask table index.
    private static readonly (int W, int H, (int Direction, int X, int Y)[] Codebook, uint Signs)[] Sizes =
    [
        (8, 8, CodebookHeightEqual, 0x7bfb),    // ctx 0: 8x8
        (8, 16, CodebookHeightGreater, 0x7beb), // ctx 1: 8x16
        (16, 8, CodebookHeightLess, 0x7beb),    // ctx 2: 16x8
        (16, 16, CodebookHeightEqual, 0x7bfb),  // ctx 3: 16x16
        (16, 32, CodebookHeightGreater, 0x7beb), // ctx 4: 16x32
        (32, 16, CodebookHeightLess, 0x7beb),   // ctx 5: 32x16
        (32, 32, CodebookHeightEqual, 0x7bfb),  // ctx 6: 32x32
        (8, 32, CodebookHeightGreater, 0x7aeb), // ctx 7: 8x32
        (32, 8, CodebookHeightLess, 0x6beb),    // ctx 8: 32x8
    ];

    /// <summary>Gets the luma wedge masks: [wedge block-size context][wedge index] with w*h weights.</summary>
    public static byte[][][] Luma { get; }

    /// <summary>Gets the 4:2:0 chroma wedge masks: [wedge block-size context][sign][wedge index] with
    /// (w/2)*(h/2) weights.</summary>
    public static byte[][][][] Chroma420 { get; }

    /// <summary>Gets the 4:2:2 chroma wedge masks: [wedge block-size context][sign][wedge index] with
    /// (w/2)*h weights.</summary>
    public static byte[][][][] Chroma422 { get; }

    /// <summary>Gets the 4:4:4 chroma wedge masks (the unsubsampled luma masks, identical for both
    /// mask signs).</summary>
    public static byte[][][][] Chroma444 { get; }

    /// <summary>Gets the chroma wedge masks for a chroma layout index (0 = 4:4:4, 1 = 4:2:2,
    /// 2 = 4:2:0, matching dav1d's <c>chr_layout_idx</c>).</summary>
    /// <param name="chromaLayoutIndex">The layout index (the sum of the subsampling flags).</param>
    /// <returns>The per-context, per-sign masks.</returns>
    public static byte[][][][] Chroma(int chromaLayoutIndex)
        => chromaLayoutIndex == 0 ? Chroma444 : chromaLayoutIndex == 1 ? Chroma422 : Chroma420;

    static Av1WedgeMasks()
    {
        // Master templates.
        byte[][] master = new byte[6][];
        for (int i = 0; i < 6; i++)
        {
            master[i] = new byte[64 * 64];
        }

        ReadOnlySpan<byte> borderOdd = [1, 2, 6, 18, 37, 53, 60, 63];
        ReadOnlySpan<byte> borderEven = [1, 4, 11, 27, 46, 58, 62, 63];
        ReadOnlySpan<byte> borderVert = [0, 2, 7, 21, 43, 57, 62, 64];

        for (int y = 0, off = 0; y < 64; y++, off += 64)
        {
            InsertBorder(master[WedgeVertical].AsSpan(off), borderVert, 32);
        }

        for (int y = 0, off = 0, ctr = 48; y < 64; y += 2, off += 128, ctr--)
        {
            InsertBorder(master[WedgeOblique63].AsSpan(off), borderEven, ctr);
            InsertBorder(master[WedgeOblique63].AsSpan(off + 64), borderOdd, ctr - 1);
        }

        Transpose(master[WedgeOblique27], master[WedgeOblique63]);
        Transpose(master[WedgeHorizontal], master[WedgeVertical]);
        HorizontalFlip(master[WedgeOblique117], master[WedgeOblique63]);
        HorizontalFlip(master[WedgeOblique153], master[WedgeOblique27]);

        Luma = new byte[Sizes.Length][][];
        Chroma420 = new byte[Sizes.Length][][][];
        Chroma422 = new byte[Sizes.Length][][][];
        Chroma444 = new byte[Sizes.Length][][][];
        for (int s = 0; s < Sizes.Length; s++)
        {
            (int w, int h, (int Direction, int X, int Y)[] codebook, uint signs) = Sizes[s];
            Luma[s] = new byte[16][];
            Chroma420[s] = [new byte[16][], new byte[16][]];
            Chroma422[s] = [new byte[16][], new byte[16][]];
            for (int n = 0; n < 16; n++)
            {
                int sign = (int)(signs >> n) & 1;
                byte[] luma = Copy2d(master[codebook[n].Direction], sign, w, h, 32 - ((w * codebook[n].X) >> 3), 32 - ((h * codebook[n].Y) >> 3));
                Luma[s][n] = luma;

                // The mask-sign index selects the chroma subsampling rounding (the codebook sign is
                // already baked into the luma mask). 4:4:4 chroma needs no rounding and shares the
                // luma masks for both signs.
                Chroma420[s][0][n] = SubsampleChroma(luma, 0, w, h, 1);
                Chroma420[s][1][n] = SubsampleChroma(luma, 1, w, h, 1);
                Chroma422[s][0][n] = SubsampleChroma(luma, 0, w, h, 0);
                Chroma422[s][1][n] = SubsampleChroma(luma, 1, w, h, 0);
            }

            Chroma444[s] = [Luma[s], Luma[s]];
        }
    }

    private static void InsertBorder(Span<byte> dst, ReadOnlySpan<byte> src, int ctr)
    {
        if (ctr > 4)
        {
            dst[..(ctr - 4)].Clear();
        }

        int dstStart = Math.Max(ctr, 4) - 4;
        int srcStart = Math.Max(4 - ctr, 0);
        int count = Math.Min(64 - ctr, 8);
        src.Slice(srcStart, count).CopyTo(dst[dstStart..]);
        if (ctr < 64 - 4)
        {
            dst.Slice(ctr + 4, 64 - 4 - ctr).Fill(64);
        }
    }

    private static void Transpose(byte[] dst, byte[] src)
    {
        for (int y = 0, yOff = 0; y < 64; y++, yOff += 64)
        {
            for (int x = 0, xOff = 0; x < 64; x++, xOff += 64)
            {
                dst[xOff + y] = src[yOff + x];
            }
        }
    }

    private static void HorizontalFlip(byte[] dst, byte[] src)
    {
        for (int y = 0, yOff = 0; y < 64; y++, yOff += 64)
        {
            for (int x = 0; x < 64; x++)
            {
                dst[yOff + 64 - 1 - x] = src[yOff + x];
            }
        }
    }

    private static byte[] Copy2d(byte[] src, int sign, int w, int h, int xOff, int yOff)
    {
        byte[] dst = new byte[w * h];
        int srcBase = (yOff * 64) + xOff;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte v = src[srcBase + x];
                dst[(y * w) + x] = sign != 0 ? (byte)(64 - v) : v;
            }

            srcBase += 64;
        }

        return dst;
    }

    // dav1d init_chroma: each chroma weight is the sign-rounded average of the underlying luma
    // weights (2x2 for 4:2:0, a horizontal pair for 4:2:2).
    private static byte[] SubsampleChroma(byte[] luma, int sign, int w, int h, int ssVer)
    {
        byte[] chroma = new byte[(w >> 1) * (h >> ssVer)];
        int lumaBase = 0;
        int chromaBase = 0;
        for (int y = 0; y < h; y += 1 + ssVer)
        {
            for (int x = 0; x < w; x += 2)
            {
                int sum = luma[lumaBase + x] + luma[lumaBase + x + 1] + 1;
                if (ssVer != 0)
                {
                    sum += luma[lumaBase + w + x] + luma[lumaBase + w + x + 1] + 1;
                }

                chroma[chromaBase + (x >> 1)] = (byte)((sum - sign) >> (1 + ssVer));
            }

            lumaBase += w << ssVer;
            chromaBase += w >> 1;
        }

        return chroma;
    }
}
