// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the is-inter flag that selects between intra and inter prediction for a block in an inter
/// frame, a port of the reference decoder's intra-flag parse and its neighbour context derivation
/// (<c>get_intra_ctx</c>).
/// </summary>
internal static class Av1IsInterReader
{
    /// <summary>
    /// Reads whether a block uses inter prediction.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="context">The neighbour-derived intra context.</param>
    /// <returns><see langword="true"/> when the block uses inter prediction.</returns>
    public static bool ReadIsInter(Av1SymbolDecoder decoder, Av1InterModeCdfContext cdf, int context)
        => decoder.ReadSymbol(cdf.IsInter[context]) != 0;

    /// <summary>
    /// Reads the skip-mode flag for an inter block, a port of the reference decoder's skip-mode parse.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="leftSkipMode">Whether the left neighbour used skip mode (0 or 1).</param>
    /// <param name="topSkipMode">Whether the top neighbour used skip mode (0 or 1).</param>
    /// <returns><see langword="true"/> when the block uses skip mode.</returns>
    public static bool ReadSkipMode(Av1SymbolDecoder decoder, Av1InterModeCdfContext cdf, int leftSkipMode, int topSkipMode)
        => decoder.ReadSymbol(cdf.SkipMode[leftSkipMode + topSkipMode]) != 0;

    /// <summary>
    /// Derives the intra-flag context from the neighbouring blocks' intra flags, matching
    /// <c>get_intra_ctx</c>.
    /// </summary>
    /// <param name="leftIntra">Whether the left neighbour is intra (0 or 1).</param>
    /// <param name="topIntra">Whether the top neighbour is intra (0 or 1).</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <param name="haveTop">Whether a top neighbour is available.</param>
    /// <returns>The intra context in the range [0, 3].</returns>
    public static int GetIntraContext(int leftIntra, int topIntra, bool haveLeft, bool haveTop)
    {
        if (haveLeft)
        {
            if (haveTop)
            {
                int context = leftIntra + topIntra;
                return context + (context == 2 ? 1 : 0);
            }

            return leftIntra * 2;
        }

        return haveTop ? topIntra * 2 : 0;
    }
}
