// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The inter-intra blend masks (a port of dav1d's <c>build_nondc_ii_masks</c>): per block size and
/// intra mode, a 64-weight mask that fades the intra prediction out with the distance from the
/// predicted edge (vertical, horizontal, or the minimum of both for the smooth mode); the DC mode
/// blends with a flat weight of 32.
/// </summary>
internal static class Av1InterIntraMasks
{
    private static readonly byte[] Weights1d =
    [
        60, 52, 45, 39, 34, 30, 26, 22, 19, 17, 15, 13, 11, 10, 8, 7,
        6, 6, 5, 4, 4, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1, 1,
    ];

    // Non-DC mask tables keyed by "w x h": [mode - 1 (V/H/SM)][w * h].
    private static readonly Dictionary<(int W, int H), byte[][]> NonDc = new()
    {
        [(32, 32)] = Build(32, 32, 1),
        [(16, 32)] = Build(16, 32, 1),
        [(16, 16)] = Build(16, 16, 2),
        [(8, 32)] = Build(8, 32, 1),
        [(8, 16)] = Build(8, 16, 2),
        [(8, 8)] = Build(8, 8, 4),
        [(4, 16)] = Build(4, 16, 2),
        [(4, 8)] = Build(4, 8, 4),
        [(4, 4)] = Build(4, 4, 8),
    };

    // Table dimensions per luma block size (dav1d ASSIGN_NONDC_II_OFFSET): the mask is read with the
    // table's row stride, which equals the block width for every mapping.
    private static readonly Dictionary<(int W4, int H4), (int W, int H)> LumaTable = new()
    {
        [(8, 8)] = (32, 32),
        [(8, 4)] = (32, 32),
        [(4, 8)] = (16, 32),
        [(4, 4)] = (16, 16),
        [(4, 2)] = (16, 16),
        [(2, 4)] = (8, 16),
        [(2, 2)] = (8, 8),
    };

    private static readonly Dictionary<(int W4, int H4), (int W, int H)> Chroma422Table = new()
    {
        [(8, 8)] = (16, 32),
        [(8, 4)] = (16, 16),
        [(4, 8)] = (8, 32),
        [(4, 4)] = (8, 16),
        [(4, 2)] = (8, 8),
        [(2, 4)] = (4, 16),
        [(2, 2)] = (4, 8),
    };

    private static readonly Dictionary<(int W4, int H4), (int W, int H)> Chroma420Table = new()
    {
        [(8, 8)] = (16, 16),
        [(8, 4)] = (16, 16),
        [(4, 8)] = (8, 16),
        [(4, 4)] = (8, 8),
        [(4, 2)] = (8, 8),
        [(2, 4)] = (4, 8),
        [(2, 2)] = (4, 4),
    };

    /// <summary>
    /// Gets the inter-intra mask for a block: <see langword="null"/> signals the flat DC weight of 32.
    /// </summary>
    /// <param name="width4">The block width in luma 4x4 units.</param>
    /// <param name="height4">The block height in luma 4x4 units.</param>
    /// <param name="interIntraMode">The inter-intra mode (0 = DC, 1 = V, 2 = H, 3 = SMOOTH).</param>
    /// <param name="chromaLayoutIndex">The chroma layout index of the requested plane (-1 or 0 for
    /// luma/4:4:4, 1 for 4:2:2 chroma, 2 for 4:2:0 chroma; the sum of the subsampling flags).</param>
    /// <returns>The mask and its row stride, or <see langword="null"/> for the flat DC blend.</returns>
    public static (byte[] Mask, int Stride)? Get(int width4, int height4, int interIntraMode, int chromaLayoutIndex)
    {
        if (interIntraMode == 0)
        {
            return null;
        }

        Dictionary<(int W4, int H4), (int W, int H)> table = chromaLayoutIndex switch
        {
            2 => Chroma420Table,
            1 => Chroma422Table,
            _ => LumaTable,
        };
        (int w, int h) = table[(width4, height4)];
        return (NonDc[(w, h)][interIntraMode - 1], w);
    }

    private static byte[][] Build(int w, int h, int step)
    {
        byte[] maskV = new byte[w * h];
        byte[] maskH = new byte[w * h];
        byte[] maskSm = new byte[w * h];
        for (int y = 0, off = 0; y < h; y++, off += w)
        {
            byte vertical = Weights1d[y * step];
            for (int x = 0; x < w; x++)
            {
                maskV[off + x] = vertical;
                maskH[off + x] = Weights1d[x * step];
                maskSm[off + x] = Weights1d[Math.Min(x, y) * step];
            }
        }

        return [maskV, maskH, maskSm];
    }
}
