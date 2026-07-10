// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The constrained directional enhancement filter (CDEF, specification section 7.15), a port of
/// dav1d's <c>cdef_find_dir</c> and <c>cdef_filter_block</c> for 8-bit samples.
/// </summary>
internal static class Av1Cdef
{
    /// <summary>Marker for unavailable reference samples (interpreted as "very large").</summary>
    public const int VeryLarge = short.MinValue;

    private const int TmpStride = 12;

    // dav1d_cdef_directions: per (dir + 2) the two tap offsets into the padded buffer (stride 12).
    private static readonly int[][] Directions =
    [
        [(1 * 12) + 0, (2 * 12) + 0],
        [(1 * 12) + 0, (2 * 12) - 1],
        [(-1 * 12) + 1, (-2 * 12) + 2],
        [(0 * 12) + 1, (-1 * 12) + 2],
        [(0 * 12) + 1, (0 * 12) + 2],
        [(0 * 12) + 1, (1 * 12) + 2],
        [(1 * 12) + 1, (2 * 12) + 2],
        [(1 * 12) + 0, (2 * 12) + 1],
        [(1 * 12) + 0, (2 * 12) + 0],
        [(1 * 12) + 0, (2 * 12) - 1],
        [(-1 * 12) + 1, (-2 * 12) + 2],
        [(0 * 12) + 1, (-1 * 12) + 2],
    ];

    private static readonly int[] DivTable = [840, 420, 280, 210, 168, 140, 120];

    /// <summary>Edge availability flags for <see cref="FilterBlock"/>.</summary>
    [Flags]
    public enum EdgeFlags
    {
        /// <summary>The left neighbour is available.</summary>
        Left = 1 << 0,

        /// <summary>The right neighbour is available.</summary>
        Right = 1 << 1,

        /// <summary>The top neighbour is available.</summary>
        Top = 1 << 2,

        /// <summary>The bottom neighbour is available.</summary>
        Bottom = 1 << 3,
    }

