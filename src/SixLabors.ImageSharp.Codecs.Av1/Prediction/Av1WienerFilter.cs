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
    public static void FilterHorizontal(Span<ushort> dst, ReadOnlySpan<byte> src, int srcOffset, ReadOnlySpan<byte> left, ReadOnlySpan<short> fh, int w, EdgeFlags edges)
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
    public static void FilterVertical(Span<byte> dst, int dstOffset, ushort[][] rows, ReadOnlySpan<short> fv, int w)
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

            dst[dstOffset + i] = (byte)Math.Clamp((sum + roundingOffV) >> roundBitsV, 0, 255);
        }
    }
}
