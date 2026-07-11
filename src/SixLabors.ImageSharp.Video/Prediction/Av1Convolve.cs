// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The 8-tap sub-pixel motion-compensation convolution (specification section 7.11.3), a port of dav1d's
/// <c>put_8tap_c</c>. Produces a full-block translational inter prediction from a
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
    public static void PredictBlock(ushort[] dst, int dstOffset, int dstStride, ushort[] refPlane, int refWidth, int refHeight, int refStride, int dx, int dy, int w, int h, int mx, int my, int filterType, int bitDepth = 8)
    {
        int bw = w + 7;
        int bh = h + 7;
        ushort[] buffer = new ushort[bw * bh];
        for (int r = 0; r < bh; r++)
        {
            int sy = Clamp(dy - 3 + r, 0, refHeight - 1) * refStride;
            int rowBase = r * bw;
            for (int c = 0; c < bw; c++)
            {
                buffer[rowBase + c] = refPlane[sy + Clamp(dx - 3 + c, 0, refWidth - 1)];
            }
        }

        Predict(dst, dstOffset, dstStride, buffer, (3 * bw) + 3, bw, w, h, mx, my, filterType, bitDepth);
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
    public static void Predict(ushort[] dst, int dstOffset, int dstStride, ushort[] src, int srcOffset, int srcStride, int w, int h, int mx, int my, int filterType, int bitDepth = 8)
    {
        int intermediateBits = IntermediateBits(bitDepth);
        int maxValue = (1 << bitDepth) - 1;
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
                        dst[dstOffset + (r * dstStride) + x] = ClipPixel((FilterMid(mid, ((r + 3) * w) + x, fv, w) + ((1 << (6 + intermediateBits)) >> 1)) >> (6 + intermediateBits), maxValue);
                    }
                }
            }
            else
            {
                for (int r = 0; r < h; r++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        dst[dstOffset + (r * dstStride) + x] = ClipPixel((Filter(src, srcOffset + (r * srcStride) + x, fh, 1) + intermediateRound) >> 6, maxValue);
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
                    dst[dstOffset + (r * dstStride) + x] = ClipPixel((Filter(src, srcOffset + (r * srcStride) + x, fv, srcStride) + 32) >> 6, maxValue);
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

    /// <summary>
    /// Produces the unrounded 16-bit intermediate prediction for one compound (multi-reference) inter
    /// block (a port of dav1d's <c>prep_8tap</c>). The result is combined by one of the blend
    /// operations rather than written to pixels directly.
    /// </summary>
    /// <param name="tmp">The 16-bit intermediate buffer (dense, stride <paramref name="w"/>).</param>
    /// <param name="src">The reference samples.</param>
    /// <param name="srcOffset">The offset of the block's top-left reference sample.</param>
    /// <param name="srcStride">The reference row stride.</param>
    /// <param name="w">The block width.</param>
    /// <param name="h">The block height.</param>
    /// <param name="mx">The horizontal sub-pixel offset in sixteenths (0-15).</param>
    /// <param name="my">The vertical sub-pixel offset in sixteenths (0-15).</param>
    /// <param name="filterType">The combined 2D filter type.</param>
    public static void Prep(short[] tmp, ushort[] src, int srcOffset, int srcStride, int w, int h, int mx, int my, int filterType, int bitDepth = 8)
    {
        int intermediateBits = IntermediateBits(bitDepth);
        int prepBias = bitDepth == 8 ? 0 : 8192;
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
                        tmp[(r * w) + x] = (short)(((FilterMid(mid, ((r + 3) * w) + x, fv, w) + 32) >> 6) - prepBias);
                    }
                }
            }
            else
            {
                for (int r = 0; r < h; r++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        tmp[(r * w) + x] = (short)(((Filter(src, srcOffset + (r * srcStride) + x, fh, 1) + ((1 << (6 - intermediateBits)) >> 1)) >> (6 - intermediateBits)) - prepBias);
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
                    tmp[(r * w) + x] = (short)(((Filter(src, srcOffset + (r * srcStride) + x, fv, srcStride) + ((1 << (6 - intermediateBits)) >> 1)) >> (6 - intermediateBits)) - prepBias);
                }
            }
        }
        else
        {
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    tmp[(r * w) + x] = (short)((src[srcOffset + (r * srcStride) + x] << intermediateBits) - prepBias);
                }
            }
        }
    }

    /// <summary>Gathers a bordered reference block (clamped edge extension) and runs <see cref="Prep"/>.</summary>
    public static void PrepBlock(short[] tmp, ushort[] refPlane, int refWidth, int refHeight, int refStride, int dx, int dy, int w, int h, int mx, int my, int filterType, int bitDepth = 8)
    {
        int bw = w + 7;
        int bh = h + 7;
        ushort[] buffer = new ushort[bw * bh];
        for (int r = 0; r < bh; r++)
        {
            int sy = Clamp(dy - 3 + r, 0, refHeight - 1) * refStride;
            int rowBase = r * bw;
            for (int c = 0; c < bw; c++)
            {
                buffer[rowBase + c] = refPlane[sy + Clamp(dx - 3 + c, 0, refWidth - 1)];
            }
        }

        Prep(tmp, buffer, (3 * bw) + 3, bw, w, h, mx, my, filterType, bitDepth);
    }

    /// <summary>Averages two compound predictions (dav1d <c>avg</c>).</summary>
    public static void Average(ushort[] dst, int dstOffset, int dstStride, short[] tmp1, short[] tmp2, int w, int h, int bitDepth = 8)
    {
        int intermediateBits = IntermediateBits(bitDepth);
        int prepBias = bitDepth == 8 ? 0 : 8192;
        int sh = intermediateBits + 1;
        int rnd = (1 << intermediateBits) + (prepBias * 2);
        int maxValue = (1 << bitDepth) - 1;
        for (int r = 0; r < h; r++)
        {
            for (int x = 0; x < w; x++)
            {
                dst[dstOffset + (r * dstStride) + x] = ClipPixel((tmp1[(r * w) + x] + tmp2[(r * w) + x] + rnd) >> sh, maxValue);
            }
        }
    }

    /// <summary>Weighted-averages two compound predictions with a weight in [0, 16] (dav1d <c>w_avg</c>).</summary>
    public static void WeightedAverage(ushort[] dst, int dstOffset, int dstStride, short[] tmp1, short[] tmp2, int w, int h, int weight, int bitDepth = 8)
    {
        int intermediateBits = IntermediateBits(bitDepth);
        int prepBias = bitDepth == 8 ? 0 : 8192;
        int sh = intermediateBits + 4;
        int rnd = (8 << intermediateBits) + (prepBias * 16);
        int maxValue = (1 << bitDepth) - 1;
        for (int r = 0; r < h; r++)
        {
            for (int x = 0; x < w; x++)
            {
                int t1 = tmp1[(r * w) + x];
                int t2 = tmp2[(r * w) + x];
                dst[dstOffset + (r * dstStride) + x] = ClipPixel(((t1 * weight) + (t2 * (16 - weight)) + rnd) >> sh, maxValue);
            }
        }
    }

    /// <summary>Mask-blends two compound predictions with a per-sample mask in [0, 64] (dav1d <c>mask</c>).</summary>
    public static void Mask(ushort[] dst, int dstOffset, int dstStride, short[] tmp1, short[] tmp2, byte[] mask, int w, int h, int bitDepth = 8)
    {
        int intermediateBits = IntermediateBits(bitDepth);
        int prepBias = bitDepth == 8 ? 0 : 8192;
        int sh = intermediateBits + 6;
        int rnd = (32 << intermediateBits) + (prepBias * 64);
        int maxValue = (1 << bitDepth) - 1;
        for (int r = 0; r < h; r++)
        {
            for (int x = 0; x < w; x++)
            {
                int m = mask[(r * w) + x];
                int t1 = tmp1[(r * w) + x];
                int t2 = tmp2[(r * w) + x];
                dst[dstOffset + (r * dstStride) + x] = ClipPixel(((t1 * m) + (t2 * (64 - m)) + rnd) >> sh, maxValue);
            }
        }
    }

    private static int Filter(ushort[] src, int x, sbyte[] f, int stride)
        => (f[0] * src[x - (3 * stride)]) + (f[1] * src[x - (2 * stride)]) + (f[2] * src[x - stride]) +
           (f[3] * src[x]) + (f[4] * src[x + stride]) + (f[5] * src[x + (2 * stride)]) +
           (f[6] * src[x + (3 * stride)]) + (f[7] * src[x + (4 * stride)]);

    private static int FilterMid(short[] src, int x, sbyte[] f, int stride)
        => (f[0] * src[x - (3 * stride)]) + (f[1] * src[x - (2 * stride)]) + (f[2] * src[x - stride]) +
           (f[3] * src[x]) + (f[4] * src[x + stride]) + (f[5] * src[x + (2 * stride)]) +
           (f[6] * src[x + (3 * stride)]) + (f[7] * src[x + (4 * stride)]);

    /// <summary>
    /// Motion-compensates one block from a reference of a different resolution (a port of dav1d's
    /// <c>put_8tap_scaled_c</c>): the source position advances by a 10-bit fixed-point step per output
    /// sample, with the sub-pixel filter chosen per position. The source window (with its 3-sample
    /// border) is gathered with clamped edge extension first (dav1d's <c>emu_edge</c>).
    /// </summary>
    /// <param name="dst">The destination samples.</param>
    /// <param name="dstOffset">The offset of the first destination sample.</param>
    /// <param name="dstStride">The destination row stride.</param>
    /// <param name="refPlane">The reference plane samples.</param>
    /// <param name="refWidth">The reference plane visible width.</param>
    /// <param name="refHeight">The reference plane visible height.</param>
    /// <param name="refStride">The reference plane row stride.</param>
    /// <param name="posX">The horizontal source position in 1/1024-pel units.</param>
    /// <param name="posY">The vertical source position in 1/1024-pel units.</param>
    /// <param name="stepX">The horizontal source step per output sample in 1/1024-pel units.</param>
    /// <param name="stepY">The vertical source step per output row in 1/1024-pel units.</param>
    /// <param name="w">The block width.</param>
    /// <param name="h">The block height.</param>
    /// <param name="filterType">The combined 2D filter type.</param>
    /// <param name="bitDepth">The stream bit depth.</param>
    public static void PredictScaledBlock(ushort[] dst, int dstOffset, int dstStride, ushort[] refPlane, int refWidth, int refHeight, int refStride, int posX, int posY, int stepX, int stepY, int w, int h, int filterType, int bitDepth = 8)
    {
        ushort[] buffer = GatherScaled(refPlane, refWidth, refHeight, refStride, posX, posY, stepX, stepY, w, h, out int bufStride);
        ScaledCore(dst, dstOffset, dstStride, null, buffer, (3 * bufStride) + 3, bufStride, w, h, posX & 0x3ff, posY & 0x3ff, stepX, stepY, filterType, bitDepth);
    }

    /// <summary>Produces the 16-bit compound intermediate from a scaled reference (dav1d <c>prep_8tap_scaled_c</c>).</summary>
    /// <param name="tmp">The 16-bit intermediate buffer (dense, stride w).</param>
    /// <param name="refPlane">The reference plane samples.</param>
    /// <param name="refWidth">The reference plane visible width.</param>
    /// <param name="refHeight">The reference plane visible height.</param>
    /// <param name="refStride">The reference plane row stride.</param>
    /// <param name="posX">The horizontal source position in 1/1024-pel units.</param>
    /// <param name="posY">The vertical source position in 1/1024-pel units.</param>
    /// <param name="stepX">The horizontal source step per output sample.</param>
    /// <param name="stepY">The vertical source step per output row.</param>
    /// <param name="w">The block width.</param>
    /// <param name="h">The block height.</param>
    /// <param name="filterType">The combined 2D filter type.</param>
    /// <param name="bitDepth">The stream bit depth.</param>
    public static void PrepScaledBlock(short[] tmp, ushort[] refPlane, int refWidth, int refHeight, int refStride, int posX, int posY, int stepX, int stepY, int w, int h, int filterType, int bitDepth = 8)
    {
        ushort[] buffer = GatherScaled(refPlane, refWidth, refHeight, refStride, posX, posY, stepX, stepY, w, h, out int bufStride);
        ScaledCore(null, 0, 0, tmp, buffer, (3 * bufStride) + 3, bufStride, w, h, posX & 0x3ff, posY & 0x3ff, stepX, stepY, filterType, bitDepth);
    }

    private static ushort[] GatherScaled(ushort[] refPlane, int refWidth, int refHeight, int refStride, int posX, int posY, int stepX, int stepY, int w, int h, out int bufStride)
    {
        int left = posX >> 10;
        int top = posY >> 10;
        int right = ((posX + ((w - 1) * stepX)) >> 10) + 1;
        int bottom = ((posY + ((h - 1) * stepY)) >> 10) + 1;
        bufStride = right - left + 7;
        int bufHeight = bottom - top + 7;
        ushort[] buffer = new ushort[bufStride * bufHeight];
        for (int r = 0; r < bufHeight; r++)
        {
            int sy = Clamp(top - 3 + r, 0, refHeight - 1) * refStride;
            int rowBase = r * bufStride;
            for (int c = 0; c < bufStride; c++)
            {
                buffer[rowBase + c] = refPlane[sy + Clamp(left - 3 + c, 0, refWidth - 1)];
            }
        }

        return buffer;
    }

    // The shared scaled convolution: an 8-row ring of horizontally-filtered lines feeds the vertical
    // taps, with per-position filter selection (dav1d put/prep_8tap_scaled_c).
    private static void ScaledCore(ushort[]? dst, int dstOffset, int dstStride, short[]? tmp, ushort[] src, int srcOffset, int srcStride, int w, int h, int mx, int my, int dx, int dy, int filterType, int bitDepth)
    {
        int intermediateBits = IntermediateBits(bitDepth);
        int intermediateRound = (1 << intermediateBits) >> 1;
        int prepBias = bitDepth == 8 ? 0 : 8192;
        int maxValue = (1 << bitDepth) - 1;

        short[][] mid = new short[8][];
        for (int i = 0; i < 8; i++)
        {
            mid[i] = new short[w];
        }

        int inY = -8;
        int srcRow = srcOffset - (3 * srcStride);

        for (int y = 0; y < h; y++)
        {
            int srcY = my >> 10;
            int myFrac = (my & 0x3ff) >> 6;
            sbyte[]? fv = myFrac == 0 ? null : (h > 4 ? SubpelFilters[filterType >> 2][myFrac - 1] : SubpelFilters[3 + ((filterType >> 2) & 1)][myFrac - 1]);

            while (inY < srcY)
            {
                short[] rotated = mid[0];
                for (int i = 0; i < 7; i++)
                {
                    mid[i] = mid[i + 1];
                }

                mid[7] = rotated;

                int imx = mx;
                int ioff = 0;
                for (int x = 0; x < w; x++)
                {
                    int mxFrac = imx >> 6;
                    sbyte[]? fh = mxFrac == 0 ? null : (w > 4 ? SubpelFilters[filterType & 3][mxFrac - 1] : SubpelFilters[3 + (filterType & 1)][mxFrac - 1]);
                    rotated[x] = fh is not null
                        ? (short)((Filter(src, srcRow + ioff, fh, 1) + ((1 << (6 - intermediateBits)) >> 1)) >> (6 - intermediateBits))
                        : (short)(src[srcRow + ioff] << intermediateBits);
                    imx += dx;
                    ioff += imx >> 10;
                    imx &= 0x3ff;
                }

                srcRow += srcStride;
                inY++;
            }

            for (int x = 0; x < w; x++)
            {
                int v;
                if (fv is not null)
                {
                    int sum = 0;
                    for (int t = 0; t < 8; t++)
                    {
                        sum += fv[t] * mid[t][x];
                    }

                    if (dst is not null)
                    {
                        v = (sum + ((1 << (6 + intermediateBits)) >> 1)) >> (6 + intermediateBits);
                        dst[dstOffset + (y * dstStride) + x] = ClipPixel(v, maxValue);
                    }
                    else
                    {
                        tmp![(y * w) + x] = (short)(((sum + 32) >> 6) - prepBias);
                    }
                }
                else if (dst is not null)
                {
                    v = (mid[3][x] + intermediateRound) >> intermediateBits;
                    dst[dstOffset + (y * dstStride) + x] = ClipPixel(v, maxValue);
                }
                else
                {
                    tmp![(y * w) + x] = (short)(mid[3][x] - prepBias);
                }
            }

            my += dy;
        }
    }

    /// <summary>Gets dav1d's <c>get_intermediate_bits</c>: 4 for 8/10-bit, 2 for 12-bit.</summary>
    internal static int IntermediateBits(int bitDepth) => bitDepth == 8 ? 4 : 14 - bitDepth;

    private static ushort ClipPixel(int v, int maxValue) => (ushort)(v < 0 ? 0 : v > maxValue ? maxValue : v);
}
