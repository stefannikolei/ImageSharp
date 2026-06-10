// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1QuantizationLookupTests
{
    [Theory]
    [InlineData(8, 0, 4, 4)]
    [InlineData(8, 255, 1336, 1828)]
    [InlineData(10, 0, 4, 4)]
    [InlineData(10, 255, 5347, 7312)]
    [InlineData(12, 0, 4, 4)]
    [InlineData(12, 255, 21387, 29247)]
    public void GetQuant_MatchesKnownSpecAnchors(int bitDepth, int qindex, int expectedDc, int expectedAc)
    {
        Assert.Equal(expectedDc, Av1QuantizationLookup.GetDcQuant(qindex, bitDepth));
        Assert.Equal(expectedAc, Av1QuantizationLookup.GetAcQuant(qindex, bitDepth));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void Quant_IsMonotonicNonDecreasing(int bitDepth)
    {
        int previousDc = 0;
        int previousAc = 0;
        for (int q = 0; q < 256; q++)
        {
            int dc = Av1QuantizationLookup.GetDcQuant(q, bitDepth);
            int ac = Av1QuantizationLookup.GetAcQuant(q, bitDepth);
            Assert.True(dc >= previousDc, $"DC not monotonic at q={q}");
            Assert.True(ac >= previousAc, $"AC not monotonic at q={q}");
            previousDc = dc;
            previousAc = ac;
        }
    }

    [Fact]
    public void GetQuant_ClampsQIndexToValidRange()
    {
        Assert.Equal(Av1QuantizationLookup.GetDcQuant(0, 8), Av1QuantizationLookup.GetDcQuant(-5, 8));
        Assert.Equal(Av1QuantizationLookup.GetAcQuant(255, 8), Av1QuantizationLookup.GetAcQuant(300, 8));
    }

    [Fact]
    public void GetQuant_HigherBitDepthYieldsLargerRange()
    {
        Assert.True(Av1QuantizationLookup.GetAcQuant(255, 12) > Av1QuantizationLookup.GetAcQuant(255, 10));
        Assert.True(Av1QuantizationLookup.GetAcQuant(255, 10) > Av1QuantizationLookup.GetAcQuant(255, 8));
    }

    [Fact]
    public void GetQuant_InvalidBitDepth_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Av1QuantizationLookup.GetDcQuant(0, 9));
}
