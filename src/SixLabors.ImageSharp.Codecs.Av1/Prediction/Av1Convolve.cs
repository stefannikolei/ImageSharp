// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The 8-tap sub-pixel motion-compensation convolution (specification section 7.11.3), a port of dav1d's
/// <c>put_8tap_c</c> for 8-bit samples. Produces a full-block translational inter prediction from a
/// reference plane given the sub-pixel offsets (mx, my) in sixteenths and the 2D filter type. The caller
/// supplies a source pointer whose surrounding 3-sample border is valid (the reference frame's edge
/// extension), matching dav1d's convolution buffer.
/// </summary>
internal static class Av1Convolve
{
    private static readonly sbyte[][][] SubpelFilters =
    [
        [
            [0, 1, -3, 63, 4, -1, 0, 0],
            [0, 1, -5, 61, 9, -2, 0, 0],
            [0, 1, -6, 58, 14, -4, 1, 0],
            [0, 1, -7, 55, 19, -5, 1, 0],
            [0, 1, -7, 51, 24, -6, 1, 0],
            [0, 1, -8, 47, 29, -6, 1, 0],
            [0, 1, -7, 42, 33, -6, 1, 0],
            [0, 1, -7, 38, 38, -7, 1, 0],
            [0, 1, -6, 33, 42, -7, 1, 0],
            [0, 1, -6, 29, 47, -8, 1, 0],
            [0, 1, -6, 24, 51, -7, 1, 0],
            [0, 1, -5, 19, 55, -7, 1, 0],
            [0, 1, -4, 14, 58, -6, 1, 0],
            [0, 0, -2, 9, 61, -5, 1, 0],
            [0, 0, -1, 4, 63, -3, 1, 0],
        ],
        [
            [0, 1, 14, 31, 17, 1, 0, 0],
            [0, 0, 13, 31, 18, 2, 0, 0],
            [0, 0, 11, 31, 20, 2, 0, 0],
            [0, 0, 10, 30, 21, 3, 0, 0],
            [0, 0, 9, 29, 22, 4, 0, 0],
            [0, 0, 8, 28, 23, 5, 0, 0],
            [0, -1, 8, 27, 24, 6, 0, 0],
            [0, -1, 7, 26, 26, 7, -1, 0],
            [0, 0, 6, 24, 27, 8, -1, 0],
            [0, 0, 5, 23, 28, 8, 0, 0],
            [0, 0, 4, 22, 29, 9, 0, 0],
            [0, 0, 3, 21, 30, 10, 0, 0],
            [0, 0, 2, 20, 31, 11, 0, 0],
            [0, 0, 2, 18, 31, 13, 0, 0],
            [0, 0, 1, 17, 31, 14, 1, 0],
        ],
        [
            [-1, 1, -3, 63, 4, -1, 1, 0],
            [-1, 3, -6, 62, 8, -3, 2, -1],
            [-1, 4, -9, 60, 13, -5, 3, -1],
            [-2, 5, -11, 58, 19, -7, 3, -1],
            [-2, 5, -11, 54, 24, -9, 4, -1],
            [-2, 5, -12, 50, 30, -10, 4, -1],
            [-2, 5, -12, 45, 35, -11, 5, -1],
            [-2, 6, -12, 40, 40, -12, 6, -2],
            [-1, 5, -11, 35, 45, -12, 5, -2],
            [-1, 4, -10, 30, 50, -12, 5, -2],
            [-1, 4, -9, 24, 54, -11, 5, -2],
            [-1, 3, -7, 19, 58, -11, 5, -2],
            [-1, 3, -5, 13, 60, -9, 4, -1],
            [-1, 2, -3, 8, 62, -6, 3, -1],
            [0, 1, -1, 4, 63, -3, 1, -1],
        ],
        [
            [0, 0, -2, 63, 4, -1, 0, 0],
            [0, 0, -4, 61, 9, -2, 0, 0],
            [0, 0, -5, 58, 14, -3, 0, 0],
            [0, 0, -6, 55, 19, -4, 0, 0],
            [0, 0, -6, 51, 24, -5, 0, 0],
            [0, 0, -7, 47, 29, -5, 0, 0],
            [0, 0, -6, 42, 33, -5, 0, 0],
            [0, 0, -6, 38, 38, -6, 0, 0],
            [0, 0, -5, 33, 42, -6, 0, 0],
            [0, 0, -5, 29, 47, -7, 0, 0],
            [0, 0, -5, 24, 51, -6, 0, 0],
            [0, 0, -4, 19, 55, -6, 0, 0],
            [0, 0, -3, 14, 58, -5, 0, 0],
            [0, 0, -2, 9, 61, -4, 0, 0],
            [0, 0, -1, 4, 63, -2, 0, 0],
        ],
        [
            [0, 0, 15, 31, 17, 1, 0, 0],
            [0, 0, 13, 31, 18, 2, 0, 0],
            [0, 0, 11, 31, 20, 2, 0, 0],
            [0, 0, 10, 30, 21, 3, 0, 0],
            [0, 0, 9, 29, 22, 4, 0, 0],
            [0, 0, 8, 28, 23, 5, 0, 0],
            [0, 0, 7, 27, 24, 6, 0, 0],
            [0, 0, 6, 26, 26, 6, 0, 0],
            [0, 0, 6, 24, 27, 7, 0, 0],
            [0, 0, 5, 23, 28, 8, 0, 0],
            [0, 0, 4, 22, 29, 9, 0, 0],
            [0, 0, 3, 21, 30, 10, 0, 0],
            [0, 0, 2, 20, 31, 11, 0, 0],
            [0, 0, 2, 18, 31, 13, 0, 0],
            [0, 0, 1, 17, 31, 15, 0, 0],
        ],
        [
            [0, 0, 0, 60, 4, 0, 0, 0],
            [0, 0, 0, 56, 8, 0, 0, 0],
            [0, 0, 0, 52, 12, 0, 0, 0],
            [0, 0, 0, 48, 16, 0, 0, 0],
            [0, 0, 0, 44, 20, 0, 0, 0],
            [0, 0, 0, 40, 24, 0, 0, 0],
            [0, 0, 0, 36, 28, 0, 0, 0],
            [0, 0, 0, 32, 32, 0, 0, 0],
            [0, 0, 0, 28, 36, 0, 0, 0],
            [0, 0, 0, 24, 40, 0, 0, 0],
            [0, 0, 0, 20, 44, 0, 0, 0],
            [0, 0, 0, 16, 48, 0, 0, 0],
            [0, 0, 0, 12, 52, 0, 0, 0],
            [0, 0, 0, 8, 56, 0, 0, 0],
            [0, 0, 0, 4, 60, 0, 0, 0],
        ],
    ];

