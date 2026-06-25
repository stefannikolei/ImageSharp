// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the switchable interpolation (subpel) filter for an inter block, a port of the reference
/// decoder's subpel-filter parse (<c>decode.c</c>). When the sequence enables dual filtering the
/// horizontal and vertical directions are coded independently; otherwise the second direction mirrors
/// the first.
/// </summary>
internal static class Av1InterpolationFilterReader
{
    /// <summary>The default switchable filter used when a block cannot carry a subpel filter.</summary>
    public const int Regular = 0;

    /// <summary>
    /// Reads the horizontal and vertical interpolation filters.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive interpolation-filter CDFs.</param>
    /// <param name="hasSubpelFilter">Whether the block carries a switchable subpel filter.</param>
    /// <param name="dualFilter">Whether the sequence enables independent horizontal/vertical filters.</param>
    /// <param name="horizontalContext">The neighbour-derived context for the horizontal filter.</param>
    /// <param name="verticalContext">The neighbour-derived context for the vertical filter.</param>
    /// <returns>The horizontal and vertical filter indices (0 = regular, 1 = smooth, 2 = sharp).</returns>
    public static (int Horizontal, int Vertical) ReadFilters(
        Av1SymbolDecoder decoder,
        Av1InterpolationFilterCdfContext cdf,
        bool hasSubpelFilter,
        bool dualFilter,
        int horizontalContext,
        int verticalContext)
    {
        if (!hasSubpelFilter)
        {
            return (Regular, Regular);
        }

        int horizontal = decoder.ReadSymbol(cdf.Filter[0][horizontalContext]);
        if (!dualFilter)
        {
            return (horizontal, horizontal);
        }

        int vertical = decoder.ReadSymbol(cdf.Filter[1][verticalContext]);
        return (horizontal, vertical);
    }
}
