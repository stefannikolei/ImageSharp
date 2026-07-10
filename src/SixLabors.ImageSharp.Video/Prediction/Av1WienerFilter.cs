// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The Wiener loop-restoration filter (specification section 7.17.3), a port of dav1d's separable
/// <c>wiener_filter_h</c> / <c>wiener_filter_v</c> kernels for 8-bit samples. The horizontal pass
/// produces a rounded 16-bit intermediate row; the vertical pass combines seven such rows into the final
/// restored sample. The surrounding restoration-unit driver (row-window management with top/bottom edge
/// extension) builds on these kernels.
/// </summary>
internal static class Av1WienerFilter
{
    /// <summary>Edge availability flags for the restoration unit.</summary>
    [Flags]
    public enum EdgeFlags
    {
        /// <summary>The left neighbour is available.</summary>
        Left = 1 << 0,

        /// <summary>The right neighbour is available.</summary>
        Right = 1 << 1,
    }

    /// <summary>
    /// Applies the horizontal Wiener pass to a single row (a port of dav1d's <c>wiener_filter_h</c> for
    /// 8-bit). Produces a rounded 16-bit intermediate value per sample. When the right edge is available
    /// the kernel reads up to three samples past the row (<paramref name="src"/> must include them, as in
    /// dav1d's restoration-unit buffer); otherwise the rightmost sample is replicated.
    /// </summary>
    /// <param name="dst">The intermediate output row (length at least <paramref name="w"/>).</param>
    /// <param name="src">The source samples.</param>
    /// <param name="srcOffset">The offset of the row's first sample.</param>
    /// <param name="left">The two-or-more left-neighbour samples (indexed [4 + idx] as in dav1d), or null.</param>
    /// <param name="fh">The seven horizontal filter taps.</param>
    /// <param name="w">The row width.</param>
    /// <param name="edges">The available horizontal edges.</param>
    public static void FilterHorizontal(Span<ushort> dst, ReadOnlySpan<ushort> src, int srcOffset, ReadOnlySpan<ushort> left, ReadOnlySpan<short> fh, int w, EdgeFlags edges)
    {
        const int bitdepth = 8;
        const int roundBitsH = 3;
        const int roundingOffH = 1 << (roundBitsH - 1);
        const int clipLimit = 1 << (bitdepth + 1 + 7 - roundBitsH);
        bool haveLeft = (edges & EdgeFlags.Left) != 0;
        bool haveRight = (edges & EdgeFlags.Right) != 0;
        bool hasLeftBuffer = left.Length > 0;

        for (int x = 0; x < w; x++)
        {
            int sum = (1 << (bitdepth + 6)) + (src[srcOffset + x] * 128);
            for (int i = 0; i < 7; i++)
            {
                int idx = x + i - 3;
                int sample;
                if (idx < 0)
                {
                    sample = !haveLeft ? src[srcOffset] : hasLeftBuffer ? left[4 + idx] : src[srcOffset + idx];
                }
                else if (idx >= w && !haveRight)
                {
                    sample = src[srcOffset + w - 1];
                }
                else
                {
                    sample = src[srcOffset + idx];
                }

                sum += sample * fh[i];
            }

            dst[x] = (ushort)Math.Clamp((sum + roundingOffH) >> roundBitsH, 0, clipLimit - 1);
        }
    }

