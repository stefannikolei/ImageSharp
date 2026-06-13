// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// Directional intra prediction (specification section 7.11.2.4), a port of dav1d's <c>ipred_z1/z2/z3</c>
/// with the edge filtering and 2x upsampling preprocessing. Operates on square transform blocks.
/// </summary>
internal static class Av1DirectionalPrediction
{
    // dav1d_dr_intra_derivative (indexed by angle >> 1).
    private static readonly ushort[] Derivative =
    [
        0, 1023, 0, 547, 372, 0, 0, 273, 215, 0, 178, 151, 0, 132, 116, 0, 102, 0, 90, 80, 0, 71,
        64, 0, 57, 51, 0, 45, 0, 40, 35, 0, 31, 27, 0, 23, 19, 0, 15, 0, 11, 0, 7, 3,
    ];

    private static readonly byte[] ModeToAngle = [90, 180, 45, 135, 113, 157, 203, 67];

    private static readonly byte[][] FilterKernel =
    [
        [0, 4, 8, 4, 0],
        [0, 5, 6, 5, 0],
        [2, 4, 4, 4, 2],
    ];

    /// <summary>
    /// Predicts a square directional intra block. Reference samples are gathered from the plane with
    /// edge availability and the dav1d extension rules.
    /// </summary>
    /// <param name="above">The above reference row, index 0..2*size-1 (above then above-right).</param>
    /// <param name="left">The left reference column, index 0..2*size-1 (left then below-left).</param>
    /// <param name="topLeft">The top-left corner sample.</param>
    /// <param name="size">The transform size (width == height).</param>
    /// <param name="mode">The directional mode (1 = VERT .. 8 = VERT_LEFT).</param>
    /// <param name="angleDelta">The signed angle delta in [-3, 3].</param>
    /// <param name="enableEdgeFilter">Whether the sequence enables intra edge filtering.</param>
    /// <param name="isSmooth">Whether a neighbour uses a smooth mode (reduces filter strength).</param>
    /// <param name="haveAbove">Whether the above edge is available.</param>
    /// <param name="haveLeft">Whether the left edge is available.</param>
    /// <param name="maxWidth">The pixels remaining to the frame's right edge (zone-2 filter limit).</param>
    /// <param name="maxHeight">The pixels remaining to the frame's bottom edge (zone-2 filter limit).</param>
    /// <param name="destination">The prediction output buffer (size*size, row-major).</param>
    public static void Predict(
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        byte topLeft,
        int size,
        int mode,
        int angleDelta,
        bool enableEdgeFilter,
        bool isSmooth,
        bool haveAbove,
        bool haveLeft,
        int maxWidth,
        int maxHeight,
        Span<byte> destination)
    {
        int angle = ModeToAngle[mode - 1] + (3 * angleDelta);

        // Availability-based mode conversion (specification 7.11.2): a directional mode falls back to
        // VERT/HOR when the edge it would extrapolate from is unavailable.
        if (angle <= 90 && !(angle < 90 && haveAbove))
        {
            PredictVertical(above, size, destination);
            return;
        }

        if (angle >= 180 && !(angle > 180 && haveLeft))
        {
            PredictHorizontal(left, size, destination);
            return;
        }

        // Build a single dav1d-style reference buffer: the corner at index 'center', the above samples at
        // center+1.. and the left samples at center-1.. (mirroring dav1d's 'topleft_in' pointer).
        int center = 2 * size;
        byte[] tl = new byte[(4 * size) + 1];
        tl[center] = topLeft;
        for (int i = 0; i < 2 * size; i++)
        {
            tl[center + 1 + i] = above[i];
            tl[center - 1 - i] = left[i];
        }

        if (angle < 90)
        {
            PredictZone1(tl, center, size, angle, enableEdgeFilter, isSmooth, destination);
        }
        else if (angle < 180)
        {
            PredictZone2(tl, center, size, angle, enableEdgeFilter, isSmooth, maxWidth, maxHeight, destination);
        }
        else
        {
            PredictZone3(tl, center, size, angle, enableEdgeFilter, isSmooth, destination);
        }
    }

