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

    [Fact]
    public void Dequantize_Dc_NoShift_MultipliesByDcQuant()
    {
        // qindex 0 => dc_q = 4, 16x16 => no shift.
        Assert.Equal(4, Av1QuantizationLookup.Dequantize(1, true, 0, 8, Av1TransformSize.Size16x16));
        Assert.Equal(40, Av1QuantizationLookup.Dequantize(10, true, 0, 8, Av1TransformSize.Size16x16));
    }

    [Fact]
    public void Dequantize_Ac_PreservesSign()
    {
        // qindex 255 => ac_q = 1828, 16x16 => no shift.
        Assert.Equal(5484, Av1QuantizationLookup.Dequantize(3, false, 255, 8, Av1TransformSize.Size16x16));
        Assert.Equal(-5484, Av1QuantizationLookup.Dequantize(-3, false, 255, 8, Av1TransformSize.Size16x16));
    }

    [Theory]
    [InlineData((int)Av1TransformSize.Size16x16, 800)] // ctx 2 => shift 0
    [InlineData((int)Av1TransformSize.Size32x32, 400)] // ctx 3 => shift 1
    [InlineData((int)Av1TransformSize.Size64x64, 200)] // ctx 4 => shift 2
    [InlineData((int)Av1TransformSize.Size16x32, 400)] // ctx 3 => shift 1
    [InlineData((int)Av1TransformSize.Size16x64, 400)] // ctx (2+4+1)>>1 = 3 => shift 1
    [InlineData((int)Av1TransformSize.Size32x8, 800)]  // ctx (3+1+1)>>1 = 2 => shift 0
    [InlineData((int)Av1TransformSize.Size8x32, 800)]  // ctx (1+3+1)>>1 = 2 => shift 0
    [InlineData((int)Av1TransformSize.Size64x16, 400)] // ctx (4+2+1)>>1 = 3 => shift 1
    public void Dequantize_AppliesTransformSizeShift(int sizeValue, int expected)
    {
        // qindex 1 => dc_q = 8, level 100 => 800 before the size shift.
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        Assert.Equal(expected, Av1QuantizationLookup.Dequantize(100, true, 1, 8, size));
    }

    [Fact]
    public void Dequantize_ClampsToCoefficientRange()
    {
        // ac_q(255) * 1000 greatly exceeds the 8-bit coefficient range (+/- 32768).
        Assert.Equal(32767, Av1QuantizationLookup.Dequantize(1000, false, 255, 8, Av1TransformSize.Size16x16));
        Assert.Equal(-32768, Av1QuantizationLookup.Dequantize(-1000, false, 255, 8, Av1TransformSize.Size16x16));
    }

    [Fact]
    public void Dequantize_Zero_ReturnsZero()
        => Assert.Equal(0, Av1QuantizationLookup.Dequantize(0, false, 128, 8, Av1TransformSize.Size8x8));
}