    /// <summary>
    /// Applies the vertical Wiener pass for one output row from seven horizontally-filtered rows (a port
    /// of dav1d's <c>wiener_filter_v</c>/<c>hv</c> accumulation for 8-bit).
    /// </summary>
    /// <param name="dst">The restored output samples.</param>
    /// <param name="dstOffset">The offset of the row's first output sample.</param>
    /// <param name="rows">The seven intermediate rows, top to bottom.</param>
    /// <param name="fv">The seven vertical filter taps.</param>
    /// <param name="w">The row width.</param>
    public static void FilterVertical(Span<ushort> dst, int dstOffset, ushort[][] rows, ReadOnlySpan<short> fv, int w)
    {
        const int bitdepth = 8;
        const int roundBitsV = 11;
        const int roundingOffV = 1 << (roundBitsV - 1);
        const int roundOffset = 1 << (bitdepth + (roundBitsV - 1));

        for (int i = 0; i < w; i++)
        {
            int sum = -roundOffset;
            for (int k = 0; k < 7; k++)
            {
                sum += rows[k][i] * fv[k];
            }

            dst[dstOffset + i] = (ushort)Math.Clamp((sum + roundingOffV) >> roundBitsV, 0, 255);
        }
    }

    /// <summary>
    /// Applies the separable Wiener filter to one stripe of a restoration unit. Interior rows are read
    /// from the CDEF-filtered plane; the rows on either side of the stripe come from the deblocked
    /// (pre-CDEF) plane, with the outermost tap replicated, matching the AV1 stripe-boundary rule.
    /// </summary>
    /// <param name="dst">The destination plane samples (initially the CDEF output), modified in place.</param>
    /// <param name="cdef">A read-only snapshot of the CDEF-filtered plane.</param>
    /// <param name="deblock">A read-only snapshot of the deblocked, pre-CDEF plane.</param>
    /// <param name="stride">The plane row stride in samples.</param>
    /// <param name="x0">The unit's left column.</param>
    /// <param name="unitWidth">The unit width in samples.</param>
    /// <param name="stripeTop">The first row of the stripe.</param>
    /// <param name="stripeEnd">One past the last row of the stripe.</param>
    /// <param name="haveTop">Whether a stripe exists above.</param>
    /// <param name="haveBottom">Whether a stripe exists below.</param>
    /// <param name="haveLeft">Whether a unit exists to the left.</param>
    /// <param name="haveRight">Whether a unit exists to the right.</param>
    /// <param name="filterH">The three coded horizontal taps.</param>
    /// <param name="filterV">The three coded vertical taps.</param>
    public static void Stripe(
        ushort[] dst, ushort[] cdef, ushort[] deblock, int stride,
        int x0, int unitWidth, int stripeTop, int stripeEnd,
        bool haveTop, bool haveBottom, bool haveLeft, bool haveRight, int[] filterH, int[] filterV)
    {
        short h0 = (short)filterH[0], h1 = (short)filterH[1], h2 = (short)filterH[2];
        short v0 = (short)filterV[0], v1 = (short)filterV[1], v2 = (short)filterV[2];
        short[] fh = [h0, h1, h2, (short)(-(h0 + h1 + h2) * 2), h2, h1, h0];
        short[] fv = [v0, v1, v2, (short)(128 - ((v0 + v1 + v2) * 2)), v2, v1, v0];

        EdgeFlags edges = (haveLeft ? EdgeFlags.Left : 0) | (haveRight ? EdgeFlags.Right : 0);

        int rowTop = stripeTop - 3;
        int rowBottom = stripeEnd + 2;
        ushort[][] hor = new ushort[rowBottom - rowTop + 1][];
        for (int ri = rowTop; ri <= rowBottom; ri++)
        {
            ushort[] buf;
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

            ushort[] left = [];
            if (haveLeft)
            {
                left = [0, buf[(row * stride) + x0 - 3], buf[(row * stride) + x0 - 2], buf[(row * stride) + x0 - 1]];
            }

            ushort[] hr = new ushort[unitWidth];
            FilterHorizontal(hr, buf, (row * stride) + x0, left, fh, unitWidth, edges);
            hor[ri - rowTop] = hr;
        }

        ushort[][] rows7 = new ushort[7][];
        for (int r = stripeTop; r < stripeEnd; r++)
        {
            for (int k = 0; k < 7; k++)
            {
                rows7[k] = hor[r - 3 + k - rowTop];
            }

            FilterVertical(dst, (r * stride) + x0, rows7, fv, unitWidth);
        }
    }
}
