// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1TransformClassTests
{
    [Theory]
    [InlineData((int)Av1TransformType.DctDct, (int)Av1TransformClass.TwoDimensional)]
    [InlineData((int)Av1TransformType.AdstDct, (int)Av1TransformClass.TwoDimensional)]
    [InlineData((int)Av1TransformType.AdstAdst, (int)Av1TransformClass.TwoDimensional)]
    [InlineData((int)Av1TransformType.FlipAdstFlipAdst, (int)Av1TransformClass.TwoDimensional)]
    [InlineData((int)Av1TransformType.Identity, (int)Av1TransformClass.TwoDimensional)]
    [InlineData((int)Av1TransformType.VerticalDct, (int)Av1TransformClass.Vertical)]
    [InlineData((int)Av1TransformType.VerticalAdst, (int)Av1TransformClass.Vertical)]
    [InlineData((int)Av1TransformType.VerticalFlipAdst, (int)Av1TransformClass.Vertical)]
    [InlineData((int)Av1TransformType.HorizontalDct, (int)Av1TransformClass.Horizontal)]
    [InlineData((int)Av1TransformType.HorizontalAdst, (int)Av1TransformClass.Horizontal)]
    [InlineData((int)Av1TransformType.HorizontalFlipAdst, (int)Av1TransformClass.Horizontal)]
    public void GetTransformClass_MatchesReference(int typeValue, int expectedClass)
    {
        Av1TransformType type = (Av1TransformType)typeValue;
        Assert.Equal((Av1TransformClass)expectedClass, type.GetTransformClass());
    }

    [Fact]
    public void GetTransformClass_AllNonDirectionalTypesAreTwoDimensional()
    {
        // The ten "full" 2D combinations (indices 0-9) plus IDTX are all the 2D class.
        for (int t = 0; t <= (int)Av1TransformType.Identity; t++)
        {
            Assert.Equal(Av1TransformClass.TwoDimensional, ((Av1TransformType)t).GetTransformClass());
        }
    }
}
