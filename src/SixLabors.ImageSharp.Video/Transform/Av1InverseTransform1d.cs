// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// One-dimensional inverse transforms used by the AV1 reconstruction process (specification
/// section 7.13.2). The fixed-point butterfly networks and constants follow the AV1 specification.
/// </summary>
/// <remarks>
/// Each transform operates in place on a strided sequence of <see cref="int"/> coefficients,
/// clamping intermediate results to the inclusive range <c>[min, max]</c> exactly as the
/// specification requires. The constants are the AV1 <c>cos128</c>/<c>sin128</c> values scaled by
/// 4096 (with the common <c>(x - 4096)</c> rewrite used to keep intermediate products within the
/// 31-bit + sign range mandated by the specification).
/// </remarks>
internal static partial class Av1InverseTransform1d
{
    private static int Clip(int value, int min, int max) => Math.Clamp(value, min, max);

    /// <summary>
    /// Applies the 4-point inverse DCT.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseDct4(Span<int> c, int offset, int stride, int min, int max)
        => InverseDct4(c, offset, stride, min, max, false);

    /// <summary>
    /// Applies the 8-point inverse DCT.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseDct8(Span<int> c, int offset, int stride, int min, int max)
        => InverseDct8(c, offset, stride, min, max, false);

    /// <summary>
    /// Applies the 16-point inverse DCT.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseDct16(Span<int> c, int offset, int stride, int min, int max)
        => InverseDct16(c, offset, stride, min, max, false);

    /// <summary>
    /// Applies the 4-point inverse identity transform.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    public static void InverseIdentity4(Span<int> c, int offset, int stride)
    {
        for (int i = 0; i < 4; i++)
        {
            int input = c[offset + (stride * i)];
            c[offset + (stride * i)] = input + (((input * 1697) + 2048) >> 12);
        }
    }

    /// <summary>
    /// Applies the 8-point inverse identity transform.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    public static void InverseIdentity8(Span<int> c, int offset, int stride)
    {
        for (int i = 0; i < 8; i++)
        {
            c[offset + (stride * i)] *= 2;
        }
    }

    /// <summary>
    /// Applies the 16-point inverse identity transform.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    public static void InverseIdentity16(Span<int> c, int offset, int stride)
    {
        for (int i = 0; i < 16; i++)
        {
            int input = c[offset + (stride * i)];
            c[offset + (stride * i)] = (2 * input) + (((input * 1697) + 1024) >> 11);
        }
    }

    /// <summary>
    /// Applies the 32-point inverse identity transform.
    /// </summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    public static void InverseIdentity32(Span<int> c, int offset, int stride)
    {
        for (int i = 0; i < 32; i++)
        {
            c[offset + (stride * i)] *= 4;
        }
    }

    private static void InverseDct4(Span<int> c, int offset, int stride, int min, int max, bool tx64)
    {
        int in0 = c[offset];
        int in1 = c[offset + stride];

        int t0, t1, t2, t3;
        if (tx64)
        {
            t0 = t1 = ((in0 * 181) + 128) >> 8;
            t2 = ((in1 * 1567) + 2048) >> 12;
            t3 = ((in1 * 3784) + 2048) >> 12;
        }
        else
        {
            int in2 = c[offset + (2 * stride)];
            int in3 = c[offset + (3 * stride)];

            t0 = (((in0 + in2) * 181) + 128) >> 8;
            t1 = (((in0 - in2) * 181) + 128) >> 8;
            t2 = (((in1 * 1567) - (in3 * (3784 - 4096)) + 2048) >> 12) - in3;
            t3 = (((in1 * (3784 - 4096)) + (in3 * 1567) + 2048) >> 12) + in1;
        }

        c[offset] = Clip(t0 + t3, min, max);
        c[offset + stride] = Clip(t1 + t2, min, max);
        c[offset + (2 * stride)] = Clip(t1 - t2, min, max);
        c[offset + (3 * stride)] = Clip(t0 - t3, min, max);
    }