    private static void PredictVertical(ReadOnlySpan<byte> above, int size, Span<byte> dst)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                dst[(y * size) + x] = above[x];
            }
        }
    }

    private static void PredictHorizontal(ReadOnlySpan<byte> left, int size, Span<byte> dst)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                dst[(y * size) + x] = left[y];
            }
        }
    }

    // dav1d ipred_z1_c (square blocks: width == height == size). Extrapolates from the above edge.
    private static void PredictZone1(byte[] tl, int center, int size, int angle, bool enableEdgeFilter, bool isSmooth, Span<byte> dst)
    {
        int wh = size + size;
        int dx = Derivative[angle >> 1];
        byte[] topOut = new byte[(2 * wh) + 16];
        byte[] top;
        int topBase;
        int maxBaseX;
        int upsample = enableEdgeFilter ? GetUpsample(wh, 90 - angle, isSmooth) : 0;
        if (upsample != 0)
        {
            // upsample_edge(top_out, width+height, &topleft_in[1], -1, width+imin(w,h))
            UpsampleEdge(topOut, 0, wh, tl, center + 1, -1, size + size);
            top = topOut;
            topBase = 0;
            maxBaseX = (2 * wh) - 2;
            dx <<= 1;
        }
        else
        {
            int strength = enableEdgeFilter ? GetFilterStrength(wh, 90 - angle, isSmooth) : 0;
            if (strength != 0)
            {
                FilterEdge(topOut, 0, wh, 0, wh, tl, center + 1, -1, size + size, strength);
                top = topOut;
                topBase = 0;
                maxBaseX = wh - 1;
            }
            else
            {
                top = tl;
                topBase = center + 1;
                maxBaseX = (size + size) - 1;
            }
        }

        int baseInc = 1 + upsample;
        int xpos = dx;
        for (int y = 0; y < size; y++, xpos += dx)
        {
            int frac = xpos & 0x3E;
            int basePos = xpos >> 6;
            for (int x = 0; x < size; x++, basePos += baseInc)
            {
                if (basePos < maxBaseX)
                {
                    int v = (top[topBase + basePos] * (64 - frac)) + (top[topBase + basePos + 1] * frac);
                    dst[(y * size) + x] = (byte)((v + 32) >> 6);
                }
                else
                {
                    byte fill = top[topBase + maxBaseX];
                    for (; x < size; x++)
                    {
                        dst[(y * size) + x] = fill;
                    }

                    break;
                }
            }
        }
    }

    // dav1d ipred_z2_c (square blocks). Blends extrapolation from both the above and left edges.
    private static void PredictZone2(byte[] tl, int center, int size, int angle, bool enableEdgeFilter, bool isSmooth, int maxWidth, int maxHeight, Span<byte> dst)
    {
        int wh = size + size;
        int width = size;
        int height = size;
        int dy = Derivative[(angle - 90) >> 1];
        int dx = Derivative[(180 - angle) >> 1];
        int upsampleLeft = enableEdgeFilter ? GetUpsample(wh, 180 - angle, isSmooth) : 0;
        int upsampleAbove = enableEdgeFilter ? GetUpsample(wh, angle - 90, isSmooth) : 0;

        // edge[64+64+1] with the corner at 'ec'; left grows downward, above upward.
        int ec = 2 * size;
        byte[] edge = new byte[(4 * size) + 1];

        if (upsampleAbove != 0)
        {
            // upsample_edge(topleft, width+1, topleft_in, 0, width+1)
            UpsampleEdge(edge, ec, width + 1, tl, center, 0, width + 1);
            dx <<= 1;
        }
        else
        {
            int strength = enableEdgeFilter ? GetFilterStrength(wh, angle - 90, isSmooth) : 0;
            if (strength != 0)
            {
                FilterEdge(edge, ec + 1, width, 0, maxWidth, tl, center + 1, -1, width, strength);
            }
            else
            {
                for (int i = 0; i < width; i++)
                {
                    edge[ec + 1 + i] = tl[center + 1 + i];
                }
            }
        }

        if (upsampleLeft != 0)
        {
            // upsample_edge(&topleft[-height*2], height+1, &topleft_in[-height], 0, height+1)
            UpsampleEdge(edge, ec - (height * 2), height + 1, tl, center - height, 0, height + 1);
            dy <<= 1;
        }
        else
        {
            int strength = enableEdgeFilter ? GetFilterStrength(wh, 180 - angle, isSmooth) : 0;
            if (strength != 0)
            {
                FilterEdge(edge, ec - height, height, height - maxHeight, height, tl, center - height, 0, height + 1, strength);
            }
            else
            {
                for (int i = 0; i < height; i++)
                {
                    edge[ec - height + i] = tl[center - height + i];
                }
            }
        }

        edge[ec] = tl[center];

        int baseIncX = 1 + upsampleAbove;
        int leftBase = ec - (1 + upsampleLeft);
        int xposStart = ((1 + upsampleAbove) << 6) - dx;
        for (int y = 0; y < height; y++, xposStart -= dx)
        {
            int baseX = xposStart >> 6;
            int fracX = xposStart & 0x3E;
            int ypos = (y << (6 + upsampleLeft)) - dy;
            for (int x = 0; x < width; x++, baseX += baseIncX, ypos -= dy)
            {
                int v;
                if (baseX >= 0)
                {
                    v = (edge[ec + baseX] * (64 - fracX)) + (edge[ec + baseX + 1] * fracX);
                }
                else
                {
                    int baseY = ypos >> 6;
                    int fracY = ypos & 0x3E;
                    v = (edge[leftBase - baseY] * (64 - fracY)) + (edge[leftBase - (baseY + 1)] * fracY);
                }

                dst[(y * size) + x] = (byte)((v + 32) >> 6);
            }
        }
    }

    // dav1d ipred_z3_c (square blocks). Extrapolates from the left edge (stored in decreasing order).
    private static void PredictZone3(byte[] tl, int center, int size, int angle, bool enableEdgeFilter, bool isSmooth, Span<byte> dst)
    {
        int wh = size + size;
        int dy = Derivative[(270 - angle) >> 1];
        byte[] leftOut = new byte[(2 * wh) + 16];
        byte[] leftArr;
        int leftBase; // index of left[0]; subsequent samples are at decreasing indices.
        int maxBaseY;
        int upsample = enableEdgeFilter ? GetUpsample(wh, angle - 180, isSmooth) : 0;
        if (upsample != 0)
        {
            // upsample_edge(left_out, w+h, &topleft_in[-(w+h)], imax(w-h,0), w+h+1); left=&left_out[2*(w+h)-2]
            UpsampleEdge(leftOut, 0, wh, tl, center - wh, Math.Max(size - size, 0), wh + 1);
            leftArr = leftOut;
            leftBase = (2 * wh) - 2;
            maxBaseY = (2 * wh) - 2;
            dy <<= 1;
        }
        else
        {
            int strength = enableEdgeFilter ? GetFilterStrength(wh, angle - 180, isSmooth) : 0;
            if (strength != 0)
            {
                FilterEdge(leftOut, 0, wh, 0, wh, tl, center - wh, Math.Max(size - size, 0), wh + 1, strength);
                leftArr = leftOut;
                leftBase = wh - 1;
                maxBaseY = wh - 1;
            }
            else
            {
                leftArr = tl;
                leftBase = center - 1;
                maxBaseY = (size + size) - 1;
            }
        }

        int baseInc = 1 + upsample;
        int ypos = dy;
        for (int x = 0; x < size; x++, ypos += dy)
        {
            int frac = ypos & 0x3E;
            int basePos = ypos >> 6;
            for (int y = 0; y < size; y++, basePos += baseInc)
            {
                if (basePos < maxBaseY)
                {
                    int v = (leftArr[leftBase - basePos] * (64 - frac)) + (leftArr[leftBase - (basePos + 1)] * frac);
                    dst[(y * size) + x] = (byte)((v + 32) >> 6);
                }
                else
                {
                    byte fill = leftArr[leftBase - maxBaseY];
                    for (; y < size; y++)
                    {
                        dst[(y * size) + x] = fill;
                    }

                    break;
                }
            }
        }
    }

    private static int GetUpsample(int wh, int angle, bool isSmooth) => angle < 40 && wh <= (isSmooth ? 8 : 16) ? 1 : 0;

    private static int GetFilterStrength(int wh, int angle, bool isSmooth)
    {
        if (isSmooth)
        {
            if (wh <= 8)
            {
                if (angle >= 64)
                {
                    return 2;
                }

                if (angle >= 40)
                {
                    return 1;
                }
            }
            else if (wh <= 16)
            {
                if (angle >= 48)
                {
                    return 2;
                }

                if (angle >= 20)
                {
                    return 1;
                }
            }
            else if (wh <= 24)
            {
                if (angle >= 4)
                {
                    return 3;
                }
            }
            else
            {
                return 3;
            }
        }
        else
        {
            if (wh <= 8)
            {
                if (angle >= 56)
                {
                    return 1;
                }
            }
            else if (wh <= 16)
            {
                if (angle >= 40)
                {
                    return 1;
                }
            }
            else if (wh <= 24)
            {
                if (angle >= 32)
                {
                    return 3;
                }

                if (angle >= 16)
                {
                    return 2;
                }

                if (angle >= 8)
                {
                    return 1;
                }
            }
            else if (wh <= 32)
            {
                if (angle >= 32)
                {
                    return 3;
                }

                if (angle >= 4)
                {
                    return 2;
                }

                return 1;
            }
            else
            {
                return 3;
            }
        }

        return 0;
    }

    // dav1d filter_edge: out[outBase + i] over [0, sz); samples in [limFrom, limTo) are convolved, the rest copied.
    private static void FilterEdge(byte[] outp, int outBase, int sz, int limFrom, int limTo, byte[] inp, int inBase, int from, int to, int strength)
    {
        byte[] kernel = FilterKernel[strength - 1];
        int i = 0;
        for (; i < Math.Min(sz, limFrom); i++)
        {
            outp[outBase + i] = inp[inBase + Clip(i, from, to - 1)];
        }

        for (; i < Math.Min(limTo, sz); i++)
        {
            int s = 0;
            for (int j = 0; j < 5; j++)
            {
                s += inp[inBase + Clip(i - 2 + j, from, to - 1)] * kernel[j];
            }

            outp[outBase + i] = (byte)((s + 8) >> 4);
        }

        for (; i < sz; i++)
        {
            outp[outBase + i] = inp[inBase + Clip(i, from, to - 1)];
        }
    }

    // dav1d upsample_edge: writes 2*hsz-1 samples, doubling resolution with the [-1, 9, 9, -1] kernel.
    private static void UpsampleEdge(byte[] outp, int outBase, int hsz, byte[] inp, int inBase, int from, int to)
    {
        ReadOnlySpan<int> kernel = [-1, 9, 9, -1];
        int i = 0;
        for (; i < hsz - 1; i++)
        {
            outp[outBase + (i * 2)] = inp[inBase + Clip(i, from, to - 1)];
            int s = 0;
            for (int j = 0; j < 4; j++)
            {
                s += inp[inBase + Clip(i + j - 1, from, to - 1)] * kernel[j];
            }

            outp[outBase + (i * 2) + 1] = (byte)Math.Clamp((s + 8) >> 4, 0, 255);
        }

        outp[outBase + (i * 2)] = inp[inBase + Clip(i, from, to - 1)];
    }

    private static int Clip(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
