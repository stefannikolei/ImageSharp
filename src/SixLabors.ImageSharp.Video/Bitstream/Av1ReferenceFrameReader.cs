// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the single-reference frame selection for an inter block, a port of the reference decoder's
/// binary-tree reference parse (<c>decode.c</c>). The result is a reference index in the range [0, 6]
/// (LAST, LAST2, LAST3, GOLDEN, BWDREF, ALTREF2, ALTREF). The neighbour-derived bit contexts are
/// supplied by the caller, one per reference CDF bit.
/// </summary>
internal static class Av1ReferenceFrameReader
{
    /// <summary>
    /// Reads the single-reference index for an inter block.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="contexts">
    /// The neighbour-derived bit contexts, indexed by reference CDF bit position [0, 5].
    /// </param>
    /// <returns>The decoded reference index in the range [0, 6].</returns>
    public static int ReadSingleReference(
        Av1SymbolDecoder decoder,
        Av1InterModeCdfContext cdf,
        ReadOnlySpan<int> contexts)
    {
        if (decoder.ReadSymbol(cdf.SingleReference[0][contexts[0]]) != 0)
        {
            if (decoder.ReadSymbol(cdf.SingleReference[1][contexts[1]]) != 0)
            {
                return 6;
            }

            return 4 + decoder.ReadSymbol(cdf.SingleReference[5][contexts[5]]);
        }

        if (decoder.ReadSymbol(cdf.SingleReference[2][contexts[2]]) != 0)
        {
            return 2 + decoder.ReadSymbol(cdf.SingleReference[4][contexts[4]]);
        }

        return decoder.ReadSymbol(cdf.SingleReference[3][contexts[3]]);
    }
}