    private static void InverseDct8(Span<int> c, int offset, int stride, int min, int max, bool tx64)
    {
        InverseDct4(c, offset, stride << 1, min, max, tx64);

        int in1 = c[offset + stride];
        int in3 = c[offset + (3 * stride)];

        int t4a, t5a, t6a, t7a;
        if (tx64)
        {
            t4a = ((in1 * 799) + 2048) >> 12;
            t5a = ((in3 * -2276) + 2048) >> 12;
            t6a = ((in3 * 3406) + 2048) >> 12;
            t7a = ((in1 * 4017) + 2048) >> 12;
        }
        else
        {
            int in5 = c[offset + (5 * stride)];
            int in7 = c[offset + (7 * stride)];

            t4a = (((in1 * 799) - (in7 * (4017 - 4096)) + 2048) >> 12) - in7;
            t5a = ((in5 * 1703) - (in3 * 1138) + 1024) >> 11;
            t6a = ((in5 * 1138) + (in3 * 1703) + 1024) >> 11;
            t7a = (((in1 * (4017 - 4096)) + (in7 * 799) + 2048) >> 12) + in1;
        }

        int t4 = Clip(t4a + t5a, min, max);
        t5a = Clip(t4a - t5a, min, max);
        int t7 = Clip(t7a + t6a, min, max);
        t6a = Clip(t7a - t6a, min, max);

        int t5 = (((t6a - t5a) * 181) + 128) >> 8;
        int t6 = (((t6a + t5a) * 181) + 128) >> 8;

        int t0 = c[offset];
        int t1 = c[offset + (2 * stride)];
        int t2 = c[offset + (4 * stride)];
        int t3 = c[offset + (6 * stride)];

        c[offset] = Clip(t0 + t7, min, max);
        c[offset + stride] = Clip(t1 + t6, min, max);
        c[offset + (2 * stride)] = Clip(t2 + t5, min, max);
        c[offset + (3 * stride)] = Clip(t3 + t4, min, max);
        c[offset + (4 * stride)] = Clip(t3 - t4, min, max);
        c[offset + (5 * stride)] = Clip(t2 - t5, min, max);
        c[offset + (6 * stride)] = Clip(t1 - t6, min, max);
        c[offset + (7 * stride)] = Clip(t0 - t7, min, max);
    }

