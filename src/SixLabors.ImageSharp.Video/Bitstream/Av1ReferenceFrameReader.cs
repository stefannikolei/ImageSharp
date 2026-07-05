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

    /// <summary>
    /// Decodes a compound reference pair once the compound flag is set (dav1d's compound branch of the
    /// reference parse): a direction flag selects bidirectional (a forward and a backward reference) or
    /// unidirectional coding.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="above">The above neighbour.</param>
    /// <param name="left">The left neighbour.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <param name="contexts">The single-reference bit contexts (shared with the compound trees).</param>
    /// <returns>The zero-based reference pair.</returns>
    public static (int Reference0, int Reference1) ReadCompoundReferences(
        Av1SymbolDecoder decoder,
        Av1InterModeCdfContext cdf,
        in Av1ReferenceNeighbour above,
        in Av1ReferenceNeighbour left,
        bool haveTop,
        bool haveLeft,
        ReadOnlySpan<int> contexts)
    {
        int directionContext = Av1ReferenceContext.ComputeCompoundDirectionContext(above, left, haveTop, haveLeft);
        if (decoder.ReadSymbol(cdf.CompoundDirection[directionContext]) != 0)
        {
            // Bidirectional: forward reference then backward reference.
            int reference0;
            if (decoder.ReadSymbol(cdf.CompoundForwardReference[0][contexts[2]]) != 0)
            {
                reference0 = 2 + decoder.ReadSymbol(cdf.CompoundForwardReference[2][contexts[4]]);
            }
            else
            {
                reference0 = decoder.ReadSymbol(cdf.CompoundForwardReference[1][contexts[3]]);
            }

            int reference1;
            if (decoder.ReadSymbol(cdf.CompoundBackwardReference[0][contexts[1]]) != 0)
            {
                reference1 = 6;
            }
            else
            {
                reference1 = 4 + decoder.ReadSymbol(cdf.CompoundBackwardReference[1][contexts[5]]);
            }

            return (reference0, reference1);
        }

        // Unidirectional.
        if (decoder.ReadSymbol(cdf.CompoundUniReference[0][contexts[0]]) != 0)
        {
            return (4, 6);
        }

        int uniP1Context = Av1ReferenceContext.ComputeUniP1Context(above, left, haveTop, haveLeft);
        int second = 1 + decoder.ReadSymbol(cdf.CompoundUniReference[1][uniP1Context]);
        if (second == 2)
        {
            second += decoder.ReadSymbol(cdf.CompoundUniReference[2][contexts[4]]);
        }

        return (0, second);
    }
}
