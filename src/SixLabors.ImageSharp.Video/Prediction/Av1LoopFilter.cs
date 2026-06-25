// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The deblocking loop filter sample primitive (specification section 7.14.6), a port of dav1d's
/// <c>loop_filter</c> for 8-bit samples. A single call filters four consecutive lines across one block
/// edge; the surrounding driver selects the filter width and thresholds.
/// </summary>
internal static class Av1LoopFilter
{
    /// <summary>
    /// Filters four lines across a single block edge in place. The samples perpendicular to the edge are
    /// addressed via <paramref name="strideB"/> (p-side at negative offsets, q-side at non-negative); the
    /// step to the next line is <paramref name="strideA"/>.
    /// </summary>
    /// <param name="dst">The plane sample buffer.</param>
    /// <param name="offset">The offset of the first q-side sample (q0) on the first line.</param>
    /// <param name="strideA">The step between the four filtered lines.</param>
    /// <param name="strideB">The step between samples across the edge.</param>
    /// <param name="e">The outer edge limit (blimit).</param>
    /// <param name="i">The inner limit.</param>
    /// <param name="h">The high-edge-variance threshold.</param>
    /// <param name="wd">The filter width (4, 6, 8 or 16).</param>
    public static void FilterEdge(Span<byte> dst, int offset, int strideA, int strideB, int e, int i, int h, int wd)
    {
        const int f = 1; // 1 << (bitdepth - 8), 8-bit.

        for (int line = 0; line < 4; line++, offset += strideA)
        {
            int p1 = dst[offset + (strideB * -2)];
            int p0 = dst[offset + (strideB * -1)];
            int q0 = dst[offset];
            int q1 = dst[offset + strideB];
            int p2 = 0, p3 = 0, q2 = 0, q3 = 0;
            int p4 = 0, p5 = 0, p6 = 0, q4 = 0, q5 = 0, q6 = 0;

            bool fm = Math.Abs(p1 - p0) <= i && Math.Abs(q1 - q0) <= i &&
                      (Math.Abs(p0 - q0) * 2) + (Math.Abs(p1 - q1) >> 1) <= e;

            if (wd > 4)
            {
                p2 = dst[offset + (strideB * -3)];
                q2 = dst[offset + (strideB * +2)];
                fm &= Math.Abs(p2 - p1) <= i && Math.Abs(q2 - q1) <= i;

                if (wd > 6)
                {
                    p3 = dst[offset + (strideB * -4)];
                    q3 = dst[offset + (strideB * +3)];
                    fm &= Math.Abs(p3 - p2) <= i && Math.Abs(q3 - q2) <= i;
                }
            }

            if (!fm)
            {
                continue;
            }

            bool flat8out = false;
            bool flat8in = false;

            if (wd >= 16)
            {
                p6 = dst[offset + (strideB * -7)];
                p5 = dst[offset + (strideB * -6)];
                p4 = dst[offset + (strideB * -5)];
                q4 = dst[offset + (strideB * +4)];
                q5 = dst[offset + (strideB * +5)];
                q6 = dst[offset + (strideB * +6)];

                flat8out = Math.Abs(p6 - p0) <= f && Math.Abs(p5 - p0) <= f &&
                           Math.Abs(p4 - p0) <= f && Math.Abs(q4 - q0) <= f &&
                           Math.Abs(q5 - q0) <= f && Math.Abs(q6 - q0) <= f;
            }

            if (wd >= 6)
            {
                flat8in = Math.Abs(p2 - p0) <= f && Math.Abs(p1 - p0) <= f &&
                          Math.Abs(q1 - q0) <= f && Math.Abs(q2 - q0) <= f;
            }

            if (wd >= 8)
            {
                flat8in &= Math.Abs(p3 - p0) <= f && Math.Abs(q3 - q0) <= f;
            }

            if (wd >= 16 && flat8out && flat8in)
            {
                dst[offset + (strideB * -6)] = (byte)((p6 + p6 + p6 + p6 + p6 + (p6 * 2) + (p5 * 2) + (p4 * 2) + p3 + p2 + p1 + p0 + q0 + 8) >> 4);
                dst[offset + (strideB * -5)] = (byte)((p6 + p6 + p6 + p6 + p6 + (p5 * 2) + (p4 * 2) + (p3 * 2) + p2 + p1 + p0 + q0 + q1 + 8) >> 4);
                dst[offset + (strideB * -4)] = (byte)((p6 + p6 + p6 + p6 + p5 + (p4 * 2) + (p3 * 2) + (p2 * 2) + p1 + p0 + q0 + q1 + q2 + 8) >> 4);
                dst[offset + (strideB * -3)] = (byte)((p6 + p6 + p6 + p5 + p4 + (p3 * 2) + (p2 * 2) + (p1 * 2) + p0 + q0 + q1 + q2 + q3 + 8) >> 4);
                dst[offset + (strideB * -2)] = (byte)((p6 + p6 + p5 + p4 + p3 + (p2 * 2) + (p1 * 2) + (p0 * 2) + q0 + q1 + q2 + q3 + q4 + 8) >> 4);
                dst[offset + (strideB * -1)] = (byte)((p6 + p5 + p4 + p3 + p2 + (p1 * 2) + (p0 * 2) + (q0 * 2) + q1 + q2 + q3 + q4 + q5 + 8) >> 4);
                dst[offset + (strideB * +0)] = (byte)((p5 + p4 + p3 + p2 + p1 + (p0 * 2) + (q0 * 2) + (q1 * 2) + q2 + q3 + q4 + q5 + q6 + 8) >> 4);
                dst[offset + (strideB * +1)] = (byte)((p4 + p3 + p2 + p1 + p0 + (q0 * 2) + (q1 * 2) + (q2 * 2) + q3 + q4 + q5 + q6 + q6 + 8) >> 4);
                dst[offset + (strideB * +2)] = (byte)((p3 + p2 + p1 + p0 + q0 + (q1 * 2) + (q2 * 2) + (q3 * 2) + q4 + q5 + q6 + q6 + q6 + 8) >> 4);
                dst[offset + (strideB * +3)] = (byte)((p2 + p1 + p0 + q0 + q1 + (q2 * 2) + (q3 * 2) + (q4 * 2) + q5 + q6 + q6 + q6 + q6 + 8) >> 4);
                dst[offset + (strideB * +4)] = (byte)((p1 + p0 + q0 + q1 + q2 + (q3 * 2) + (q4 * 2) + (q5 * 2) + q6 + q6 + q6 + q6 + q6 + 8) >> 4);
                dst[offset + (strideB * +5)] = (byte)((p0 + q0 + q1 + q2 + q3 + (q4 * 2) + (q5 * 2) + (q6 * 2) + q6 + q6 + q6 + q6 + q6 + 8) >> 4);
            }
            else if (wd >= 8 && flat8in)
            {
                dst[offset + (strideB * -3)] = (byte)((p3 + p3 + p3 + (2 * p2) + p1 + p0 + q0 + 4) >> 3);
                dst[offset + (strideB * -2)] = (byte)((p3 + p3 + p2 + (2 * p1) + p0 + q0 + q1 + 4) >> 3);
                dst[offset + (strideB * -1)] = (byte)((p3 + p2 + p1 + (2 * p0) + q0 + q1 + q2 + 4) >> 3);
                dst[offset + (strideB * +0)] = (byte)((p2 + p1 + p0 + (2 * q0) + q1 + q2 + q3 + 4) >> 3);
                dst[offset + (strideB * +1)] = (byte)((p1 + p0 + q0 + (2 * q1) + q2 + q3 + q3 + 4) >> 3);
                dst[offset + (strideB * +2)] = (byte)((p0 + q0 + q1 + (2 * q2) + q3 + q3 + q3 + 4) >> 3);
            }
            else if (wd == 6 && flat8in)
            {
                dst[offset + (strideB * -2)] = (byte)((p2 + (2 * p2) + (2 * p1) + (2 * p0) + q0 + 4) >> 3);
                dst[offset + (strideB * -1)] = (byte)((p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1 + 4) >> 3);
                dst[offset + (strideB * +0)] = (byte)((p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2 + 4) >> 3);
                dst[offset + (strideB * +1)] = (byte)((p0 + (2 * q0) + (2 * q1) + (2 * q2) + q2 + 4) >> 3);
            }
            else
            {
                bool hev = Math.Abs(p1 - p0) > h || Math.Abs(q1 - q0) > h;
                if (hev)
                {
                    int diff = ClipDiff(p1 - q1);
                    diff = ClipDiff((3 * (q0 - p0)) + diff);
                    int f1 = Math.Min(diff + 4, 127) >> 3;
                    int f2 = Math.Min(diff + 3, 127) >> 3;
                    dst[offset + (strideB * -1)] = ClipPixel(p0 + f2);
                    dst[offset + (strideB * +0)] = ClipPixel(q0 - f1);
                }
                else
                {
                    int diff = ClipDiff(3 * (q0 - p0));
                    int f1 = Math.Min(diff + 4, 127) >> 3;
                    int f2 = Math.Min(diff + 3, 127) >> 3;
                    dst[offset + (strideB * -1)] = ClipPixel(p0 + f2);
                    dst[offset + (strideB * +0)] = ClipPixel(q0 - f1);

                    int fr = (f1 + 1) >> 1;
                    dst[offset + (strideB * -2)] = ClipPixel(p1 + fr);
                    dst[offset + (strideB * +1)] = ClipPixel(q1 - fr);
                }
            }
        }
    }

    private static int ClipDiff(int v) => v < -128 ? -128 : v > 127 ? 127 : v;

    private static byte ClipPixel(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
}
