// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <content>
/// One-dimensional inverse ADST and FLIPADST transforms (specification section 7.13.2.6). The
/// FLIPADST transform is the ADST transform with the output order reversed.
/// </content>
internal static partial class Av1InverseTransform1d
{
    /// <summary>Applies the 4-point inverse ADST.</summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseAdst4(Span<int> c, int offset, int stride, int min, int max)
        => Adst4(c, offset, stride, min, max, offset, stride);

    /// <summary>Applies the 4-point inverse FLIPADST.</summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseFlipAdst4(Span<int> c, int offset, int stride, int min, int max)
        => Adst4(c, offset, stride, min, max, offset + (3 * stride), -stride);

    /// <summary>Applies the 8-point inverse ADST.</summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseAdst8(Span<int> c, int offset, int stride, int min, int max)
        => Adst8(c, offset, stride, min, max, offset, stride);

    /// <summary>Applies the 8-point inverse FLIPADST.</summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseFlipAdst8(Span<int> c, int offset, int stride, int min, int max)
        => Adst8(c, offset, stride, min, max, offset + (7 * stride), -stride);

    /// <summary>Applies the 16-point inverse ADST.</summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseAdst16(Span<int> c, int offset, int stride, int min, int max)
        => Adst16(c, offset, stride, min, max, offset, stride);

    /// <summary>Applies the 16-point inverse FLIPADST.</summary>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    public static void InverseFlipAdst16(Span<int> c, int offset, int stride, int min, int max)
        => Adst16(c, offset, stride, min, max, offset + (15 * stride), -stride);

    private static void Adst4(Span<int> c, int inOffset, int inStride, int min, int max, int outOffset, int outStride)
    {
        int in0 = c[inOffset];
        int in1 = c[inOffset + inStride];
        int in2 = c[inOffset + (2 * inStride)];
        int in3 = c[inOffset + (3 * inStride)];

        int o0 = (((1321 * in0) + ((3803 - 4096) * in2) + ((2482 - 4096) * in3) + ((3344 - 4096) * in1) + 2048) >> 12) + in2 + in3 + in1;
        int o1 = ((((2482 - 4096) * in0) - (1321 * in2) - ((3803 - 4096) * in3) + ((3344 - 4096) * in1) + 2048) >> 12) + in0 - in3 + in1;
        int o2 = ((209 * (in0 - in2 + in3)) + 128) >> 8;
        int o3 = ((((3803 - 4096) * in0) + ((2482 - 4096) * in2) - (1321 * in3) - ((3344 - 4096) * in1) + 2048) >> 12) + in0 + in2 - in1;

        c[outOffset] = o0;
        c[outOffset + outStride] = o1;
        c[outOffset + (2 * outStride)] = o2;
        c[outOffset + (3 * outStride)] = o3;
    }

    private static void Adst8(Span<int> c, int inOffset, int inStride, int min, int max, int outOffset, int outStride)
    {
        int in0 = c[inOffset];
        int in1 = c[inOffset + inStride];
        int in2 = c[inOffset + (2 * inStride)];
        int in3 = c[inOffset + (3 * inStride)];
        int in4 = c[inOffset + (4 * inStride)];
        int in5 = c[inOffset + (5 * inStride)];
        int in6 = c[inOffset + (6 * inStride)];
        int in7 = c[inOffset + (7 * inStride)];

        int t0a = ((((4076 - 4096) * in7) + (401 * in0) + 2048) >> 12) + in7;
        int t1a = (((401 * in7) - ((4076 - 4096) * in0) + 2048) >> 12) - in0;
        int t2a = ((((3612 - 4096) * in5) + (1931 * in2) + 2048) >> 12) + in5;
        int t3a = (((1931 * in5) - ((3612 - 4096) * in2) + 2048) >> 12) - in2;
        int t4a = ((1299 * in3) + (1583 * in4) + 1024) >> 11;
        int t5a = ((1583 * in3) - (1299 * in4) + 1024) >> 11;
        int t6a = (((1189 * in1) + ((3920 - 4096) * in6) + 2048) >> 12) + in6;
        int t7a = ((((3920 - 4096) * in1) - (1189 * in6) + 2048) >> 12) + in1;

        int t0 = Clip(t0a + t4a, min, max);
        int t1 = Clip(t1a + t5a, min, max);
        int t2 = Clip(t2a + t6a, min, max);
        int t3 = Clip(t3a + t7a, min, max);
        int t4 = Clip(t0a - t4a, min, max);
        int t5 = Clip(t1a - t5a, min, max);
        int t6 = Clip(t2a - t6a, min, max);
        int t7 = Clip(t3a - t7a, min, max);

        t4a = ((((3784 - 4096) * t4) + (1567 * t5) + 2048) >> 12) + t4;
        t5a = (((1567 * t4) - ((3784 - 4096) * t5) + 2048) >> 12) - t5;
        t6a = ((((3784 - 4096) * t7) - (1567 * t6) + 2048) >> 12) + t7;
        t7a = (((1567 * t7) + ((3784 - 4096) * t6) + 2048) >> 12) + t6;

        c[outOffset] = Clip(t0 + t2, min, max);
        c[outOffset + (7 * outStride)] = -Clip(t1 + t3, min, max);
        t2 = Clip(t0 - t2, min, max);
        t3 = Clip(t1 - t3, min, max);
        c[outOffset + outStride] = -Clip(t4a + t6a, min, max);
        c[outOffset + (6 * outStride)] = Clip(t5a + t7a, min, max);
        t6 = Clip(t4a - t6a, min, max);
        t7 = Clip(t5a - t7a, min, max);

        c[outOffset + (3 * outStride)] = -((((t2 + t3) * 181) + 128) >> 8);
        c[outOffset + (4 * outStride)] = (((t2 - t3) * 181) + 128) >> 8;
        c[outOffset + (2 * outStride)] = (((t6 + t7) * 181) + 128) >> 8;
        c[outOffset + (5 * outStride)] = -((((t6 - t7) * 181) + 128) >> 8);
    }