    /// <summary>
    /// Predicts one inter block from a reference plane, gathering the source (with clamped edge
    /// extension, dav1d's <c>emu_edge</c>) around the integer reference position and convolving it. The
    /// integer position (dx, dy) and sub-pixel offsets (mx, my) are derived by the caller from the motion
    /// vector; pixels outside the reference plane are replicated from its nearest edge.
    /// </summary>
    /// <param name="dst">The destination samples.</param>
    /// <param name="dstOffset">The offset of the first destination sample.</param>
    /// <param name="dstStride">The destination row stride.</param>
    /// <param name="refPlane">The reference plane samples.</param>
    /// <param name="refWidth">The reference plane width.</param>
    /// <param name="refHeight">The reference plane height.</param>
    /// <param name="refStride">The reference plane row stride.</param>
    /// <param name="dx">The integer reference column of the block's top-left sample.</param>
    /// <param name="dy">The integer reference row of the block's top-left sample.</param>
    /// <param name="w">The block width.</param>
    /// <param name="h">The block height.</param>
    /// <param name="mx">The horizontal sub-pixel offset in sixteenths (0-15).</param>
    /// <param name="my">The vertical sub-pixel offset in sixteenths (0-15).</param>
    /// <param name="filterType">The combined 2D filter type.</param>
    public static void PredictBlock(byte[] dst, int dstOffset, int dstStride, byte[] refPlane, int refWidth, int refHeight, int refStride, int dx, int dy, int w, int h, int mx, int my, int filterType)
    {
        int bw = w + 7;
        int bh = h + 7;
        byte[] buffer = new byte[bw * bh];
        for (int r = 0; r < bh; r++)
        {
            int sy = Clamp(dy - 3 + r, 0, refHeight - 1) * refStride;
            int rowBase = r * bw;
            for (int c = 0; c < bw; c++)
            {
                buffer[rowBase + c] = refPlane[sy + Clamp(dx - 3 + c, 0, refWidth - 1)];
            }
        }

        Predict(dst, dstOffset, dstStride, buffer, (3 * bw) + 3, bw, w, h, mx, my, filterType);
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>
    /// Convolves one inter-prediction block. Filter type encodes the vertical filter in bits 2-3 and the
    /// horizontal filter in bits 0-1 (0 = regular, 1 = smooth, 2 = sharp).
    /// </summary>
    /// <param name="dst">The destination samples.</param>
    /// <param name="dstOffset">The offset of the first destination sample.</param>
    /// <param name="dstStride">The destination row stride.</param>
    /// <param name="src">The reference samples.</param>
    /// <param name="srcOffset">The offset of the block's top-left reference sample.</param>
    /// <param name="srcStride">The reference row stride.</param>
    /// <param name="w">The block width.</param>
    /// <param name="h">The block height.</param>
    /// <param name="mx">The horizontal sub-pixel offset in sixteenths (0-15).</param>
    /// <param name="my">The vertical sub-pixel offset in sixteenths (0-15).</param>
    /// <param name="filterType">The combined 2D filter type.</param>
    public static void Predict(byte[] dst, int dstOffset, int dstStride, byte[] src, int srcOffset, int srcStride, int w, int h, int mx, int my, int filterType)
    {
        const int intermediateBits = 4;
        int intermediateRound = 32 + ((1 << (6 - intermediateBits)) >> 1);

        sbyte[]? fh = mx == 0 ? null : (w > 4 ? SubpelFilters[filterType & 3][mx - 1] : SubpelFilters[3 + (filterType & 1)][mx - 1]);
        sbyte[]? fv = my == 0 ? null : (h > 4 ? SubpelFilters[filterType >> 2][my - 1] : SubpelFilters[3 + ((filterType >> 2) & 1)][my - 1]);

        if (fh != null)
        {
            if (fv != null)
            {
                short[] mid = new short[w * (h + 7)];
                int s = srcOffset - (3 * srcStride);
                for (int r = 0; r < h + 7; r++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        mid[(r * w) + x] = (short)((Filter(src, s + x, fh, 1) + ((1 << (6 - intermediateBits)) >> 1)) >> (6 - intermediateBits));
                    }

                    s += srcStride;
                }

                for (int r = 0; r < h; r++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        dst[dstOffset + (r * dstStride) + x] = ClipPixel((FilterMid(mid, ((r + 3) * w) + x, fv, w) + ((1 << (6 + intermediateBits)) >> 1)) >> (6 + intermediateBits));
                    }
                }
            }
            else
            {
                for (int r = 0; r < h; r++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        dst[dstOffset + (r * dstStride) + x] = ClipPixel((Filter(src, srcOffset + (r * srcStride) + x, fh, 1) + intermediateRound) >> 6);
                    }
                }
            }
        }
        else if (fv != null)
        {
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    dst[dstOffset + (r * dstStride) + x] = ClipPixel((Filter(src, srcOffset + (r * srcStride) + x, fv, srcStride) + 32) >> 6);
                }
            }
        }
        else
        {
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    dst[dstOffset + (r * dstStride) + x] = src[srcOffset + (r * srcStride) + x];
                }
            }
        }
    }

    private static int Filter(byte[] src, int x, sbyte[] f, int stride)
        => (f[0] * src[x - (3 * stride)]) + (f[1] * src[x - (2 * stride)]) + (f[2] * src[x - stride]) +
           (f[3] * src[x]) + (f[4] * src[x + stride]) + (f[5] * src[x + (2 * stride)]) +
           (f[6] * src[x + (3 * stride)]) + (f[7] * src[x + (4 * stride)]);

    private static int FilterMid(short[] src, int x, sbyte[] f, int stride)
        => (f[0] * src[x - (3 * stride)]) + (f[1] * src[x - (2 * stride)]) + (f[2] * src[x - stride]) +
           (f[3] * src[x]) + (f[4] * src[x + stride]) + (f[5] * src[x + (2 * stride)]) +
           (f[6] * src[x + (3 * stride)]) + (f[7] * src[x + (4 * stride)]);

    private static byte ClipPixel(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
}
