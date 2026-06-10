// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1InverseTransform2dTests
{
    // Intermediate (row) rounding shift per transform size, mirrored from the AV1 reference table.
    private static readonly int[] RowShift =
        [0, 1, 2, 2, 2, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2];

    // DC-only responses computed by hand from the AV1 DC fast-path formula using the known shift
    // for each size; these pin the row shift independently of the full transform path.
    [Theory]
    [InlineData((int)Av1TransformSize.Size4x4, 128)]
    [InlineData((int)Av1TransformSize.Size8x8, 64)]
    [InlineData((int)Av1TransformSize.Size16x16, 32)]
    [InlineData((int)Av1TransformSize.Size8x16, 45)]
    public void DctDct_DcOnly_ProducesExpectedFlatResidual(int sizeValue, int expected)
    {
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        int w = size.GetWidth();
        int h = size.GetHeight();

        int[] coeff = new int[w * h];
        coeff[0] = 4096;
        int[] residual = new int[w * h];

        Av1InverseTransform2d.Reconstruct(Av1TransformType.DctDct, size, coeff, residual, 8);

        foreach (int value in residual)
        {
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [InlineData((int)Av1TransformSize.Size4x4)]
    [InlineData((int)Av1TransformSize.Size8x8)]
    [InlineData((int)Av1TransformSize.Size16x16)]
    public void DctDct_MatchesSeparableMathReference(int sizeValue)
    {
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        int w = size.GetWidth();
        int h = size.GetHeight();
        int shift = RowShift[sizeValue];

        Random random = new(sizeValue + 7);
        for (int trial = 0; trial < 50; trial++)
        {
            int[] coeff = new int[w * h];
            for (int i = 0; i < coeff.Length; i++)
            {
                coeff[i] = random.Next(-200, 201);
            }

            int[] residual = new int[w * h];
            Av1InverseTransform2d.Reconstruct(Av1TransformType.DctDct, size, coeff, residual, 8);

            double[] reference = SeparableMathDct(coeff, w, h, shift);
            for (int i = 0; i < residual.Length; i++)
            {
                Assert.True(Math.Abs(residual[i] - reference[i]) <= 4, $"size={size} i={i} actual={residual[i]} ref={reference[i]:F2}");
            }
        }
    }

    [Theory]
    [InlineData((int)Av1TransformType.DctDct, (int)Av1TransformSize.Size8x8)]
    [InlineData((int)Av1TransformType.AdstAdst, (int)Av1TransformSize.Size8x8)]
    [InlineData((int)Av1TransformType.AdstDct, (int)Av1TransformSize.Size16x16)]
    [InlineData((int)Av1TransformType.DctAdst, (int)Av1TransformSize.Size4x4)]
    [InlineData((int)Av1TransformType.Identity, (int)Av1TransformSize.Size8x8)]
    [InlineData((int)Av1TransformType.FlipAdstDct, (int)Av1TransformSize.Size8x16)]
    [InlineData((int)Av1TransformType.DctDct, (int)Av1TransformSize.Size16x8)]
    public void Reconstruct_MatchesSeparableReference(int typeValue, int sizeValue)
    {
        Av1TransformType type = (Av1TransformType)typeValue;
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        int w = size.GetWidth();
        int h = size.GetHeight();

        Random random = new((typeValue * 31) + sizeValue);
        int[] coeff = new int[w * h];
        for (int i = 0; i < coeff.Length; i++)
        {
            coeff[i] = random.Next(-150, 151);
        }

        int[] residual = new int[w * h];
        Av1InverseTransform2d.Reconstruct(type, size, coeff, residual, 8);

        int[] reference = SeparableIntegerReference(type, size, coeff);
        Assert.Equal(reference, residual);
    }

    // Independent float reference: separable inverse DCT-III applied along rows then columns, with
    // the row shift and final /16, matching the AV1 net scaling but using exact arithmetic.
    private static double[] SeparableMathDct(int[] coeff, int w, int h, int shift)
    {
        double[,] m = new double[h, w];
        for (int y = 0; y < h; y++)
        {
            double[] row = new double[w];
            for (int x = 0; x < w; x++)
            {
                row[x] = coeff[(y * w) + x];
            }

            double[] r = MathIdct(row);
            for (int x = 0; x < w; x++)
            {
                m[y, x] = r[x] / (1 << shift);
            }
        }

        double[] output = new double[w * h];
        for (int x = 0; x < w; x++)
        {
            double[] col = new double[h];
            for (int y = 0; y < h; y++)
            {
                col[y] = m[y, x];
            }

            double[] c = MathIdct(col);
            for (int y = 0; y < h; y++)
            {
                output[(y * w) + x] = c[y] / 16.0;
            }
        }

        return output;
    }

    private static double[] MathIdct(double[] input)
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

    // A structurally independent integer reference following the section 7.13.3 description.
    private static int[] SeparableIntegerReference(Av1TransformType type, Av1TransformSize size, int[] coeff)
    {
        int w = size.GetWidth();
        int h = size.GetHeight();
        int shift = RowShift[(int)size];
        int round = (1 << shift) >> 1;
        bool isRect2 = (w * 2 == h) || (h * 2 == w);
        int min = short.MinValue;
        int max = short.MaxValue;

        Av1Transform1dType rowType = type.GetHorizontal();
        Av1Transform1dType columnType = type.GetVertical();

        int[] tmp = new int[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int value = coeff[(y * w) + x];
                tmp[(y * w) + x] = isRect2 ? (((value * 181) + 128) >> 8) : value;
            }

            Av1InverseTransform1d.Apply(rowType, w, tmp, y * w, 1, min, max);
        }

        for (int i = 0; i < tmp.Length; i++)
        {
            tmp[i] = Math.Clamp((tmp[i] + round) >> shift, min, max);
        }

        for (int x = 0; x < w; x++)
        {
            Av1InverseTransform1d.Apply(columnType, h, tmp, x, w, min, max);
        }

        int[] residual = new int[w * h];
        for (int i = 0; i < tmp.Length; i++)
        {
            residual[i] = (tmp[i] + 8) >> 4;
        }

        return residual;
    }
}
