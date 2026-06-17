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
    /// Applies the radius-2 (5x5 box) self-guided filter to one stripe of a restoration unit.
    /// </summary>
    /// <param name="dst">The destination plane samples (initially the CDEF output), modified in place.</param>
    /// <param name="cdef">A read-only snapshot of the CDEF-filtered plane (interior source rows).</param>
    /// <param name="deblock">A read-only snapshot of the deblocked, pre-CDEF plane (stripe-boundary rows).</param>
    /// <param name="planeWidth">The plane width in samples.</param>
    /// <param name="planeHeight">The plane height in samples.</param>
    /// <param name="x0">The unit's left column.</param>
    /// <param name="unitWidth">The unit width in samples.</param>
    /// <param name="stripeTop">The first row of the stripe.</param>
    /// <param name="stripeEnd">One past the last row of the stripe.</param>
    /// <param name="haveTop">Whether a stripe exists above (else the top row is replicated).</param>
    /// <param name="haveBottom">Whether a stripe exists below (else the bottom row is replicated).</param>
    /// <param name="haveLeft">Whether a unit exists to the left.</param>
    /// <param name="haveRight">Whether a unit exists to the right.</param>
    /// <param name="s0">The radius-2 strength parameter.</param>
    /// <param name="w0">The radius-2 projection weight.</param>
    public static void Box5Stripe(
        byte[] dst, byte[] cdef, byte[] deblock, int planeWidth, int planeHeight,
        int x0, int unitWidth, int stripeTop, int stripeEnd,
        bool haveTop, bool haveBottom, bool haveLeft, bool haveRight, int s0, int w0)
    {
        int height = stripeEnd - stripeTop;
        int n = unitWidth + 2; // A/B width; index j maps to column x = x0 - 1 + j.

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

        // Horizontal 5-tap box sums for every source row the vertical box and finish filter need.
        int rowTop = stripeTop - 3;
        int rowBottom = stripeEnd + 1;
        int rowCount = rowBottom - rowTop + 1;
        int[][] hSum = new int[rowCount][];
        int[][] hSumSq = new int[rowCount][];
        for (int ri = rowTop; ri <= rowBottom; ri++)
        {
            int[] hs = new int[n];
            int[] hq = new int[n];
            for (int j = 0; j < n; j++)
            {
                int x = x0 - 1 + j;
                int sum = 0;
                int sumSq = 0;
                for (int t = -2; t <= 2; t++)
                {
                    int v = SrcPix(ri, x + t);
                    sum += v;
                    sumSq += v * v;
                }

                hs[j] = sum;
                hq[j] = sumSq;
            }

            hSum[ri - rowTop] = hs;
            hSumSq[ri - rowTop] = hq;
        }

        // The box5 a/b coefficients are computed at odd-offset centre rows: stripeTop-1, +1, +3, ...
        int centreCount = (height / 2) + 1;
        int[][] aCoef = new int[centreCount][];
        int[][] bCoef = new int[centreCount][];
        for (int idx = 0; idx < centreCount; idx++)
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
                    sumV += hSum[k - rowTop][j];
                    sumSqV += hSumSq[k - rowTop][j];
                }

                long p = ((long)sumSqV * 25) - ((long)sumV * sumV);
                if (p < 0)
                {
                    p = 0;
                }

                long z = ((p * s0) + (1 << 19)) >> 20;
                int xx = XByX[(int)Math.Min(z, 255)];
                a[j] = (int)((((long)xx * sumV * 164) + (1 << 11)) >> 12);
                b[j] = xx;
            }

            aCoef[idx] = a;
            bCoef[idx] = b;
        }

        for (int r = stripeTop; r < stripeEnd; r++)
        {
            int offset = r - stripeTop;
            int rowBase = r * planeWidth;
            if ((offset & 1) == 0)
            {
                int i0 = (r - stripeTop) / 2;
                int i1 = i0 + 1;
                int[] a0 = aCoef[i0], a1 = aCoef[i1], b0 = bCoef[i0], b1 = bCoef[i1];
                for (int x = x0; x < x0 + unitWidth; x++)
                {
                    int j = x - x0 + 1;
                    int aTerm = ((b0[j] + b1[j]) * 6) + ((b0[j - 1] + b1[j - 1] + b0[j + 1] + b1[j + 1]) * 5);
                    int bTerm = ((a0[j] + a1[j]) * 6) + ((a0[j - 1] + a1[j - 1] + a0[j + 1] + a1[j + 1]) * 5);
                    int src = cdef[rowBase + x];
                    int tmp = (bTerm - (aTerm * src) + (1 << 8)) >> 9;
                    dst[rowBase + x] = Clip255(src + (((w0 * tmp) + (1 << 10)) >> 11));
                }
            }
            else
            {
                int i = (r - (stripeTop - 1)) / 2;
                int[] a = aCoef[i], b = bCoef[i];
                for (int x = x0; x < x0 + unitWidth; x++)
                {
                    int j = x - x0 + 1;
                    int aTerm = (b[j] * 6) + ((b[j - 1] + b[j + 1]) * 5);
                    int bTerm = (a[j] * 6) + ((a[j - 1] + a[j + 1]) * 5);
                    int src = cdef[rowBase + x];
                    int tmp = (bTerm - (aTerm * src) + (1 << 7)) >> 8;
                    dst[rowBase + x] = Clip255(src + (((w0 * tmp) + (1 << 10)) >> 11));
                }
            }
        }
    }

    private static byte Clip255(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
}
