// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

// The transform enums are internal; theory parameters use their integer values to keep the public
// test methods from exposing internal types, then cast back inside.
public class Av1TransformTypeSizeTests
{
    private const int Min = -(1 << 23);
    private const int Max = (1 << 23) - 1;

    [Theory]
    [InlineData((int)Av1TransformSize.Size4x4, 4, 4)]
    [InlineData((int)Av1TransformSize.Size8x8, 8, 8)]
    [InlineData((int)Av1TransformSize.Size16x16, 16, 16)]
    [InlineData((int)Av1TransformSize.Size32x32, 32, 32)]
    [InlineData((int)Av1TransformSize.Size64x64, 64, 64)]
    [InlineData((int)Av1TransformSize.Size4x8, 4, 8)]
    [InlineData((int)Av1TransformSize.Size8x4, 8, 4)]
    [InlineData((int)Av1TransformSize.Size16x32, 16, 32)]
    [InlineData((int)Av1TransformSize.Size64x32, 64, 32)]
    [InlineData((int)Av1TransformSize.Size4x16, 4, 16)]
    [InlineData((int)Av1TransformSize.Size32x8, 32, 8)]
    [InlineData((int)Av1TransformSize.Size64x16, 64, 16)]
    public void TransformSize_HasExpectedDimensions(int sizeValue, int width, int height)
    {
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        Assert.Equal(width, size.GetWidth());
        Assert.Equal(height, size.GetHeight());
        Assert.Equal(width, 1 << size.GetWidthLog2());
        Assert.Equal(height, 1 << size.GetHeightLog2());
    }

    [Theory]
    [InlineData((int)Av1TransformType.DctDct, (int)Av1Transform1dType.Dct, (int)Av1Transform1dType.Dct)]
    [InlineData((int)Av1TransformType.AdstDct, (int)Av1Transform1dType.Adst, (int)Av1Transform1dType.Dct)]
    [InlineData((int)Av1TransformType.DctAdst, (int)Av1Transform1dType.Dct, (int)Av1Transform1dType.Adst)]
    [InlineData((int)Av1TransformType.AdstAdst, (int)Av1Transform1dType.Adst, (int)Av1Transform1dType.Adst)]
    [InlineData((int)Av1TransformType.FlipAdstDct, (int)Av1Transform1dType.FlipAdst, (int)Av1Transform1dType.Dct)]
    [InlineData((int)Av1TransformType.DctFlipAdst, (int)Av1Transform1dType.Dct, (int)Av1Transform1dType.FlipAdst)]
    [InlineData((int)Av1TransformType.Identity, (int)Av1Transform1dType.Identity, (int)Av1Transform1dType.Identity)]
    [InlineData((int)Av1TransformType.VerticalDct, (int)Av1Transform1dType.Dct, (int)Av1Transform1dType.Identity)]
    [InlineData((int)Av1TransformType.HorizontalDct, (int)Av1Transform1dType.Identity, (int)Av1Transform1dType.Dct)]
    [InlineData((int)Av1TransformType.VerticalAdst, (int)Av1Transform1dType.Adst, (int)Av1Transform1dType.Identity)]
    [InlineData((int)Av1TransformType.HorizontalFlipAdst, (int)Av1Transform1dType.Identity, (int)Av1Transform1dType.FlipAdst)]
    public void TransformType_MapsToExpected1dTransforms(int typeValue, int verticalValue, int horizontalValue)
    {
        Av1TransformType type = (Av1TransformType)typeValue;
        Assert.Equal((Av1Transform1dType)verticalValue, type.GetVertical());
        Assert.Equal((Av1Transform1dType)horizontalValue, type.GetHorizontal());
    }

    [Theory]
    [InlineData((int)Av1Transform1dType.Dct, 4)]
    [InlineData((int)Av1Transform1dType.Dct, 8)]
    [InlineData((int)Av1Transform1dType.Dct, 16)]
    [InlineData((int)Av1Transform1dType.Dct, 32)]
    [InlineData((int)Av1Transform1dType.Dct, 64)]
    [InlineData((int)Av1Transform1dType.Adst, 4)]
    [InlineData((int)Av1Transform1dType.Adst, 8)]
    [InlineData((int)Av1Transform1dType.Adst, 16)]
    [InlineData((int)Av1Transform1dType.FlipAdst, 4)]
    [InlineData((int)Av1Transform1dType.FlipAdst, 8)]
    [InlineData((int)Av1Transform1dType.FlipAdst, 16)]
    [InlineData((int)Av1Transform1dType.Identity, 4)]
    [InlineData((int)Av1Transform1dType.Identity, 8)]
    [InlineData((int)Av1Transform1dType.Identity, 16)]
    [InlineData((int)Av1Transform1dType.Identity, 32)]
    public void Apply_MatchesDirectCall(int typeValue, int length)
    {
        Av1Transform1dType type = (Av1Transform1dType)typeValue;
        Random random = new(((int)type * 100) + length);
        int[] input = new int[length];
        for (int i = 0; i < length; i++)
        {
            input[i] = random.Next(-400, 400);
        }

        int[] viaDispatch = (int[])input.Clone();
        Av1InverseTransform1d.Apply(type, length, viaDispatch, 0, 1, Min, Max);

        int[] direct = (int[])input.Clone();
        ApplyDirect(type, length, direct);

        Assert.Equal(direct, viaDispatch);
    }

    [Fact]
    public void Apply_UnsupportedAdstLength_Throws()
        => Assert.Throws<NotSupportedException>(() =>
        {
            int[] data = new int[32];
            Av1InverseTransform1d.Apply(Av1Transform1dType.Adst, 32, data, 0, 1, Min, Max);
        });

    private static void ApplyDirect(Av1Transform1dType type, int length, int[] data)
    {
        Span<int> c = data;
        switch (type)
        {
            case Av1Transform1dType.Dct when length == 4:
                Av1InverseTransform1d.InverseDct4(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Dct when length == 8:
                Av1InverseTransform1d.InverseDct8(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Dct when length == 16:
                Av1InverseTransform1d.InverseDct16(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Dct when length == 32:
                Av1InverseTransform1d.InverseDct32(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Dct when length == 64:
                Av1InverseTransform1d.InverseDct64(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Adst when length == 4:
                Av1InverseTransform1d.InverseAdst4(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Adst when length == 8:
                Av1InverseTransform1d.InverseAdst8(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Adst when length == 16:
                Av1InverseTransform1d.InverseAdst16(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.FlipAdst when length == 4:
                Av1InverseTransform1d.InverseFlipAdst4(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.FlipAdst when length == 8:
                Av1InverseTransform1d.InverseFlipAdst8(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.FlipAdst when length == 16:
                Av1InverseTransform1d.InverseFlipAdst16(c, 0, 1, Min, Max);
                break;
            case Av1Transform1dType.Identity when length == 4:
                Av1InverseTransform1d.InverseIdentity4(c, 0, 1);
                break;
            case Av1Transform1dType.Identity when length == 8:
                Av1InverseTransform1d.InverseIdentity8(c, 0, 1);
                break;
            case Av1Transform1dType.Identity when length == 16:
                Av1InverseTransform1d.InverseIdentity16(c, 0, 1);
                break;
            case Av1Transform1dType.Identity when length == 32:
                Av1InverseTransform1d.InverseIdentity32(c, 0, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