    /// <summary>
    /// Estimates the dominant edge direction (0..7) of an 8x8 block and its variance.
    /// </summary>
    /// <param name="img">The 8x8 luma block (row-major access via <paramref name="stride"/>).</param>
    /// <param name="offset">The offset of the block's first sample.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="variance">Receives the direction variance.</param>
    /// <returns>The best direction in [0, 7].</returns>
    public static int FindDirection(ReadOnlySpan<ushort> img, int offset, int stride, out int variance)
    {
        int[][] partialSumHv = [new int[8], new int[8]];
        int[][] partialSumDiag = [new int[15], new int[15]];
        int[][] partialSumAlt = [new int[11], new int[11], new int[11], new int[11]];

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int px = img[offset + (y * stride) + x] - 128;
                partialSumDiag[0][y + x] += px;
                partialSumAlt[0][y + (x >> 1)] += px;
                partialSumHv[0][y] += px;
                partialSumAlt[1][3 + y - (x >> 1)] += px;
                partialSumDiag[1][7 + y - x] += px;
                partialSumAlt[2][3 - (y >> 1) + x] += px;
                partialSumHv[1][x] += px;
                partialSumAlt[3][(y >> 1) + x] += px;
            }
        }

        uint[] cost = new uint[8];
        for (int n = 0; n < 8; n++)
        {
            cost[2] += (uint)(partialSumHv[0][n] * partialSumHv[0][n]);
            cost[6] += (uint)(partialSumHv[1][n] * partialSumHv[1][n]);
        }

        cost[2] *= 105;
        cost[6] *= 105;

        for (int n = 0; n < 7; n++)
        {
            int d = DivTable[n];
            cost[0] += (uint)(((partialSumDiag[0][n] * partialSumDiag[0][n]) + (partialSumDiag[0][14 - n] * partialSumDiag[0][14 - n])) * d);
            cost[4] += (uint)(((partialSumDiag[1][n] * partialSumDiag[1][n]) + (partialSumDiag[1][14 - n] * partialSumDiag[1][14 - n])) * d);
        }

        cost[0] += (uint)(partialSumDiag[0][7] * partialSumDiag[0][7] * 105);
        cost[4] += (uint)(partialSumDiag[1][7] * partialSumDiag[1][7] * 105);

        for (int n = 0; n < 4; n++)
        {
            int index = (n * 2) + 1;
            uint c = 0;
            for (int m = 0; m < 5; m++)
            {
                c += (uint)(partialSumAlt[n][3 + m] * partialSumAlt[n][3 + m]);
            }

            c *= 105;
            for (int m = 0; m < 3; m++)
            {
                int d = DivTable[(2 * m) + 1];
                c += (uint)(((partialSumAlt[n][m] * partialSumAlt[n][m]) + (partialSumAlt[n][10 - m] * partialSumAlt[n][10 - m])) * d);
            }

            cost[index] = c;
        }

        int bestDir = 0;
        uint bestCost = cost[0];
        for (int n = 1; n < 8; n++)
        {
            if (cost[n] > bestCost)
            {
                bestCost = cost[n];
                bestDir = n;
            }
        }

        variance = (int)((bestCost - cost[bestDir ^ 4]) >> 10);
        return bestDir;
    }

    /// <summary>
    /// Applies the CDEF filter to a w x h block in place, reading the surrounding samples from the
    /// supplied edges (a port of dav1d's <c>cdef_filter_block</c>). The block's two right-neighbour
    /// columns are read directly from <paramref name="dst"/> when the right edge is available.
    /// </summary>
    /// <param name="dst">The destination plane buffer.</param>
    /// <param name="dstOffset">The offset of the block's top-left sample.</param>
    /// <param name="dstStride">The destination row stride.</param>
    /// <param name="left">The two left columns, indexed [row][0..1].</param>
    /// <param name="top">The two top rows above the block, length (w + 4), starting two left of the block.</param>
    /// <param name="bottom">The two bottom rows below the block, length (w + 4), starting two left of the block.</param>
    /// <param name="priStrength">The primary strength.</param>
    /// <param name="secStrength">The secondary strength.</param>
    /// <param name="dir">The edge direction (0..7).</param>
    /// <param name="damping">The damping value.</param>
    /// <param name="w">The block width (4 or 8).</param>
    /// <param name="h">The block height (4 or 8).</param>
    /// <param name="edges">The available edges.</param>
    public static void FilterBlock(
        Span<ushort> dst,
        int dstOffset,
        int dstStride,
        ReadOnlySpan<ushort> left,
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> bottom,
        int priStrength,
        int secStrength,
        int dir,
        int damping,
        int w,
        int h,
        EdgeFlags edges)
    {
        int[] tmpBuf = new int[TmpStride * (h + 4)];
        int center = (2 * TmpStride) + 2; // tmp pointer origin (corresponds to dst[0]).
        Padding(tmpBuf, center, dst, dstOffset, dstStride, left, top, bottom, w, h, edges);

        int priTap = 4 - (priStrength & 1);
        int priShift = Math.Max(0, damping - Log2(priStrength));
        int secShift = secStrength != 0 ? damping - Log2(secStrength) : 0;

        for (int y = 0; y < h; y++)
        {
            int tmpRow = center + (y * TmpStride);
            for (int x = 0; x < w; x++)
            {
                int px = dst[dstOffset + (y * dstStride) + x];
                int sum = 0;
                int max = px;
                int min = px;
                int priTapK = priTap;
                for (int k = 0; k < 2; k++)
                {
                    if (priStrength != 0)
                    {
                        int off1 = Directions[dir + 2][k];
                        int p0 = tmpBuf[tmpRow + x + off1];
                        int p1 = tmpBuf[tmpRow + x - off1];
                        sum += priTapK * Constrain(p0 - px, priStrength, priShift);
                        sum += priTapK * Constrain(p1 - px, priStrength, priShift);
                        priTapK = (priTapK & 3) | 2;
                        min = UMin(p0, min);
                        max = Math.Max(p0, max);
                        min = UMin(p1, min);
                        max = Math.Max(p1, max);
                    }

                    if (secStrength != 0)
                    {
                        int off2 = Directions[dir + 4][k];
                        int off3 = Directions[dir + 0][k];
                        int s0 = tmpBuf[tmpRow + x + off2];
                        int s1 = tmpBuf[tmpRow + x - off2];
                        int s2 = tmpBuf[tmpRow + x + off3];
                        int s3 = tmpBuf[tmpRow + x - off3];
                        int secTap = 2 - k;
                        sum += secTap * Constrain(s0 - px, secStrength, secShift);
                        sum += secTap * Constrain(s1 - px, secStrength, secShift);
                        sum += secTap * Constrain(s2 - px, secStrength, secShift);
                        sum += secTap * Constrain(s3 - px, secStrength, secShift);
                        min = UMin(s0, min);
                        max = Math.Max(s0, max);
                        min = UMin(s1, min);
                        max = Math.Max(s1, max);
                        min = UMin(s2, min);
                        max = Math.Max(s2, max);
                        min = UMin(s3, min);
                        max = Math.Max(s3, max);
                    }
                }

                int value = px + ((sum - (sum < 0 ? 1 : 0) + 8) >> 4);
                if (priStrength != 0 && secStrength != 0)
                {
                    value = Math.Clamp(value, min, max);
                }

                dst[dstOffset + (y * dstStride) + x] = (ushort)value;
            }
        }
    }

    private static void Padding(
        int[] tmp,
        int center,
        ReadOnlySpan<ushort> dst,
        int dstOffset,
        int dstStride,
        ReadOnlySpan<ushort> left,
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> bottom,
        int w,
        int h,
        EdgeFlags edges)
    {
        for (int i = 0; i < tmp.Length; i++)
        {
            tmp[i] = VeryLarge;
        }

        int xStart = -2;
        int xEnd = w + 2;
        int yStart = -2;
        int yEnd = h + 2;
        if ((edges & EdgeFlags.Top) == 0)
        {
            yStart = 0;
        }

        if ((edges & EdgeFlags.Bottom) == 0)
        {
            yEnd -= 2;
        }

        if ((edges & EdgeFlags.Left) == 0)
        {
            xStart = 0;
        }

        if ((edges & EdgeFlags.Right) == 0)
        {
            xEnd -= 2;
        }

        // top rows.
        for (int y = yStart; y < 0; y++)
        {
            for (int x = xStart; x < xEnd; x++)
            {
                tmp[center + (y * TmpStride) + x] = top[((y + 2) * (w + 4)) + x + 2];
            }
        }

        // left columns.
        for (int y = 0; y < h; y++)
        {
            for (int x = xStart; x < 0; x++)
            {
                tmp[center + (y * TmpStride) + x] = left[(y * 2) + x + 2];
            }
        }

        // body.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < xEnd; x++)
            {
                tmp[center + (y * TmpStride) + x] = dst[dstOffset + (y * dstStride) + x];
            }
        }

        // bottom rows.
        for (int y = h; y < yEnd; y++)
        {
            for (int x = xStart; x < xEnd; x++)
            {
                tmp[center + (y * TmpStride) + x] = bottom[((y - h) * (w + 4)) + x + 2];
            }
        }
    }

    /// <summary>
    /// Scales the primary luma strength by the block variance (a port of dav1d's
    /// <c>adjust_strength</c>); blocks with little variance receive a weaker filter.
    /// </summary>
    /// <param name="strength">The primary strength.</param>
    /// <param name="variance">The block variance from <see cref="FindDirection"/>.</param>
    /// <returns>The variance-adjusted strength.</returns>
    public static int AdjustStrength(int strength, int variance)
    {
        if (variance == 0)
        {
            return 0;
        }

        int i = (variance >> 6) != 0 ? Math.Min(Log2(variance >> 6), 12) : 0;
        return ((strength * (4 + i)) + 8) >> 4;
    }

    private static int Constrain(int diff, int threshold, int shift)
    {
        int adiff = Math.Abs(diff);
        int v = Math.Min(adiff, Math.Max(0, threshold - (adiff >> shift)));
        return diff < 0 ? -v : v;
    }

    private static int UMin(int a, int b) => (uint)a < (uint)b ? a : b;

    private static int Log2(int value) => value <= 0 ? 0 : BitOperations.Log2((uint)value);
}
