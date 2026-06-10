// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1DecoderTests
{
    private static Configuration CreateConfiguration() => new(new Av1ConfigurationModule());

    [Fact]
    public void Detector_RecognizesAv1IvfHeader()
    {
        Av1ImageFormatDetector detector = new();
        byte[] data = Av1TestData.IvfFile();

        bool detected = detector.TryDetectFormat(data, out IImageFormat format);

        Assert.True(detected);
        Assert.Same(Av1Format.Instance, format);
    }

    [Fact]
    public void Detector_RejectsNonAv1Data()
    {
        Av1ImageFormatDetector detector = new();

        // RIFF/WEBP header should not be detected as AV1.
        byte[] data = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

        Assert.False(detector.TryDetectFormat(data, out IImageFormat format));
        Assert.Null(format);
    }

    [Fact]
    public void Identify_ReturnsSequenceHeaderDimensions()
    {
        using MemoryStream stream = new(Av1TestData.IvfFile());

        ImageInfo info = Av1Decoder.Instance.Identify(new DecoderOptions(), stream);

        Assert.Equal(Av1TestData.ExpectedWidth, info.Width);
        Assert.Equal(Av1TestData.ExpectedHeight, info.Height);
    }

    [Fact]
    public void Image_Identify_ResolvesAv1DecoderThroughConfiguration()
    {
        DecoderOptions options = new() { Configuration = CreateConfiguration() };
        using MemoryStream stream = new(Av1TestData.IvfFile());

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(Av1TestData.ExpectedWidth, info.Width);
        Assert.Equal(Av1TestData.ExpectedHeight, info.Height);
    }

    [Fact]
    public void Decode_NotYetImplemented_Throws()
    {
        using MemoryStream stream = new(Av1TestData.IvfFile());

        Assert.Throws<NotSupportedException>(
            () => Av1Decoder.Instance.Decode<Rgba32>(new DecoderOptions(), stream));
    }

    [Fact]
    public void Decode_InvalidContainer_ThrowsBeforeNotSupported()
    {
        // A DKIF container declaring a non-AV1 codec must fail as invalid data, not NotSupported.
        byte[] data = Av1TestData.IvfFile();

        // Overwrite the "AV01" FourCC (offset 8) with "VP80".
        data[8] = (byte)'V';
        data[9] = (byte)'P';
        data[10] = (byte)'8';
        data[11] = (byte)'0';

        using MemoryStream stream = new(data);

        Assert.Throws<InvalidDataException>(
            () => Av1Decoder.Instance.Identify(new DecoderOptions(), stream));
    }
}
