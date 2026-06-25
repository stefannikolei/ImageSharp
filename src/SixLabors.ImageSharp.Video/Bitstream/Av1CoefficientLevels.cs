// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Reads the magnitude of a coefficient level once the base token has signalled that it is at least
/// <c>1 + NUM_BASE_LEVELS</c> (specification section 5.11.39, the <c>coeff_base_range</c> loop
/// followed by an Exp-Golomb residual).
/// </summary>
internal static class Av1CoefficientLevels
{
    /// <summary>The number of base levels coded by the coeff_base symbol.</summary>
    public const int NumBaseLevels = 2;

    /// <summary>The total magnitude coded by the base-range symbols.</summary>
    public const int CoefficientBaseRange = 12;

    /// <summary>The number of symbols in a base-range CDF (values 0..3).</summary>
    public const int BaseRangeCdfSize = 4;

    /// <summary>The level at which the base-range coding saturates and an Exp-Golomb residual follows.</summary>
    public const int MaxBaseRangeLevel = NumBaseLevels + 1 + CoefficientBaseRange; // 15

    /// <summary>
    /// Reads the coeff_base_range "high" token only (the value 3..15), without the trailing
    /// Exp-Golomb residual. This matches dav1d's <c>decode_hi_tok</c>; in the full coefficient
    /// reader the Golomb residual is decoded later, during the sign/residual pass.
    /// </summary>
    /// <param name="decoder">The symbol decoder.</param>
    /// <param name="baseRangeCdf">The adaptive coeff_base_range CDF (4 symbols).</param>
    /// <returns>The decoded high token in the range [3, 15].</returns>
    public static int ReadHighToken(Av1SymbolDecoder decoder, Span<ushort> baseRangeCdf)
    {
        int level = 1 + NumBaseLevels;
        for (int index = 0; index < CoefficientBaseRange; index += BaseRangeCdfSize - 1)
        {
            int coefficientBaseRange = decoder.ReadSymbol(baseRangeCdf);
            level += coefficientBaseRange;
            if (coefficientBaseRange < BaseRangeCdfSize - 1)
            {
                break;
            }
        }

        return level;
    }

    /// <summary>
    /// Reads the full coefficient level magnitude, starting from the saturated base level
    /// <c>1 + NUM_BASE_LEVELS</c>.
    /// </summary>
    /// <param name="decoder">The symbol decoder.</param>
    /// <param name="baseRangeCdf">The adaptive coeff_base_range CDF (4 symbols).</param>
    /// <returns>The decoded level (at least <c>1 + NUM_BASE_LEVELS</c>).</returns>
    public static int ReadBaseRange(Av1SymbolDecoder decoder, Span<ushort> baseRangeCdf)
    {
        int level = ReadHighToken(decoder, baseRangeCdf);
        if (level >= MaxBaseRangeLevel)
        {
            level += (int)decoder.ReadGolomb();
        }

        return level;
    }
}
