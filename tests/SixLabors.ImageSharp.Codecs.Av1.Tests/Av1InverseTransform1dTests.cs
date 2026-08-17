// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1InverseTransform1dTests
{
    private const int Min = -(1 << 23);
    private const int Max = (1 << 23) - 1;

    // The AV1 inverse DCT computes, in exact arithmetic:
    //   out[n] = in[0] / sqrt(2)  +  sum_{k=1}^{N-1} in[k] * cos(PI * (2n+1) * k / (2N))
    private static double[] DctReference(int[] input)
    {
        int n = input.Length;
        double[] output = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = input[0] / Math.Sqrt(2.0);
            for (int k = 1; k < n; k++)
            {
                sum += input[k] * Math.Cos(Math.PI * ((2 * i) + 1) * k / (2.0 * n));
            }

            output[i] = sum;
        }

        return output;
    }

    private static void RunDct(int[] data)
    {
        Span<int> span = data;
        switch (data.Length)
        {
            case 4:
                Av1InverseTransform1d.InverseDct4(span, 0, 1, Min, Max);
                break;
            case 8:
                Av1InverseTransform1d.InverseDct8(span, 0, 1, Min, Max);
                break;
            case 16:
                Av1InverseTransform1d.InverseDct16(span, 0, 1, Min, Max);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(data));
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void InverseDct_MatchesMathematicalReference(int size)
    {
        Random random = new(size * 17);
        for (int trial = 0; trial < 200; trial++)
        {
            int[] input = new int[size];
            for (int i = 0; i < size; i++)
            {
                input[i] = random.Next(-300, 301);
            }

            double[] reference = DctReference(input);
            int[] actual = (int[])input.Clone();
            RunDct(actual);

            // Fixed-point rounding accumulates roughly one unit per butterfly stage; a transcription
            // error in a constant would instead diverge by tens of units.
            double tolerance = size <= 8 ? 2.0 : 4.0;
            for (int i = 0; i < size; i++)
            {
                double diff = Math.Abs(actual[i] - reference[i]);
                Assert.True(diff <= tolerance, $"size={size} index={i} actual={actual[i]} reference={reference[i]:F3} diff={diff:F3}");
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void InverseDct_DcInput_ProducesConstantOutput(int size)
    {
        int[] data = new int[size];
        data[0] = 1000;
        RunDct(data);

        int expected = (int)Math.Round(1000 / Math.Sqrt(2.0));
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(data[0], data[i]);
            Assert.True(Math.Abs(data[i] - expected) <= 1);
        }
    }

    [Fact]
    public void InverseIdentity8_ScalesByTwo()
    {
        int[] data = [-5, 0, 3, 17, -128, 200, 64, -1];
        int[] expected = data.Select(v => v * 2).ToArray();

        Av1InverseTransform1d.InverseIdentity8(data, 0, 1);

        Assert.Equal(expected, data);
    }

    [Fact]
    public void InverseIdentity32_ScalesByFour()
    {
        int[] data = new int[32];
        Random random = new(5);
        for (int i = 0; i < 32; i++)
        {
            data[i] = random.Next(-500, 500);
        }

        int[] expected = data.Select(v => v * 4).ToArray();

        Av1InverseTransform1d.InverseIdentity32(data, 0, 1);

        Assert.Equal(expected, data);
    }

    [Fact]
    public void InverseIdentity4_ScalesBySqrt2()
    {
        int[] data = new int[4];
        Random random = new(11);
        for (int i = 0; i < 4; i++)
        {
            data[i] = random.Next(-1000, 1000);
        }

        int[] input = (int[])data.Clone();
        Av1InverseTransform1d.InverseIdentity4(data, 0, 1);

        for (int i = 0; i < 4; i++)
        {
            double expected = input[i] * Math.Sqrt(2.0);
            Assert.True(Math.Abs(data[i] - expected) <= 1, $"index={i} actual={data[i]} expected={expected:F3}");
        }
    }

    [Fact]
    public void InverseIdentity16_ScalesByTwoSqrt2()
    {
        int[] data = new int[16];
        Random random = new(13);
        for (int i = 0; i < 16; i++)
        {
            data[i] = random.Next(-1000, 1000);
        }

        int[] input = (int[])data.Clone();
        Av1InverseTransform1d.InverseIdentity16(data, 0, 1);

        for (int i = 0; i < 16; i++)
        {
            double expected = input[i] * 2 * Math.Sqrt(2.0);
            Assert.True(Math.Abs(data[i] - expected) <= 1, $"index={i} actual={data[i]} expected={expected:F3}");
        }
    }

    [Fact]
    public void InverseDct_RespectsStrideAndOffset()
    {
        // Place an 8-point sequence at offset 3 with stride 2 inside a larger buffer and confirm
        // only those elements are transformed.
        int[] sequence = [10, -20, 30, -40, 50, -60, 70, -80];
        int[] reference = (int[])sequence.Clone();
        Av1InverseTransform1d.InverseDct8(reference, 0, 1, Min, Max);

        int[] buffer = new int[3 + (2 * 8) + 1];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = 999;
        }

        for (int i = 0; i < 8; i++)
        {
            buffer[3 + (i * 2)] = sequence[i];
        }

        Av1InverseTransform1d.InverseDct8(buffer, 3, 2, Min, Max);

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(reference[i], buffer[3 + (i * 2)]);
        }

        // Untouched guard elements.
        Assert.Equal(999, buffer[0]);
        Assert.Equal(999, buffer[4]);
        Assert.Equal(999, buffer[^1]);
    }
}