    private static void InverseDct16(Span<int> c, int offset, int stride, int min, int max, bool tx64)
    {
        InverseDct8(c, offset, stride << 1, min, max, tx64);

        int in1 = c[offset + stride];
        int in3 = c[offset + (3 * stride)];
        int in5 = c[offset + (5 * stride)];
        int in7 = c[offset + (7 * stride)];

        int t8a, t9a, t10a, t11a, t12a, t13a, t14a, t15a;
        if (tx64)
        {
            t8a = ((in1 * 401) + 2048) >> 12;
            t9a = ((in7 * -2598) + 2048) >> 12;
            t10a = ((in5 * 1931) + 2048) >> 12;
            t11a = ((in3 * -1189) + 2048) >> 12;
            t12a = ((in3 * 3920) + 2048) >> 12;
            t13a = ((in5 * 3612) + 2048) >> 12;
            t14a = ((in7 * 3166) + 2048) >> 12;
            t15a = ((in1 * 4076) + 2048) >> 12;
        }
        else
        {
            int in9 = c[offset + (9 * stride)];
            int in11 = c[offset + (11 * stride)];
            int in13 = c[offset + (13 * stride)];
            int in15 = c[offset + (15 * stride)];

            t8a = (((in1 * 401) - (in15 * (4076 - 4096)) + 2048) >> 12) - in15;
            t9a = ((in9 * 1583) - (in7 * 1299) + 1024) >> 11;
            t10a = (((in5 * 1931) - (in11 * (3612 - 4096)) + 2048) >> 12) - in11;
            t11a = (((in13 * (3920 - 4096)) - (in3 * 1189) + 2048) >> 12) + in13;
            t12a = (((in13 * 1189) + (in3 * (3920 - 4096)) + 2048) >> 12) + in3;
            t13a = (((in5 * (3612 - 4096)) + (in11 * 1931) + 2048) >> 12) + in5;
            t14a = ((in9 * 1299) + (in7 * 1583) + 1024) >> 11;
            t15a = (((in1 * (4076 - 4096)) + (in15 * 401) + 2048) >> 12) + in1;
        }

        int t8 = Clip(t8a + t9a, min, max);
        int t9 = Clip(t8a - t9a, min, max);
        int t10 = Clip(t11a - t10a, min, max);
        int t11 = Clip(t11a + t10a, min, max);
        int t12 = Clip(t12a + t13a, min, max);
        int t13 = Clip(t12a - t13a, min, max);
        int t14 = Clip(t15a - t14a, min, max);
        int t15 = Clip(t15a + t14a, min, max);

        t9a = (((t14 * 1567) - (t9 * (3784 - 4096)) + 2048) >> 12) - t9;
        t14a = (((t14 * (3784 - 4096)) + (t9 * 1567) + 2048) >> 12) + t14;
        t10a = ((-((t13 * (3784 - 4096)) + (t10 * 1567)) + 2048) >> 12) - t13;
        t13a = (((t13 * 1567) - (t10 * (3784 - 4096)) + 2048) >> 12) - t10;

        t8a = Clip(t8 + t11, min, max);
        t9 = Clip(t9a + t10a, min, max);
        t10 = Clip(t9a - t10a, min, max);
        t11a = Clip(t8 - t11, min, max);
        t12a = Clip(t15 - t12, min, max);
        t13 = Clip(t14a - t13a, min, max);
        t14 = Clip(t14a + t13a, min, max);
        t15a = Clip(t15 + t12, min, max);

        t10a = (((t13 - t10) * 181) + 128) >> 8;
        t13a = (((t13 + t10) * 181) + 128) >> 8;
        t11 = (((t12a - t11a) * 181) + 128) >> 8;
        t12 = (((t12a + t11a) * 181) + 128) >> 8;

        int u0 = c[offset];
        int u1 = c[offset + (2 * stride)];
        int u2 = c[offset + (4 * stride)];
        int u3 = c[offset + (6 * stride)];
        int u4 = c[offset + (8 * stride)];
        int u5 = c[offset + (10 * stride)];
        int u6 = c[offset + (12 * stride)];
        int u7 = c[offset + (14 * stride)];

        c[offset] = Clip(u0 + t15a, min, max);
        c[offset + stride] = Clip(u1 + t14, min, max);
        c[offset + (2 * stride)] = Clip(u2 + t13a, min, max);
        c[offset + (3 * stride)] = Clip(u3 + t12, min, max);
        c[offset + (4 * stride)] = Clip(u4 + t11, min, max);
        c[offset + (5 * stride)] = Clip(u5 + t10a, min, max);
        c[offset + (6 * stride)] = Clip(u6 + t9, min, max);
        c[offset + (7 * stride)] = Clip(u7 + t8a, min, max);
        c[offset + (8 * stride)] = Clip(u7 - t8a, min, max);
        c[offset + (9 * stride)] = Clip(u6 - t9, min, max);
        c[offset + (10 * stride)] = Clip(u5 - t10a, min, max);
        c[offset + (11 * stride)] = Clip(u4 - t11, min, max);
        c[offset + (12 * stride)] = Clip(u3 - t12, min, max);
        c[offset + (13 * stride)] = Clip(u2 - t13a, min, max);
        c[offset + (14 * stride)] = Clip(u1 - t14, min, max);
        c[offset + (15 * stride)] = Clip(u0 - t15a, min, max);
    }
}
