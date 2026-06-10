// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class ObuSequenceHeaderTests
{
    [Fact]
    public void Parse_ReducedStillPictureHeader_ExtractsDimensions()
    {
        ObuSequenceHeader header = ObuSequenceHeader.Parse(Av1TestData.SequenceHeaderPayload);

        Assert.Equal(0, header.SeqProfile);
        Assert.True(header.StillPicture);
        Assert.True(header.ReducedStillPictureHeader);
        Assert.Equal(Av1TestData.ExpectedWidth, header.MaxFrameWidth);
        Assert.Equal(Av1TestData.ExpectedHeight, header.MaxFrameHeight);
    }
}
