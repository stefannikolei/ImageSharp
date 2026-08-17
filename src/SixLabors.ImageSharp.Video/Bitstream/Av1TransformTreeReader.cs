// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Reads the variable-transform tree of an inter block, a port of the reference decoder's
/// <c>read_tx_tree</c> (<c>decode.c</c>). Starting from the block's maximum transform size it recursively
/// reads a split flag at each node (when the depth and size allow), recording which nodes split in a
/// per-depth bit mask and writing the resulting transform-size category into the neighbouring transform
/// arrays. Coordinates are in frame 4x4 units; the neighbour arrays store transform width/height
/// categories (log2 of the size in 4x4 units).
/// </summary>
internal static class Av1TransformTreeReader
{
    // The square transform-size category of a 64x64 transform (dav1d TX_64X64).
    private const int MaxSquareCategory = 4;

    /// <summary>
    /// Reads a (sub-)tree of the variable-transform partition.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="splitCdf">The transform-partition split CDFs, indexed by category then context.</param>
    /// <param name="from">The transform size at this node.</param>
    /// <param name="depth">The recursion depth (0 at the block's maximum transform).</param>
    /// <param name="xOffset">The node's horizontal position within the block (in node units).</param>
    /// <param name="yOffset">The node's vertical position within the block (in node units).</param>
    /// <param name="masks">The per-depth split bit masks (length at least three).</param>
    /// <param name="aboveTx">The above transform-size-category neighbour array, indexed by 4x4 column.</param>
    /// <param name="leftTx">The left transform-size-category neighbour array, indexed by 4x4 row.</param>
    /// <param name="bx4">The node column in frame 4x4 units.</param>
    /// <param name="by4">The node row in frame 4x4 units.</param>
    /// <param name="frameWidth4">The frame width in 4x4 units.</param>
    /// <param name="frameHeight4">The frame height in 4x4 units.</param>
    public static void Read(
        Av1SymbolDecoder decoder,
        ushort[][][] splitCdf,
        Av1TransformSize from,
        int depth,
        int xOffset,
        int yOffset,
        ushort[] masks,
        sbyte[] aboveTx,
        sbyte[] leftTx,
        int bx4,
        int by4,
        int frameWidth4,
        int frameHeight4)
    {
        int categoryWidth = from.GetWidthLog2() - 2;
        int categoryHeight = from.GetHeightLog2() - 2;
        int maxCategory = Math.Max(categoryWidth, categoryHeight);

        bool isSplit;
        if (depth < 2 && from != Av1TransformSize.Size4x4)
        {
            int category = (2 * (MaxSquareCategory - maxCategory)) - depth;
            int above = aboveTx[bx4] < categoryWidth ? 1 : 0;
            int left = leftTx[by4] < categoryHeight ? 1 : 0;
            isSplit = decoder.ReadSymbol(splitCdf[category][above + left]) != 0;
            if (isSplit)
            {
                masks[depth] |= (ushort)(1 << ((yOffset * 4) + xOffset));
            }
        }
        else
        {
            isSplit = false;
        }

        if (isSplit && maxCategory > 1)
        {
            Av1TransformSize sub = from.GetSubSize();
            int subWidth4 = sub.GetWidth() >> 2;
            int subHeight4 = sub.GetHeight() >> 2;

            Read(decoder, splitCdf, sub, depth + 1, (xOffset * 2) + 0, (yOffset * 2) + 0, masks, aboveTx, leftTx, bx4, by4, frameWidth4, frameHeight4);
            if (categoryWidth >= categoryHeight && bx4 + subWidth4 < frameWidth4)
            {
                Read(decoder, splitCdf, sub, depth + 1, (xOffset * 2) + 1, (yOffset * 2) + 0, masks, aboveTx, leftTx, bx4 + subWidth4, by4, frameWidth4, frameHeight4);
            }

            if (categoryHeight >= categoryWidth && by4 + subHeight4 < frameHeight4)
            {
                Read(decoder, splitCdf, sub, depth + 1, (xOffset * 2) + 0, (yOffset * 2) + 1, masks, aboveTx, leftTx, bx4, by4 + subHeight4, frameWidth4, frameHeight4);
                if (categoryWidth >= categoryHeight && bx4 + subWidth4 < frameWidth4)
                {
                    Read(decoder, splitCdf, sub, depth + 1, (xOffset * 2) + 1, (yOffset * 2) + 1, masks, aboveTx, leftTx, bx4 + subWidth4, by4 + subHeight4, frameWidth4, frameHeight4);
                }
            }
        }
        else
        {
            sbyte aboveValue = (sbyte)(isSplit ? 0 : categoryWidth);
            sbyte leftValue = (sbyte)(isSplit ? 0 : categoryHeight);
            int width4 = from.GetWidth() >> 2;
            int height4 = from.GetHeight() >> 2;
            for (int x = 0; x < width4 && bx4 + x < aboveTx.Length; x++)
            {
                aboveTx[bx4 + x] = aboveValue;
            }

            for (int y = 0; y < height4 && by4 + y < leftTx.Length; y++)
            {
                leftTx[by4 + y] = leftValue;
            }
        }
    }
}
