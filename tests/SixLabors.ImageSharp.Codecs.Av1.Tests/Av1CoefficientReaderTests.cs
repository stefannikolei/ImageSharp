// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1CoefficientReaderTests
{
    [Theory]
    [InlineData((int)Av1TransformSize.Size4x4, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size8x8, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size16x16, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size32x32, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size64x64, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size4x8, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size8x4, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size8x16, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size16x8, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size4x16, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size16x4, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size16x32, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size32x16, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size32x64, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size64x32, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size16x64, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size64x16, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size4x4, (int)Av1TransformType.HorizontalDct)]
    [InlineData((int)Av1TransformSize.Size8x8, (int)Av1TransformType.HorizontalDct)]
    [InlineData((int)Av1TransformSize.Size16x16, (int)Av1TransformType.HorizontalDct)]
    [InlineData((int)Av1TransformSize.Size4x4, (int)Av1TransformType.VerticalDct)]
    [InlineData((int)Av1TransformSize.Size8x8, (int)Av1TransformType.VerticalDct)]
    [InlineData((int)Av1TransformSize.Size16x16, (int)Av1TransformType.VerticalDct)]
    public void Coefficients_RoundTrip(int sizeValue, int typeValue)
    {
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        Av1TransformType type = (Av1TransformType)typeValue;

        // The number of coded coefficients: for the 2D class this is the scan length (which already
        // accounts for the 64-wide/high truncation to a 32x32 region); otherwise the full block.
        bool is2d = type.GetTransformClass() == Av1TransformClass.TwoDimensional;
        int count = is2d ? Av1ScanOrder.GetScan(size).Length : size.GetWidth() * size.GetHeight();
        Random random = new(HashCode.Combine(sizeValue, typeValue));

        for (int plane = 0; plane <= 1; plane++)
        {
            // Sweep a range of densities, including the all-zero and dc-only edge cases.
            foreach (double density in new[] { 0.0, 0.02, 0.15, 0.6, 1.0 })
            {
                int[] expected = GenerateBlock(random, count, density);
                int skipContext = random.Next(13);
                int dcSignContext = random.Next(3);

                Av1CoefficientCdfContext encoderCdf = Av1CoefficientCdfContext.CreateDefault(0);
                Av1CoefficientCdfContext decoderCdf = Av1CoefficientCdfContext.CreateDefault(0);

                Av1SymbolEncoder encoder = new();
                Av1CoefficientWriter.WriteCoefficients(encoder, encoderCdf, size, type, plane, skipContext, dcSignContext, expected);
                byte[] data = encoder.Finish();

                int[] actual = new int[count];
                Av1SymbolDecoder decoder = new(data);
                Av1CoefficientReader.ReadCoefficients(decoder, decoderCdf, size, type, plane, skipContext, dcSignContext, actual);

                Assert.True(expected.AsSpan().SequenceEqual(actual), $"Coefficient mismatch (size={size}, type={type}, plane={plane}, density={density}).");
                AssertCdfEqual(encoderCdf, decoderCdf);
            }
        }
    }

    private static int[] GenerateBlock(Random random, int count, double density)
    {
        int[] block = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (random.NextDouble() >= density)
            {
                continue;
            }

            // Bias towards small magnitudes (the common case) while still hitting the Golomb path.
            int magnitude = random.Next(10) switch
            {
                < 6 => random.Next(1, 4),
                < 9 => random.Next(4, 16),
                _ => random.Next(15, 3000),
            };

            block[i] = random.Next(2) == 0 ? magnitude : -magnitude;
        }

        return block;
    }

    private static void AssertCdfEqual(Av1CoefficientCdfContext a, Av1CoefficientCdfContext b)
    {
        AssertGroupEqual(a.Skip, b.Skip);
        AssertGroupEqual(a.DcSign, b.DcSign);
        AssertGroupEqual(a.EobHighBit, b.EobHighBit);
        AssertGroupEqual(a.BaseToken, b.BaseToken);
        AssertGroupEqual(a.BaseRange, b.BaseRange);
        AssertGroupEqual(a.EobBaseToken, b.EobBaseToken);
        for (int i = 0; i < a.EobBin.Length; i++)
        {
            AssertGroupEqual(a.EobBin[i], b.EobBin[i]);
        }
    }

    private static void AssertGroupEqual(ushort[][] a, ushort[][] b)
    {
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.True(a[i].AsSpan().SequenceEqual(b[i]), "Adaptive CDF drifted between encoder and decoder.");
        }
    }
}
