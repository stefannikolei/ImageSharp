// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1BitStreamReaderTests
{
    [Fact]
    public void ReadLiteral_ReadsMostSignificantBitFirst()
    {
        // 0b1011_0100
        Av1BitStreamReader reader = new([0b1011_0100]);

        Assert.Equal(0b1011u, reader.ReadLiteral(4));
        Assert.Equal(0b0100u, reader.ReadLiteral(4));
        Assert.Equal(8, reader.BitPosition);
    }

    [Fact]
    public void ReadLiteral_SpansByteBoundaries()
    {
        // 0b1100_1010 0b1111_0000 => first 12 bits = 0b1100_1010_1111 = 0xCAF.
        Av1BitStreamReader reader = new([0b1100_1010, 0b1111_0000]);

        Assert.Equal(0xCAFu, reader.ReadLiteral(12));
    }

    [Fact]
    public void ReadLiteral_Zero_ConsumesNothing()
    {
        Av1BitStreamReader reader = new([0xFF]);

        Assert.Equal(0u, reader.ReadLiteral(0));
        Assert.Equal(0, reader.BitPosition);
    }

    [Fact]
    public void ReadBit_ReturnsIndividualBits()
    {
        Av1BitStreamReader reader = new([0b1010_0000]);

        Assert.Equal(1u, reader.ReadBit());
        Assert.Equal(0u, reader.ReadBit());
        Assert.Equal(1u, reader.ReadBit());
        Assert.Equal(0u, reader.ReadBit());
    }

    [Theory]
    [InlineData(0b1000_0000, 0u)] // "1" => 0
    [InlineData(0b0100_0000, 1u)] // "010" => 1
    [InlineData(0b0110_0000, 2u)] // "011" => 2
    [InlineData(0b0011_0000, 5u)] // "00110" => 5
    public void ReadUnsignedVariableLength_DecodesUvlc(int firstByte, uint expected)
    {
        Av1BitStreamReader reader = new([(byte)firstByte]);

        Assert.Equal(expected, reader.ReadUnsignedVariableLength());
    }

    [Fact]
    public void ReadBit_PastEnd_Throws()
    {
        Av1BitStreamReader reader = new([0x00]);
        reader.ReadLiteral(8);

        try
        {
            reader.ReadBit();
            Assert.Fail("Expected an InvalidDataException.");
        }
        catch (InvalidDataException)
        {
            // Expected.
        }
    }

    [Theory]
    [InlineData(0b1000_0000, -8)]
    [InlineData(0b0111_0000, 7)]
    [InlineData(0b1111_0000, -1)]
    [InlineData(0b0000_0000, 0)]
    public void ReadSignedLiteral_DecodesTwosComplement(int firstByte, int expected)
    {
        Av1BitStreamReader reader = new([(byte)firstByte]);
        Assert.Equal(expected, reader.ReadSignedLiteral(4));
    }

    [Theory]
    [InlineData(0b0000_0000, 0u)] // "00"  -> 0
    [InlineData(0b1000_0000, 2u)] // "10"  -> 2
    [InlineData(0b1100_0000, 3u)] // "110" -> 3
    [InlineData(0b1110_0000, 4u)] // "111" -> 4
    public void ReadNonSymmetric_DecodesRange(int firstByte, uint expected)
    {
        Av1BitStreamReader reader = new([(byte)firstByte]);
        Assert.Equal(expected, reader.ReadNonSymmetric(5));
    }

    [Fact]
    public void ReadNonSymmetric_One_AlwaysZero()
    {
        Av1BitStreamReader reader = new([0xFF]);
        Assert.Equal(0u, reader.ReadNonSymmetric(1));
        Assert.Equal(0, reader.BitPosition);
    }

    [Theory]
    [InlineData(new byte[] { 0xAB }, 1, 0xABu)]
    [InlineData(new byte[] { 0x34, 0x12 }, 2, 0x1234u)]
    [InlineData(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 4, 0x12345678u)]
    public void ReadLittleEndian_DecodesBytes(byte[] data, int n, uint expected)
    {
        Av1BitStreamReader reader = new(data);
        Assert.Equal(expected, reader.ReadLittleEndian(n));
    }
}
