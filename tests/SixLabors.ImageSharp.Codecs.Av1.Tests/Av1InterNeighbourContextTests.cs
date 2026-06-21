// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the inter neighbour-context store (<see cref="Av1InterNeighbourContext"/>): the block
/// write-back, neighbour reads driving the reference and filter contexts, and the per-super-block-row
/// left reset.
/// </summary>
public class Av1InterNeighbourContextTests
{
    [Fact]
    public void InitialState_IsIntra()
    {
        Av1InterNeighbourContext context = new(32, 32);

        Av1ReferenceNeighbour above = context.GetAbove(5);
        Assert.True(above.IsIntra);
        Assert.Equal(-1, above.Reference0);
        Assert.Equal(3, above.Filter0);
        Assert.Equal(1, context.AboveIntra(5));
        Assert.Equal(0, context.LeftSkipMode(5));
    }

    [Fact]
    public void Write_SplatsAboveAndLeftAcrossBlock()
    {
        Av1InterNeighbourContext context = new(32, 32);

        // An inter block at (row 4, col 8), size 2x2, reference 0, filters (1, 2), skip-mode.
        context.Write(4, 8, 2, 2, isIntra: false, reference0: 0, reference1: -1, isCompound: false, filter0: 1, filter1: 2, skipMode: true);

        for (int x = 8; x < 10; x++)
        {
            Av1ReferenceNeighbour above = context.GetAbove(x);
            Assert.False(above.IsIntra);
            Assert.Equal(0, above.Reference0);
            Assert.Equal(1, above.Filter0);
            Assert.Equal(2, above.Filter1);
            Assert.Equal(0, context.AboveIntra(x));
            Assert.Equal(1, context.AboveSkipMode(x));
        }

        for (int y = 4; y < 6; y++)
        {
            Av1ReferenceNeighbour left = context.GetLeft(y);
            Assert.False(left.IsIntra);
            Assert.Equal(0, left.Reference0);
            Assert.Equal(1, context.LeftSkipMode(y));
        }

        // Outside the block stays intra.
        Assert.True(context.GetAbove(10).IsIntra);
        Assert.True(context.GetLeft(6).IsIntra);
    }

    [Fact]
    public void Write_FeedsReferenceAndFilterContexts()
    {
        Av1InterNeighbourContext context = new(32, 32);
        context.Write(4, 8, 2, 2, isIntra: false, reference0: 0, reference1: -1, isCompound: false, filter0: 1, filter1: 2, skipMode: false);

        // A block at (row 4, col 10): left neighbour is the inter block, above is intra (top of frame).
        Av1ReferenceNeighbour above = context.GetAbove(10);
        Av1ReferenceNeighbour left = context.GetLeft(4);

        int[] contexts = Av1ReferenceContext.ComputeSingleReferenceContexts(above, left, haveTop: false, haveLeft: true);

        // Only the left LAST (ref 0) neighbour contributes: matches case "left inter LAST".
        Assert.Equal(2, contexts[0]);

        int filterContext = Av1ReferenceContext.ComputeFilterContext(above, left, isCompound: false, direction: 0, reference: 0);
        Assert.Equal(1, filterContext); // above unset (3), left filter 1 -> 1
    }

    [Fact]
    public void ClearLeft_ResetsLeftButKeepsAbove()
    {
        Av1InterNeighbourContext context = new(32, 32);
        context.Write(4, 8, 2, 2, isIntra: false, reference0: 0, reference1: -1, isCompound: false, filter0: 1, filter1: 2, skipMode: true);

        context.ClearLeft();

        Assert.True(context.GetLeft(4).IsIntra);
        Assert.Equal(0, context.LeftSkipMode(4));

        // Above is untouched.
        Assert.False(context.GetAbove(8).IsIntra);
    }
}
