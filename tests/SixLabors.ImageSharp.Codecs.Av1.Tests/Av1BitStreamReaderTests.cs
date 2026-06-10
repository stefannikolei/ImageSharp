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
}
