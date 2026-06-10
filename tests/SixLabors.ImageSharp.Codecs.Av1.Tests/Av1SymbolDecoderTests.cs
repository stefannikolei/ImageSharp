// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1SymbolDecoderTests
{
    [Fact]
    public void Bools_RoundTrip()
    {
        Random random = new(1234);
        int[] bits = new int[512];
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < bits.Length; i++)
        {
            bits[i] = random.Next(2);
            encoder.WriteBool(bits[i]);
        }

        byte[] data = encoder.Finish();

        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < bits.Length; i++)
        {
            Assert.Equal(bits[i], decoder.ReadBool());
        }
    }

    [Fact]
    public void Literals_RoundTrip()
    {
        Random random = new(99);
        (uint Value, int Bits)[] literals = new (uint, int)[256];
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < literals.Length; i++)
        {
            int width = random.Next(1, 17);
            uint value = (uint)random.NextInt64(0, 1L << width);
            literals[i] = (value, width);
            encoder.WriteLiteral(value, width);
        }

        byte[] data = encoder.Finish();

        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < literals.Length; i++)
        {
            Assert.Equal(literals[i].Value, decoder.ReadLiteral(literals[i].Bits));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(16)]
    public void Symbols_NoAdaptation_RoundTrip(int nsymbs)
    {
        Random random = new(7 * nsymbs);
        ushort[] referenceCdf = Av1Cdf.CreateUniform(nsymbs);

        int[] symbols = new int[1000];
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < symbols.Length; i++)
        {
            symbols[i] = random.Next(nsymbs);
            encoder.WriteSymbolNoUpdate(symbols[i], referenceCdf);
        }

        byte[] data = encoder.Finish();

        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < symbols.Length; i++)
        {
            Assert.Equal(symbols[i], decoder.ReadSymbolNoUpdate(referenceCdf));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(16)]
    public void Symbols_WithAdaptation_RoundTrip(int nsymbs)
    {
        Random random = new(31 + nsymbs);

        int[] symbols = new int[2000];
        ushort[] encoderCdf = Av1Cdf.CreateUniform(nsymbs);
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < symbols.Length; i++)
        {
            // Bias the distribution so adaptation is exercised meaningfully.
            symbols[i] = random.Next(100) < 70 ? 0 : random.Next(nsymbs);
            encoder.WriteSymbol(symbols[i], encoderCdf);
        }

        byte[] data = encoder.Finish();

        ushort[] decoderCdf = Av1Cdf.CreateUniform(nsymbs);
        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < symbols.Length; i++)
        {
            Assert.Equal(symbols[i], decoder.ReadSymbol(decoderCdf));
        }

        // The adaptively-updated CDFs must remain in lock-step between encoder and decoder.
        Assert.Equal(encoderCdf, decoderCdf);
    }

    [Fact]
    public void MixedStream_RoundTrip()
    {
        Random random = new(2024);
        Av1SymbolEncoder encoder = new();
        ushort[] encoderCdf = Av1Cdf.CreateUniform(4);

        List<(int Kind, int A, int B)> ops = [];
        for (int i = 0; i < 500; i++)
        {
            int kind = random.Next(3);
            switch (kind)
            {
                case 0:
                    int bit = random.Next(2);
                    ops.Add((0, bit, 0));
                    encoder.WriteBool(bit);
                    break;
                case 1:
                    int width = random.Next(1, 13);
                    int value = random.Next(1 << width);
                    ops.Add((1, value, width));
                    encoder.WriteLiteral((uint)value, width);
                    break;
                default:
                    int symbol = random.Next(4);
                    ops.Add((2, symbol, 0));
                    encoder.WriteSymbol(symbol, encoderCdf);
                    break;
            }
        }

        byte[] data = encoder.Finish();

        Av1SymbolDecoder decoder = new(data);
        ushort[] decoderCdf = Av1Cdf.CreateUniform(4);
        foreach ((int kind, int a, int b) in ops)
        {
            switch (kind)
            {
                case 0:
                    Assert.Equal(a, decoder.ReadBool());
                    break;
                case 1:
                    Assert.Equal((uint)a, decoder.ReadLiteral(b));
                    break;
                default:
                    Assert.Equal(a, decoder.ReadSymbol(decoderCdf));
                    break;
            }
        }
    }
}
