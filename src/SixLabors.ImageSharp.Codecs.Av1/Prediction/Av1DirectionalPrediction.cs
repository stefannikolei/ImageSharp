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

        if (angle < 90)
        {
            PredictZone1(above, topLeft, size, angle, enableEdgeFilter, isSmooth, destination);
        }
        else if (angle < 180)
        {
            // The zone-2 (90 < angle < 180) predictor is not yet bit-exact; fail loudly.
            throw new NotSupportedException("Zone 2 directional prediction is not supported yet.");
        }
        else
        {
            // The zone-3 upsampling path (small blocks with shallow angles) is not yet bit-exact.
            if (enableEdgeFilter && GetUpsample(size + size, angle - 180, isSmooth) != 0)
            {
                throw new NotSupportedException("Zone 3 directional prediction with edge upsampling is not supported yet.");
            }

            PredictZone3(left, topLeft, size, angle, enableEdgeFilter, isSmooth, destination);
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

    private static void PredictZone1(ReadOnlySpan<byte> above, byte topLeft, int size, int angle, bool enableEdgeFilter, bool isSmooth, Span<byte> dst)
    {
        int dx = Derivative[angle >> 1];

        // Build the top edge (corner at index 0, then the above samples).
        int len = (2 * size) + 1;
        byte[] edge = new byte[len];
        edge[0] = topLeft;
        for (int i = 0; i < 2 * size; i++)
        {
            edge[i + 1] = above[i];
        }

        int maxBaseX;
        int baseInc = 1;
        int upsample = enableEdgeFilter ? GetUpsample(size + size, 90 - angle, isSmooth) : 0;
        if (upsample != 0)
        {
            byte[] up = new byte[(4 * size) + 4];
            UpsampleEdge(up, (2 * size) + 1, edge, 0, (2 * size) + 1);
            edge = up;

            // 'top' now starts at index 0 of the upsampled buffer (excluding corner).
            maxBaseX = (2 * (size + size)) - 2;
            dx <<= 1;
            baseInc = 2;
            Zone1Interpolate(edge, 0, maxBaseX, dx, baseInc, size, dst);
            return;
        }

        int strength = enableEdgeFilter ? GetFilterStrength(size + size, 90 - angle, isSmooth) : 0;
        if (strength != 0)
        {
            byte[] filtered = new byte[(2 * size) + 1];
            FilterEdge(filtered, (2 * size) + 1, 0, (2 * size) + 1, edge, 0, (2 * size) + 1, strength);

            // 'top' = filtered[1..]; offset by 1 to skip the corner.
            maxBaseX = (size + size) - 1;
            Zone1Interpolate(filtered, 1, maxBaseX, dx, baseInc, size, dst);
        }
        else
        {
            maxBaseX = (size + Math.Min(size, size)) - 1;
            Zone1Interpolate(edge, 1, maxBaseX, dx, baseInc, size, dst);
        }
    }

    private static void Zone1Interpolate(byte[] top, int topOffset, int maxBaseX, int dx, int baseInc, int size, Span<byte> dst)
    {
        int xpos = dx;
        for (int y = 0; y < size; y++, xpos += dx)
        {
            int frac = xpos & 0x3E;
            int basePos = xpos >> 6;
            for (int x = 0; x < size; x++, basePos += baseInc)
            {
                if (basePos < maxBaseX)
                {
                    int v = (top[topOffset + basePos] * (64 - frac)) + (top[topOffset + basePos + 1] * frac);
                    dst[(y * size) + x] = (byte)((v + 32) >> 6);
                }
                else
                {
                    byte fill = top[topOffset + maxBaseX];
                    for (; x < size; x++)
                    {
                        dst[(y * size) + x] = fill;
                    }

                    break;
                }
            }
        }
    }

    private static void PredictZone3(ReadOnlySpan<byte> left, byte topLeft, int size, int angle, bool enableEdgeFilter, bool isSmooth, Span<byte> dst)
    {
        int dy = Derivative[(270 - angle) >> 1];

        int len = (2 * size) + 1;
        byte[] edge = new byte[len];
        edge[0] = topLeft;
        for (int i = 0; i < 2 * size; i++)
        {
            edge[i + 1] = left[i];
        }

        int maxBaseY;
        int baseInc = 1;
        int upsample = enableEdgeFilter ? GetUpsample(size + size, angle - 180, isSmooth) : 0;
        if (upsample != 0)
        {
            byte[] up = new byte[(4 * size) + 4];
            UpsampleEdge(up, (2 * size) + 1, edge, 0, (2 * size) + 1);
            edge = up;
            maxBaseY = (2 * (size + size)) - 2;
            dy <<= 1;
            baseInc = 2;
            Zone3Interpolate(edge, 0, maxBaseY, dy, baseInc, size, dst);
            return;
        }

        int strength = enableEdgeFilter ? GetFilterStrength(size + size, angle - 180, isSmooth) : 0;
        if (strength != 0)
        {
            byte[] filtered = new byte[(2 * size) + 1];
            FilterEdge(filtered, (2 * size) + 1, 0, (2 * size) + 1, edge, 0, (2 * size) + 1, strength);
            maxBaseY = (size + size) - 1;
            Zone3Interpolate(filtered, 1, maxBaseY, dy, baseInc, size, dst);
        }
        else
        {
            maxBaseY = (size + Math.Min(size, size)) - 1;
            Zone3Interpolate(edge, 1, maxBaseY, dy, baseInc, size, dst);
        }
    }

    private static void Zone3Interpolate(byte[] left, int leftOffset, int maxBaseY, int dy, int baseInc, int size, Span<byte> dst)
    {
        int ypos = dy;
        for (int x = 0; x < size; x++, ypos += dy)
        {
            int frac = ypos & 0x3E;
            int basePos = ypos >> 6;
            for (int y = 0; y < size; y++, basePos += baseInc)
            {
                if (basePos < maxBaseY)
                {
                    int v = (left[leftOffset + basePos] * (64 - frac)) + (left[leftOffset + basePos + 1] * frac);
                    dst[(y * size) + x] = (byte)((v + 32) >> 6);
                }
                else
                {
                    byte fill = left[leftOffset + maxBaseY];
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

    private static void FilterEdge(byte[] outp, int sz, int limFrom, int limTo, byte[] inp, int from, int to, int strength)
    {
        byte[] kernel = FilterKernel[strength - 1];
        int i = 0;
        for (; i < Math.Min(sz, limFrom); i++)
        {
            outp[i] = inp[Clip(i, from, to - 1)];
        }

        for (; i < Math.Min(limTo, sz); i++)
        {
            int s = 0;
            for (int j = 0; j < 5; j++)
            {
                s += inp[Clip(i - 2 + j, from, to - 1)] * kernel[j];
            }

            outp[i] = (byte)((s + 8) >> 4);
        }

        for (; i < sz; i++)
        {
            outp[i] = inp[Clip(i, from, to - 1)];
        }
    }

    private static void UpsampleEdge(byte[] outp, int hsz, byte[] inp, int from, int to)
    {
        ReadOnlySpan<int> kernel = [-1, 9, 9, -1];
        int i = 0;
        for (; i < hsz - 1; i++)
        {
            outp[i * 2] = inp[Clip(i, from, to - 1)];
            int s = 0;
            for (int j = 0; j < 4; j++)
            {
                s += inp[Clip(i + j - 1, from, to - 1)] * kernel[j];
            }

            outp[(i * 2) + 1] = (byte)Math.Clamp((s + 8) >> 4, 0, 255);
        }

        outp[i * 2] = inp[Clip(i, from, to - 1)];
    }

    private static int Clip(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
