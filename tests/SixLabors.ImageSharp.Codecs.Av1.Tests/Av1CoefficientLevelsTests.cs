// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1CoefficientLevelsTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(70000)]
    public void BaseRange_RoundTrip(int level)
    {
        ushort[] encoderCdf = Av1Cdf.CreateUniform(Av1CoefficientLevels.BaseRangeCdfSize);
        Av1SymbolEncoder encoder = new();
        WriteBaseRange(encoder, level, encoderCdf);
        byte[] data = encoder.Finish();

        ushort[] decoderCdf = Av1Cdf.CreateUniform(Av1CoefficientLevels.BaseRangeCdfSize);
        Av1SymbolDecoder decoder = new(data);
        int decoded = Av1CoefficientLevels.ReadBaseRange(decoder, decoderCdf);

        Assert.Equal(level, decoded);

        // The adaptively-updated CDFs must remain in lock-step between encoder and decoder.
        Assert.Equal(encoderCdf, decoderCdf);
    }

    [Fact]
    public void BaseRange_RoundTrip_Sequence()
    {
        Random random = new(1234);
        int[] levels = new int[500];
        ushort[] encoderCdf = Av1Cdf.CreateUniform(Av1CoefficientLevels.BaseRangeCdfSize);
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < levels.Length; i++)
        {
            // Skew towards the base-range values while still exercising the Golomb residual.
            levels[i] = random.Next(2) == 0 ? random.Next(3, 16) : random.Next(15, 5000);
            WriteBaseRange(encoder, levels[i], encoderCdf);
        }

        byte[] data = encoder.Finish();

        ushort[] decoderCdf = Av1Cdf.CreateUniform(Av1CoefficientLevels.BaseRangeCdfSize);
        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < levels.Length; i++)
        {
            Assert.Equal(levels[i], Av1CoefficientLevels.ReadBaseRange(decoder, decoderCdf));
        }

        Assert.Equal(encoderCdf, decoderCdf);
    }

    /// <summary>
    /// The inverse of <see cref="Av1CoefficientLevels.ReadBaseRange"/>: emits the coeff_base_range
    /// symbols followed by an Exp-Golomb residual when the level saturates.
    /// </summary>
    private static void WriteBaseRange(Av1SymbolEncoder encoder, int level, Span<ushort> baseRangeCdf)
    {
        const int baseLevel = 1 + Av1CoefficientLevels.NumBaseLevels;
        const int maxSymbol = Av1CoefficientLevels.BaseRangeCdfSize - 1;
        int remaining = Math.Min(level, Av1CoefficientLevels.MaxBaseRangeLevel) - baseLevel;
        for (int index = 0; index < Av1CoefficientLevels.CoefficientBaseRange; index += maxSymbol)
        {
            int coefficientBaseRange = Math.Min(remaining, maxSymbol);
            encoder.WriteSymbol(coefficientBaseRange, baseRangeCdf);
            remaining -= coefficientBaseRange;
            if (coefficientBaseRange < maxSymbol)
            {
                break;
            }
        }

        if (level >= Av1CoefficientLevels.MaxBaseRangeLevel)
        {
            encoder.WriteGolomb((uint)(level - Av1CoefficientLevels.MaxBaseRangeLevel));
        }
    }
}
