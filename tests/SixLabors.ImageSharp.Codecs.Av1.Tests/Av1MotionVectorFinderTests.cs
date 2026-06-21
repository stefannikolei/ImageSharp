// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the spatial single-reference MV candidate finder (<see cref="Av1MotionVectorFinder"/>)
/// against the reference decoder's algorithm: neighbour scanning over the reference grid, candidate
/// assembly, the global-motion predictor fill and the composed mode context.
/// </summary>
public class Av1MotionVectorFinderTests
{
    private static readonly int[] SignBias = new int[7];

    private static Av1MotionVectorGrid IntraGrid(int columns4, int rows4)
    {
        Av1MotionVectorGrid grid = new(columns4, rows4);
        Av1RefMvsBlock intra = new(default, default, 0, -1, Av1BlockSize.Block8x8, false, false, isIntra: true);
        grid.Fill(0, 0, columns4, rows4, intra);
        return grid;
    }

    [Fact]
    public void Find_SingleTopNeighbour_ProducesCandidateAndContext()
    {
        Av1MotionVectorGrid grid = IntraGrid(32, 32);

        // A matching top neighbour (reference 1) at row 7, columns 8-9 with MV (0, 32).
        Av1RefMvsBlock top = new(new Av1MotionVector(0, 32), default, 1, -1, Av1BlockSize.Block8x8, false, false, isIntra: false);
        grid.Fill(7, 8, 2, 1, top);

        Av1MotionVectorStack stack = new();
        Av1TileBounds bounds = new(0, 32, 0, 32);
        (int count, int context) = Av1MotionVectorFinder.Find(
            grid, stack, bx4: 8, by4: 8, Av1BlockSize.Block8x8, referenceFrame: 1, bounds,
            topRightAvailable: false, imageWidth4: 32, imageHeight4: 32,
            globalMv: default, globalMvSubstitution: false, SignBias);

        Assert.Equal(1, count);
        Assert.Equal(0, stack[0].MotionVector.Y);
        Assert.Equal(32, stack[0].MotionVector.X);

        // The second slot is filled with the global-motion predictor (0, 0).
        Assert.Equal(0, stack[1].MotionVector.Y);
        Assert.Equal(0, stack[1].MotionVector.X);

        // nearest_match = 1 (row only), ref_match = 1 -> newmv_ctx = 3, refmv_ctx = 3 -> ctx = (3<<4)|3.
        Assert.Equal((3 << 4) | 3, context);
    }

    [Fact]
    public void Find_TopAndLeftSameVector_MergesWeightAndRaisesContext()
    {
        Av1MotionVectorGrid grid = IntraGrid(32, 32);

        Av1RefMvsBlock match = new(new Av1MotionVector(8, -16), default, 1, -1, Av1BlockSize.Block8x8, false, false, isIntra: false);
        grid.Fill(7, 8, 2, 1, match); // top
        grid.Fill(8, 7, 1, 2, match); // left

        Av1MotionVectorStack stack = new();
        Av1TileBounds bounds = new(0, 32, 0, 32);
        (int count, int context) = Av1MotionVectorFinder.Find(
            grid, stack, bx4: 8, by4: 8, Av1BlockSize.Block8x8, referenceFrame: 1, bounds,
            topRightAvailable: false, imageWidth4: 32, imageHeight4: 32,
            globalMv: default, globalMvSubstitution: false, SignBias);

        Assert.Equal(1, count);
        Assert.Equal(8, stack[0].MotionVector.Y);
        Assert.Equal(-16, stack[0].MotionVector.X);

        // nearest_match = 2 (row and column) -> refmv_ctx = 5, newmv_ctx = 5; ctx = (5<<4)|5.
        Assert.Equal((5 << 4) | 5, context);
    }

    [Fact]
    public void Find_NoNeighbours_FillsGlobalPredictorOnly()
    {
        Av1MotionVectorGrid grid = IntraGrid(32, 32);

        Av1MotionVectorStack stack = new();
        Av1TileBounds bounds = new(0, 32, 0, 32);

        // Top-left corner block: no top, no left neighbours scanned.
        (int count, int context) = Av1MotionVectorFinder.Find(
            grid, stack, bx4: 0, by4: 0, Av1BlockSize.Block8x8, referenceFrame: 1, bounds,
            topRightAvailable: false, imageWidth4: 32, imageHeight4: 32,
            globalMv: new Av1MotionVector(4, 4), globalMvSubstitution: false, SignBias);

        Assert.Equal(0, count);
        Assert.Equal(4, stack[0].MotionVector.Y);
        Assert.Equal(4, stack[0].MotionVector.X);
        Assert.Equal(0, context); // no matches -> all contexts zero
    }
}