    private static void Adst16(Span<int> c, int inOffset, int inStride, int min, int max, int outOffset, int outStride)
    {
        int in0 = c[inOffset];
        int in1 = c[inOffset + inStride];
        int in2 = c[inOffset + (2 * inStride)];
        int in3 = c[inOffset + (3 * inStride)];
        int in4 = c[inOffset + (4 * inStride)];
        int in5 = c[inOffset + (5 * inStride)];
        int in6 = c[inOffset + (6 * inStride)];
        int in7 = c[inOffset + (7 * inStride)];
        int in8 = c[inOffset + (8 * inStride)];
        int in9 = c[inOffset + (9 * inStride)];
        int in10 = c[inOffset + (10 * inStride)];
        int in11 = c[inOffset + (11 * inStride)];
        int in12 = c[inOffset + (12 * inStride)];
        int in13 = c[inOffset + (13 * inStride)];
        int in14 = c[inOffset + (14 * inStride)];
        int in15 = c[inOffset + (15 * inStride)];

        int t0 = (((in15 * (4091 - 4096)) + (in0 * 201) + 2048) >> 12) + in15;
        int t1 = (((in15 * 201) - (in0 * (4091 - 4096)) + 2048) >> 12) - in0;
        int t2 = (((in13 * (3973 - 4096)) + (in2 * 995) + 2048) >> 12) + in13;
        int t3 = (((in13 * 995) - (in2 * (3973 - 4096)) + 2048) >> 12) - in2;
        int t4 = (((in11 * (3703 - 4096)) + (in4 * 1751) + 2048) >> 12) + in11;
        int t5 = (((in11 * 1751) - (in4 * (3703 - 4096)) + 2048) >> 12) - in4;
        int t6 = ((in9 * 1645) + (in6 * 1220) + 1024) >> 11;
        int t7 = ((in9 * 1220) - (in6 * 1645) + 1024) >> 11;
        int t8 = (((in7 * 2751) + (in8 * (3035 - 4096)) + 2048) >> 12) + in8;
        int t9 = (((in7 * (3035 - 4096)) - (in8 * 2751) + 2048) >> 12) + in7;
        int t10 = (((in5 * 2106) + (in10 * (3513 - 4096)) + 2048) >> 12) + in10;
        int t11 = (((in5 * (3513 - 4096)) - (in10 * 2106) + 2048) >> 12) + in5;
        int t12 = (((in3 * 1380) + (in12 * (3857 - 4096)) + 2048) >> 12) + in12;
        int t13 = (((in3 * (3857 - 4096)) - (in12 * 1380) + 2048) >> 12) + in3;
        int t14 = (((in1 * 601) + (in14 * (4052 - 4096)) + 2048) >> 12) + in14;
        int t15 = (((in1 * (4052 - 4096)) - (in14 * 601) + 2048) >> 12) + in1;

        int t0a = Clip(t0 + t8, min, max);
        int t1a = Clip(t1 + t9, min, max);
        int t2a = Clip(t2 + t10, min, max);
        int t3a = Clip(t3 + t11, min, max);
        int t4a = Clip(t4 + t12, min, max);
        int t5a = Clip(t5 + t13, min, max);
        int t6a = Clip(t6 + t14, min, max);
        int t7a = Clip(t7 + t15, min, max);
        int t8a = Clip(t0 - t8, min, max);
        int t9a = Clip(t1 - t9, min, max);
        int t10a = Clip(t2 - t10, min, max);
        int t11a = Clip(t3 - t11, min, max);
        int t12a = Clip(t4 - t12, min, max);
        int t13a = Clip(t5 - t13, min, max);
        int t14a = Clip(t6 - t14, min, max);
        int t15a = Clip(t7 - t15, min, max);

        t8 = (((t8a * (4017 - 4096)) + (t9a * 799) + 2048) >> 12) + t8a;
        t9 = (((t8a * 799) - (t9a * (4017 - 4096)) + 2048) >> 12) - t9a;
        t10 = (((t10a * 2276) + (t11a * (3406 - 4096)) + 2048) >> 12) + t11a;
        t11 = (((t10a * (3406 - 4096)) - (t11a * 2276) + 2048) >> 12) + t10a;
        t12 = (((t13a * (4017 - 4096)) - (t12a * 799) + 2048) >> 12) + t13a;
        t13 = (((t13a * 799) + (t12a * (4017 - 4096)) + 2048) >> 12) + t12a;
        t14 = (((t15a * 2276) - (t14a * (3406 - 4096)) + 2048) >> 12) - t14a;
        t15 = (((t15a * (3406 - 4096)) + (t14a * 2276) + 2048) >> 12) + t15a;

        t0 = Clip(t0a + t4a, min, max);
        t1 = Clip(t1a + t5a, min, max);
        t2 = Clip(t2a + t6a, min, max);
        t3 = Clip(t3a + t7a, min, max);
        t4 = Clip(t0a - t4a, min, max);
        t5 = Clip(t1a - t5a, min, max);
        t6 = Clip(t2a - t6a, min, max);
        t7 = Clip(t3a - t7a, min, max);
        t8a = Clip(t8 + t12, min, max);
        t9a = Clip(t9 + t13, min, max);
        t10a = Clip(t10 + t14, min, max);
        t11a = Clip(t11 + t15, min, max);
        t12a = Clip(t8 - t12, min, max);
        t13a = Clip(t9 - t13, min, max);
        t14a = Clip(t10 - t14, min, max);
        t15a = Clip(t11 - t15, min, max);

        t4a = (((t4 * (3784 - 4096)) + (t5 * 1567) + 2048) >> 12) + t4;
        t5a = (((t4 * 1567) - (t5 * (3784 - 4096)) + 2048) >> 12) - t5;
        t6a = (((t7 * (3784 - 4096)) - (t6 * 1567) + 2048) >> 12) + t7;
        t7a = (((t7 * 1567) + (t6 * (3784 - 4096)) + 2048) >> 12) + t6;
        t12 = (((t12a * (3784 - 4096)) + (t13a * 1567) + 2048) >> 12) + t12a;
        t13 = (((t12a * 1567) - (t13a * (3784 - 4096)) + 2048) >> 12) - t13a;
        t14 = (((t15a * (3784 - 4096)) - (t14a * 1567) + 2048) >> 12) + t15a;
        t15 = (((t15a * 1567) + (t14a * (3784 - 4096)) + 2048) >> 12) + t14a;

        c[outOffset] = Clip(t0 + t2, min, max);
        c[outOffset + (15 * outStride)] = -Clip(t1 + t3, min, max);
        int t2b = Clip(t0 - t2, min, max);
        int t3b = Clip(t1 - t3, min, max);
        c[outOffset + (3 * outStride)] = -Clip(t4a + t6a, min, max);
        c[outOffset + (12 * outStride)] = Clip(t5a + t7a, min, max);
        t6 = Clip(t4a - t6a, min, max);
        t7 = Clip(t5a - t7a, min, max);
        c[outOffset + outStride] = -Clip(t8a + t10a, min, max);
        c[outOffset + (14 * outStride)] = Clip(t9a + t11a, min, max);
        t10 = Clip(t8a - t10a, min, max);
        t11 = Clip(t9a - t11a, min, max);
        c[outOffset + (2 * outStride)] = Clip(t12 + t14, min, max);
        c[outOffset + (13 * outStride)] = -Clip(t13 + t15, min, max);
        t14a = Clip(t12 - t14, min, max);
        t15a = Clip(t13 - t15, min, max);

        c[outOffset + (7 * outStride)] = -((((t2b + t3b) * 181) + 128) >> 8);
        c[outOffset + (8 * outStride)] = (((t2b - t3b) * 181) + 128) >> 8;
        c[outOffset + (4 * outStride)] = (((t6 + t7) * 181) + 128) >> 8;
        c[outOffset + (11 * outStride)] = -((((t6 - t7) * 181) + 128) >> 8);
        c[outOffset + (6 * outStride)] = (((t10 + t11) * 181) + 128) >> 8;
        c[outOffset + (9 * outStride)] = -((((t10 - t11) * 181) + 128) >> 8);
        c[outOffset + (5 * outStride)] = -((((t14a + t15a) * 181) + 128) >> 8);
        c[outOffset + (10 * outStride)] = (((t14a - t15a) * 181) + 128) >> 8;
    }
}
