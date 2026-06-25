// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The self-guided (SGR) loop-restoration filter (specification section 7.17.4), a port of dav1d's
/// <c>sgr_5x5</c> / <c>sgr_3x3</c> / <c>sgr_mix</c> for 8-bit samples. A single stripe of one restoration
/// unit is filtered: the interior rows are read from the CDEF-filtered plane while the two rows on either
/// side of the stripe come from the deblocked (pre-CDEF) plane, matching the AV1 stripe-boundary rule.
/// </summary>
internal static class Av1SelfGuidedFilter
{
    // dav1d_sgr_x_by_x: the reciprocal lookup used when inverting the box-filter denominator.
    private static readonly byte[] XByX =
    [
        255, 128, 85, 64, 51, 43, 37, 32, 28, 26, 23, 21, 20, 18, 17,
        16, 15, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 9, 9,
        8, 8, 8, 8, 7, 7, 7, 7, 7, 6, 6, 6, 6, 6, 6,
        6, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0,
    ];

    /// <summary>
    /// Applies the self-guided filter to one stripe of a restoration unit (radius-2, radius-1 or mixed).
    /// </summary>
    /// <param name="dst">The destination plane samples (initially the CDEF output), modified in place.</param>
    /// <param name="cdef">A read-only snapshot of the CDEF-filtered plane (interior source rows).</param>
    /// <param name="deblock">A read-only snapshot of the deblocked, pre-CDEF plane (stripe-boundary rows).</param>
    /// <param name="planeWidth">The plane width in samples.</param>
    /// <param name="x0">The unit's left column.</param>
    /// <param name="unitWidth">The unit width in samples.</param>
    /// <param name="stripeTop">The first row of the stripe.</param>
    /// <param name="stripeEnd">One past the last row of the stripe.</param>
    /// <param name="haveTop">Whether a stripe exists above (else the top row is replicated).</param>
    /// <param name="haveBottom">Whether a stripe exists below (else the bottom row is replicated).</param>
    /// <param name="haveLeft">Whether a unit exists to the left.</param>
    /// <param name="haveRight">Whether a unit exists to the right.</param>
    /// <param name="s0">The radius-2 strength (0 disables the radius-2 pass).</param>
    /// <param name="s1">The radius-1 strength (0 disables the radius-1 pass).</param>
    /// <param name="w0">The radius-2 projection weight.</param>
    /// <param name="w1">The radius-1 projection weight.</param>
    public static void Stripe(
        byte[] dst, byte[] cdef, byte[] deblock, int planeWidth,
        int x0, int unitWidth, int stripeTop, int stripeEnd,
        bool haveTop, bool haveBottom, bool haveLeft, bool haveRight, int s0, int s1, int w0, int w1)
    {
        int n = unitWidth + 2; // A/B width; index j maps to column x = x0 - 1 + j.
        bool useBox5 = s0 != 0;
        bool useBox3 = s1 != 0;

        int SrcPix(int ri, int x)
        {
            byte[] buf;
            int row;
            if (ri >= stripeTop && ri < stripeEnd)
            {
                buf = cdef;
                row = ri;
            }
            else if (ri < stripeTop)
            {
                if (!haveTop)
                {
                    buf = cdef;
                    row = stripeTop;
                }
                else
                {
                    buf = deblock;
                    row = ri < stripeTop - 2 ? stripeTop - 2 : ri;
                }
            }
            else
            {
                if (!haveBottom)
                {
                    buf = cdef;
                    row = stripeEnd - 1;
                }
                else
                {
                    buf = deblock;
                    row = ri > stripeEnd + 1 ? stripeEnd + 1 : ri;
                }
            }

            int ax = x;
            if (!haveLeft && ax < x0)
            {
                ax = x0;
            }

            if (!haveRight && ax >= x0 + unitWidth)
            {
                ax = x0 + unitWidth - 1;
            }

            if (ax < 0)
            {
                ax = 0;
            }
            else if (ax >= planeWidth)
            {
                ax = planeWidth - 1;
            }

            return buf[(row * planeWidth) + ax];
        }

        int rowTop = stripeTop - 3;
        int rowBottom = stripeEnd + 1;
        int rowCount = rowBottom - rowTop + 1;
        int[][] h5Sum = useBox5 ? new int[rowCount][] : null!;
        int[][] h5SumSq = useBox5 ? new int[rowCount][] : null!;
        int[][] h3Sum = useBox3 ? new int[rowCount][] : null!;
        int[][] h3SumSq = useBox3 ? new int[rowCount][] : null!;
        for (int ri = rowTop; ri <= rowBottom; ri++)
        {
            int[] h5s = useBox5 ? new int[n] : null!;
            int[] h5q = useBox5 ? new int[n] : null!;
            int[] h3s = useBox3 ? new int[n] : null!;
            int[] h3q = useBox3 ? new int[n] : null!;
            for (int j = 0; j < n; j++)
            {
                int x = x0 - 1 + j;
                if (useBox5)
                {
                    int sum = 0;
                    int sumSq = 0;
                    for (int t = -2; t <= 2; t++)
                    {
                        int v = SrcPix(ri, x + t);
                        sum += v;
                        sumSq += v * v;
                    }

                    h5s[j] = sum;
                    h5q[j] = sumSq;
                }

                if (useBox3)
                {
                    int sum = 0;
                    int sumSq = 0;
                    for (int t = -1; t <= 1; t++)
                    {
                        int v = SrcPix(ri, x + t);
                        sum += v;
                        sumSq += v * v;
                    }

                    h3s[j] = sum;
                    h3q[j] = sumSq;
                }
            }

            if (useBox5)
            {
                h5Sum[ri - rowTop] = h5s;
                h5SumSq[ri - rowTop] = h5q;
            }

            if (useBox3)
            {
                h3Sum[ri - rowTop] = h3s;
                h3SumSq[ri - rowTop] = h3q;
            }
        }

        int height = stripeEnd - stripeTop;

        // box5 a/b at odd-offset centre rows (stripeTop-1, +1, +3, ...).
        int box5Count = (height / 2) + 1;
        int[][] a5 = useBox5 ? new int[box5Count][] : null!;
        int[][] b5 = useBox5 ? new int[box5Count][] : null!;
        if (useBox5)
        {
            for (int idx = 0; idx < box5Count; idx++)
            {
                int c = stripeTop - 1 + (2 * idx);
                int[] a = new int[n];
                int[] b = new int[n];
                for (int j = 0; j < n; j++)
                {
                    int sumV = 0;
                    int sumSqV = 0;
                    for (int k = c - 2; k <= c + 2; k++)
                    {
                        sumV += h5Sum[k - rowTop][j];
                        sumSqV += h5SumSq[k - rowTop][j];
                    }

                    CalcAb(sumSqV, sumV, 25, s0, 164, out a[j], out b[j]);
                }

                a5[idx] = a;
                b5[idx] = b;
            }
        }

        // box3 a/b at every centre row from stripeTop-1 to stripeEnd.
        int box3First = stripeTop - 1;
        int[][] a3 = useBox3 ? new int[height + 2][] : null!;
        int[][] b3 = useBox3 ? new int[height + 2][] : null!;
        if (useBox3)
        {
            for (int c = box3First; c <= stripeEnd; c++)
            {
                int[] a = new int[n];
                int[] b = new int[n];
                for (int j = 0; j < n; j++)
                {
                    int sumV = 0;
                    int sumSqV = 0;
                    for (int k = c - 1; k <= c + 1; k++)
                    {
                        sumV += h3Sum[k - rowTop][j];
                        sumSqV += h3SumSq[k - rowTop][j];
                    }

                    CalcAb(sumSqV, sumV, 9, s1, 455, out a[j], out b[j]);
                }

                a3[c - box3First] = a;
                b3[c - box3First] = b;
            }
        }

        for (int r = stripeTop; r < stripeEnd; r++)
        {
            int rowBase = r * planeWidth;
            for (int x = x0; x < x0 + unitWidth; x++)
            {
                int j = x - x0 + 1;
                int src = cdef[rowBase + x];
                int weighted = 0;

                if (useBox5)
                {
                    int tmp5;
                    if (((r - stripeTop) & 1) == 0)
                    {
                        int i0 = (r - stripeTop) / 2;
                        int i1 = i0 + 1;
                        int[] pa0 = a5[i0], pa1 = a5[i1], pb0 = b5[i0], pb1 = b5[i1];
                        int aTerm = ((pb0[j] + pb1[j]) * 6) + ((pb0[j - 1] + pb1[j - 1] + pb0[j + 1] + pb1[j + 1]) * 5);
                        int bTerm = ((pa0[j] + pa1[j]) * 6) + ((pa0[j - 1] + pa1[j - 1] + pa0[j + 1] + pa1[j + 1]) * 5);
                        tmp5 = (bTerm - (aTerm * src) + (1 << 8)) >> 9;
                    }
                    else
                    {
                        int i = (r - (stripeTop - 1)) / 2;
                        int[] pa = a5[i], pb = b5[i];
                        int aTerm = (pb[j] * 6) + ((pb[j - 1] + pb[j + 1]) * 5);
                        int bTerm = (pa[j] * 6) + ((pa[j - 1] + pa[j + 1]) * 5);
                        tmp5 = (bTerm - (aTerm * src) + (1 << 7)) >> 8;
                    }

                    weighted += w0 * tmp5;
                }

                if (useBox3)
                {
                    int idxC = r - box3First;
                    int[] pa0 = a3[idxC - 1], pa1 = a3[idxC], pa2 = a3[idxC + 1];
                    int[] pb0 = b3[idxC - 1], pb1 = b3[idxC], pb2 = b3[idxC + 1];
                    int aTerm = ((pb1[j] + pb1[j - 1] + pb1[j + 1] + pb0[j] + pb2[j]) * 4) + ((pb0[j - 1] + pb2[j - 1] + pb0[j + 1] + pb2[j + 1]) * 3);
                    int bTerm = ((pa1[j] + pa1[j - 1] + pa1[j + 1] + pa0[j] + pa2[j]) * 4) + ((pa0[j - 1] + pa2[j - 1] + pa0[j + 1] + pa2[j + 1]) * 3);
                    int tmp3 = (bTerm - (aTerm * src) + (1 << 8)) >> 9;
                    weighted += w1 * tmp3;
                }

                dst[rowBase + x] = Clip255(src + ((weighted + (1 << 10)) >> 11));
            }
        }
    }

    private static void CalcAb(int sumSq, int sum, int n, int s, int oneByX, out int a, out int b)
    {
        long p = ((long)sumSq * n) - ((long)sum * sum);
        if (p < 0)
        {
            p = 0;
        }

        long z = ((p * s) + (1 << 19)) >> 20;
        int x = XByX[(int)Math.Min(z, 255)];
        a = (int)((((long)x * sum * oneByX) + (1 << 11)) >> 12);
        b = x;
    }

    private static byte Clip255(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
}
