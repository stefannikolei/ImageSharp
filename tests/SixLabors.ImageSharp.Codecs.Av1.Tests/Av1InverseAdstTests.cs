// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1InverseAdstTests
{
    private const int Min = -(1 << 23);
    private const int Max = (1 << 23) - 1;

    private delegate void Transform(Span<int> c, int offset, int stride, int min, int max);

    private static Transform GetAdst(int size) => size switch
    {
        4 => Av1InverseTransform1d.InverseAdst4,
        8 => Av1InverseTransform1d.InverseAdst8,
        16 => Av1InverseTransform1d.InverseAdst16,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    private static Transform GetFlipAdst(int size) => size switch
    {
        4 => Av1InverseTransform1d.InverseFlipAdst4,
        8 => Av1InverseTransform1d.InverseFlipAdst8,
        16 => Av1InverseTransform1d.InverseFlipAdst16,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    // Builds the transform matrix column by column via impulse responses and asserts the columns
    // are mutually orthogonal. ADST (like DCT) is an orthogonal transform, so any transcription
    // error in a butterfly constant breaks orthogonality far beyond fixed-point rounding noise.
    private static void AssertOrthogonal(Transform transform, int size)
    {
        double[][] columns = new double[size][];
        for (int k = 0; k < size; k++)
        {
            int[] impulse = new int[size];
            impulse[k] = 1 << 12;
            transform(impulse, 0, 1, Min, Max);
            columns[k] = impulse.Select(v => (double)v).ToArray();
        }

        for (int i = 0; i < size; i++)
        {
            double normI = Math.Sqrt(columns[i].Sum(v => v * v));
            for (int j = i + 1; j < size; j++)
            {
                double normJ = Math.Sqrt(columns[j].Sum(v => v * v));
                double dot = 0;
                for (int n = 0; n < size; n++)
                {
                    dot += columns[i][n] * columns[j][n];
                }

                double cosine = dot / (normI * normJ);
                Assert.True(Math.Abs(cosine) < 0.02, $"size={size} columns {i},{j} not orthogonal: cos={cosine:F4}");
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void InverseAdst_ColumnsAreOrthogonal(int size) => AssertOrthogonal(GetAdst(size), size);

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void InverseFlipAdst_ColumnsAreOrthogonal(int size) => AssertOrthogonal(GetFlipAdst(size), size);

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void FlipAdst_IsReversedAdst(int size)
    {
        Random random = new(size);
        int[] input = new int[size];
        for (int i = 0; i < size; i++)
        {
            input[i] = random.Next(-500, 500);
        }

        int[] adst = (int[])input.Clone();
        int[] flip = (int[])input.Clone();
        GetAdst(size)(adst, 0, 1, Min, Max);
        GetFlipAdst(size)(flip, 0, 1, Min, Max);

        for (int i = 0; i < size; i++)
        {
            Assert.Equal(adst[size - 1 - i], flip[i]);
        }
    }
}
