// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1InverseDct32Tests
{
    private const int Min = -(1 << 23);
    private const int Max = (1 << 23) - 1;

    private delegate void Transform(Span<int> c, int offset, int stride, int min, int max);

    private static Transform GetDct(int size) => size switch
    {
        32 => Av1InverseTransform1d.InverseDct32,
        64 => Av1InverseTransform1d.InverseDct64,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void InverseDct_ColumnsAreOrthogonal(int size)
    {
        Transform transform = GetDct(size);

        // The 64-point AV1 transform only reads coefficients 0..31 (indices 32..63 are always
        // zero by specification), i.e. it maps 32 coefficients onto 64 outputs. Only those basis
        // vectors are meaningful.
        int basisCount = size == 64 ? 32 : size;

        double[][] columns = new double[basisCount][];
        for (int k = 0; k < basisCount; k++)
        {
            int[] impulse = new int[size];
            impulse[k] = 1 << 12;
            transform(impulse, 0, 1, Min, Max);
            columns[k] = impulse.Select(v => (double)v).ToArray();
        }

        for (int i = 0; i < basisCount; i++)
        {
            double normI = Math.Sqrt(columns[i].Sum(v => v * v));
            for (int j = i + 1; j < basisCount; j++)
            {
                double normJ = Math.Sqrt(columns[j].Sum(v => v * v));
                double dot = 0;
                for (int n = 0; n < size; n++)
                {
                    dot += columns[i][n] * columns[j][n];
                }

                double cosine = dot / (normI * normJ);
                Assert.True(Math.Abs(cosine) < 0.02, $"size={size} columns {i},{j} cos={cosine:F4}");
            }
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void InverseDct_DcInput_ProducesConstantOutput(int size)
    {
        int[] data = new int[size];
        data[0] = 4096;
        GetDct(size)(data, 0, 1, Min, Max);

        int expected = (int)Math.Round(4096 / Math.Sqrt(2.0));
        for (int i = 0; i < size; i++)
        {
            Assert.Equal(data[0], data[i]);
            Assert.True(Math.Abs(data[i] - expected) <= 1);
        }
    }

    [Fact]
    public void InverseDct32_MatchesMathematicalReference()
    {
        Random random = new(321);
        for (int trial = 0; trial < 100; trial++)
        {
            int[] input = new int[32];
            for (int i = 0; i < 32; i++)
            {
                input[i] = random.Next(-300, 301);
            }

            // out[n] = in[0]/sqrt(2) + sum_{k>=1} in[k] * cos(PI*(2n+1)*k/(2N))
            double[] reference = new double[32];
            for (int n = 0; n < 32; n++)
            {
                double sum = input[0] / Math.Sqrt(2.0);
                for (int k = 1; k < 32; k++)
                {
                    sum += input[k] * Math.Cos(Math.PI * ((2 * n) + 1) * k / 64.0);
                }

                reference[n] = sum;
            }

            int[] actual = (int[])input.Clone();
            Av1InverseTransform1d.InverseDct32(actual, 0, 1, Min, Max);

            for (int i = 0; i < 32; i++)
            {
                Assert.True(Math.Abs(actual[i] - reference[i]) <= 6, $"index={i} actual={actual[i]} reference={reference[i]:F2}");
            }
        }
    }
}
