// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// Recursive filter intra prediction (specification section 7.11.2.3), a port of dav1d's
/// <c>ipred_filter</c>. Each 4x2 sub-unit is predicted from seven neighbouring samples (the corner, four
/// above and two left) using one of five fixed tap sets; predicted samples feed the units below and to
/// the right. Used for luma blocks up to 32x32 when filter-intra is signalled.
/// </summary>
internal static class Av1FilterIntraPrediction
{
    // Intra_Filter_Taps[filter][cell 0..7][tap p0..p6] (dav1d_filter_intra_taps, de-interleaved).
    private static readonly sbyte[][][] Taps =
    [
        [
            [-6, 10, 0, 0, 0, 12, 0], [-5, 2, 10, 0, 0, 9, 0], [-3, 1, 1, 10, 0, 7, 0], [-3, 1, 1, 2, 10, 5, 0],
            [-4, 6, 0, 0, 0, 2, 12], [-3, 2, 6, 0, 0, 2, 9], [-3, 2, 2, 6, 0, 2, 7], [-3, 1, 2, 2, 6, 3, 5],
        ],
        [
            [-10, 16, 0, 0, 0, 10, 0], [-6, 0, 16, 0, 0, 6, 0], [-4, 0, 0, 16, 0, 4, 0], [-2, 0, 0, 0, 16, 2, 0],
            [-10, 16, 0, 0, 0, 0, 10], [-6, 0, 16, 0, 0, 0, 6], [-4, 0, 0, 16, 0, 0, 4], [-2, 0, 0, 0, 16, 0, 2],
        ],
        [
            [-8, 8, 0, 0, 0, 16, 0], [-8, 0, 8, 0, 0, 16, 0], [-8, 0, 0, 8, 0, 16, 0], [-8, 0, 0, 0, 8, 16, 0],
            [-4, 4, 0, 0, 0, 0, 16], [-4, 0, 4, 0, 0, 0, 16], [-4, 0, 0, 4, 0, 0, 16], [-4, 0, 0, 0, 4, 0, 16],
        ],
        [
            [-2, 8, 0, 0, 0, 10, 0], [-1, 3, 8, 0, 0, 6, 0], [-1, 2, 3, 8, 0, 4, 0], [0, 1, 2, 3, 8, 2, 0],
            [-1, 4, 0, 0, 0, 3, 10], [-1, 3, 4, 0, 0, 4, 6], [-1, 2, 3, 4, 0, 4, 4], [-1, 2, 2, 3, 4, 3, 3],
        ],
        [
            [-12, 14, 0, 0, 0, 14, 0], [-10, 0, 14, 0, 0, 12, 0], [-9, 0, 0, 14, 0, 11, 0], [-8, 0, 0, 0, 14, 10, 0],
            [-10, 12, 0, 0, 0, 0, 14], [-9, 1, 12, 0, 0, 0, 12], [-8, 0, 0, 12, 0, 1, 11], [-7, 0, 0, 1, 12, 1, 9],
        ],
    ];

    /// <summary>
    /// Predicts a square filter-intra block. The above and left reference samples and the corner are
    /// taken from the reconstructed neighbours; predicted samples feed later sub-units in place.
    /// </summary>
    /// <param name="above">The above reference row (index 0..width-1).</param>
    /// <param name="left">The left reference column (index 0..height-1).</param>
    /// <param name="topLeft">The top-left corner sample.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="filterIndex">The filter tap set index (0..4).</param>
    /// <param name="destination">The prediction output buffer (width*height, row-major).</param>
    public static void Predict(ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> left, ushort topLeft, int width, int height, int filterIndex, Span<ushort> destination)
    {
        sbyte[][] filter = Taps[filterIndex];

        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 4)
            {
                int p0 = x == 0 ? (y == 0 ? topLeft : left[y - 1]) : SampleTop(destination, above, width, y, x - 1);
                int p1 = SampleTop(destination, above, width, y, x);
                int p2 = SampleTop(destination, above, width, y, x + 1);
                int p3 = SampleTop(destination, above, width, y, x + 2);
                int p4 = SampleTop(destination, above, width, y, x + 3);
                int p5;
                int p6;
                if (x == 0)
                {
                    p5 = left[y];
                    p6 = left[y + 1];
                }
                else
                {
                    p5 = destination[(y * width) + (x - 1)];
                    p6 = destination[((y + 1) * width) + (x - 1)];
                }

                for (int yy = 0; yy < 2; yy++)
                {
                    for (int xx = 0; xx < 4; xx++)
                    {
                        sbyte[] t = filter[(yy * 4) + xx];
                        int acc = (t[0] * p0) + (t[1] * p1) + (t[2] * p2) + (t[3] * p3) + (t[4] * p4) + (t[5] * p5) + (t[6] * p6);
                        destination[((y + yy) * width) + x + xx] = (ushort)Math.Clamp((acc + 8) >> 4, 0, 255);
                    }
                }
            }
        }
    }

    // The "top" row for band y at column c: the above edge for the first band, otherwise the row above.
    private static int SampleTop(Span<ushort> dst, ReadOnlySpan<ushort> above, int width, int y, int c) =>
        y == 0 ? above[c] : dst[((y - 1) * width) + c];
}
