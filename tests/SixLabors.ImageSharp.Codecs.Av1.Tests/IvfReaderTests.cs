// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class IvfReaderTests
{
    [Fact]
    public void ReadFileHeader_ParsesAv1Container()
    {
        using MemoryStream stream = new(Av1TestData.IvfFile());

        IvfFileHeader header = IvfReader.ReadFileHeader(stream);

        Assert.Equal("AV01", header.FourCc);
        Assert.True(header.IsAv1);
        Assert.Equal(Av1TestData.ExpectedWidth, header.Width);
        Assert.Equal(Av1TestData.ExpectedHeight, header.Height);
        Assert.Equal(30u, header.FrameRateNumerator);
        Assert.Equal(1u, header.FrameRateDenominator);
        Assert.Equal(1u, header.FrameCount);
    }

    [Fact]
    public void ReadFileHeader_InvalidSignature_Throws()
    {
        using MemoryStream stream = new(new byte[IvfFileHeader.Size]);

        Assert.Throws<InvalidDataException>(() => IvfReader.ReadFileHeader(stream));
    }

    [Fact]
    public void TryReadFrame_ReadsSingleFrameThenStops()
    {
        using MemoryStream stream = new(Av1TestData.IvfFile());
        _ = IvfReader.ReadFileHeader(stream);

        bool read = IvfReader.TryReadFrame(stream, out ulong timestamp, out byte[] frame);

        Assert.True(read);
        Assert.Equal(0ul, timestamp);
        Assert.True(frame.AsSpan().SequenceEqual(Av1TestData.FrameData()));

        Assert.False(IvfReader.TryReadFrame(stream, out _, out _));
    }
}
