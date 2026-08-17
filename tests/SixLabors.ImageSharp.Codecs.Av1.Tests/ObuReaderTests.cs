// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class ObuReaderTests
{
    [Theory]
    [InlineData(new byte[] { 0x00 }, 0L)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128L)]
    [InlineData(new byte[] { 0xFF, 0x01 }, 255L)]
    [InlineData(new byte[] { 0xE5, 0x8E, 0x26 }, 624485L)]
    public void ReadLeb128_DecodesKnownVectors(byte[] data, long expected)
    {
        int offset = 0;
        long value = ObuReader.ReadLeb128(data, ref offset);

        Assert.Equal(expected, value);
        Assert.Equal(data.Length, offset);
    }

    [Fact]
    public void ReadLeb128_TooLong_Throws()
    {
        byte[] data = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80];
        int offset = 0;

        Assert.Throws<InvalidDataException>(() => ObuReader.ReadLeb128(data, ref offset));
    }

    [Fact]
    public void ReadHeader_ParsesSequenceHeaderType()
    {
        // 0x0A: type=1 (sequence header), extension=0, has_size=1.
        byte[] data = [0x0A];
        int offset = 0;

        ObuHeader header = ObuReader.ReadHeader(data, ref offset);

        Assert.Equal(ObuType.SequenceHeader, header.Type);
        Assert.False(header.HasExtension);
        Assert.True(header.HasSize);
        Assert.Equal(1, offset);
    }

    [Fact]
    public void ReadHeader_ParsesTemporalDelimiterType()
    {
        // 0x12: type=2 (temporal delimiter), has_size=1.
        byte[] data = [0x12];
        int offset = 0;

        ObuHeader header = ObuReader.ReadHeader(data, ref offset);

        Assert.Equal(ObuType.TemporalDelimiter, header.Type);
        Assert.True(header.HasSize);
    }

    [Fact]
    public void ReadHeader_ParsesExtensionHeader()
    {
        // 0x0E: type=1, extension=1, has_size=1. Extension byte 0x48 => temporal_id=2, spatial_id=1.
        byte[] data = [0x0E, 0x48];
        int offset = 0;

        ObuHeader header = ObuReader.ReadHeader(data, ref offset);

        Assert.True(header.HasExtension);
        Assert.Equal(2, header.TemporalId);
        Assert.Equal(1, header.SpatialId);
        Assert.Equal(2, offset);
    }

    [Fact]
    public void ReadHeader_ForbiddenBitSet_Throws()
    {
        byte[] data = [0x80];
        int offset = 0;

        Assert.Throws<InvalidDataException>(() => ObuReader.ReadHeader(data, ref offset));
    }

    [Fact]
    public void TryRead_ReadsSizedObuPayload()
    {
        byte[] data = Av1TestData.SequenceHeaderObu();
        int offset = 0;

        bool result = ObuReader.TryRead(data, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload);

        Assert.True(result);
        Assert.Equal(ObuType.SequenceHeader, header.Type);
        Assert.True(payload.SequenceEqual(Av1TestData.SequenceHeaderPayload));
        Assert.Equal(data.Length, offset);

        // No further OBUs.
        Assert.False(ObuReader.TryRead(data, ref offset, out _, out _));
    }

    [Fact]
    public void TryRead_IteratesMultipleObus()
    {
        byte[] data = Av1TestData.FrameData();
        int offset = 0;

        Assert.True(ObuReader.TryRead(data, ref offset, out ObuHeader first, out _));
        Assert.Equal(ObuType.TemporalDelimiter, first.Type);

        Assert.True(ObuReader.TryRead(data, ref offset, out ObuHeader second, out _));
        Assert.Equal(ObuType.SequenceHeader, second.Type);

        Assert.False(ObuReader.TryRead(data, ref offset, out _, out _));
    }
}
