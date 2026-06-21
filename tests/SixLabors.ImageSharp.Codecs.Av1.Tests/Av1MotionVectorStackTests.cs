// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the single-reference motion-vector candidate assembly (<see cref="Av1MotionVectorStack"/>):
/// spatial candidate merging and weighting, global-motion substitution, the nearest-weight bonus and
/// secondary sort, and the inter-mode context derivation, all against the reference decoder's algorithm.
/// </summary>
public class Av1MotionVectorStackTests
{
    private static Av1RefMvsBlock Inter(int reference, int y, int x, bool isNewMv = false, bool isGlobalMv = false)
        => new(new Av1MotionVector(y, x), default, reference, -1, Av1BlockSize.Block8x8, isNewMv, isGlobalMv, isIntra: false);

    private static void AddSpatial(Av1MotionVectorStack stack, in Av1RefMvsBlock block, int weight, int reference, Av1MotionVector global, bool globalValid)
    {
        bool newMv = false;
        bool refMv = false;
        stack.AddSpatialCandidate(block, weight, reference, global, globalValid, ref newMv, ref refMv);
    }

    [Fact]
    public void AddSpatialCandidate_MergesEqualVectorsAndAccumulatesWeight()
    {
        Av1MotionVectorStack stack = new();
        Av1MotionVector noGlobal = default;
        bool newMv = false;
        bool refMv = false;

        stack.AddSpatialCandidate(Inter(1, 10, 20, isNewMv: true), 4, 1, noGlobal, false, ref newMv, ref refMv);
        stack.AddSpatialCandidate(Inter(1, 10, 20), 6, 1, noGlobal, false, ref newMv, ref refMv);
        stack.AddSpatialCandidate(Inter(1, 5, 5), 2, 1, noGlobal, false, ref newMv, ref refMv);
        stack.AddSpatialCandidate(Inter(2, 7, 7), 8, 1, noGlobal, false, ref newMv, ref refMv); // different reference
        stack.AddSpatialCandidate(
            new Av1RefMvsBlock(default, default, 1, -1, Av1BlockSize.Block8x8, false, false, isIntra: true),
            8,
            1,
            noGlobal,
            false,
            ref newMv,
            ref refMv);

        Assert.Equal(2, stack.Count);
        Assert.True(refMv);
        Assert.True(newMv);
        Assert.Equal(10, stack[0].MotionVector.Y);
        Assert.Equal(20, stack[0].MotionVector.X);
        Assert.Equal(10, stack[0].Weight);
        Assert.Equal(5, stack[1].MotionVector.Y);
        Assert.Equal(2, stack[1].Weight);
    }

    [Fact]
    public void AddSpatialCandidate_SubstitutesGlobalMotionWhenValid()
    {
        Av1MotionVector global = new(100, 200);

        Av1MotionVectorStack withGlobal = new();
        AddSpatial(withGlobal, Inter(1, 10, 20, isGlobalMv: true), 4, 1, global, globalValid: true);
        Assert.Equal(100, withGlobal[0].MotionVector.Y);
        Assert.Equal(200, withGlobal[0].MotionVector.X);

        Av1MotionVectorStack withoutGlobal = new();
        AddSpatial(withoutGlobal, Inter(1, 10, 20, isGlobalMv: true), 4, 1, global, globalValid: false);
        Assert.Equal(10, withoutGlobal[0].MotionVector.Y);
        Assert.Equal(20, withoutGlobal[0].MotionVector.X);
    }

    [Fact]
    public void ApplyNearestWeightBonusAndSort_OrdersByDescendingWeight()
    {
        Av1MotionVectorStack stack = new();
        Av1MotionVector noGlobal = default;
        AddSpatial(stack, Inter(1, 1, 1), 4, 1, noGlobal, false);
        AddSpatial(stack, Inter(1, 2, 2), 10, 1, noGlobal, false);
        AddSpatial(stack, Inter(1, 3, 3), 2, 1, noGlobal, false);

        stack.ApplyNearestWeightBonus(3);
        stack.Sort(3);

        Assert.Equal(2, stack[0].MotionVector.Y);
        Assert.Equal(650, stack[0].Weight);
        Assert.Equal(1, stack[1].MotionVector.Y);
        Assert.Equal(644, stack[1].Weight);
        Assert.Equal(3, stack[2].MotionVector.Y);
        Assert.Equal(642, stack[2].Weight);
    }

    [Theory]
    [InlineData(0, 0, false, 0, 0)]
    [InlineData(0, 2, true, 1, 2)]
    [InlineData(1, 1, false, 3, 3)]
    [InlineData(1, 2, true, 2, 4)]
    [InlineData(2, 9, true, 4, 5)]
    public void DeriveContexts_MatchesReference(
        int nearestMatch,
        int referenceMatchCount,
        bool haveNewMv,
        int expectedNewMv,
        int expectedRefMv)
    {
        (int newMv, int refMv) = Av1MotionVectorStack.DeriveContexts(nearestMatch, referenceMatchCount, haveNewMv);
        Assert.Equal(expectedNewMv, newMv);
        Assert.Equal(expectedRefMv, refMv);
    }

    [Fact]
    public void ComposeContext_PacksFields()
        => Assert.Equal(58, Av1MotionVectorStack.ComposeContext(refMvContext: 3, globalMvContext: 1, newMvContext: 2));

    [Fact]
    public void ScanRow_EqualWidthAddsWeightedCandidate()
    {
        Av1RefMvsBlock[] row = [Inter(1, 10, 20)];
        Av1MotionVectorStack stack = new();
        bool newMv = false;
        bool refMv = false;

        int extent = stack.ScanRow(row, bw4: 2, w4: 2, maxRows: 2, step: 1, referenceFrame: 1, default, false, ref newMv, ref refMv);

        Assert.Equal(1, extent);
        Assert.Equal(1, stack.Count);
        Assert.Equal(10, stack[0].MotionVector.Y);
        Assert.Equal(4, stack[0].Weight); // len(2) * weight(2)
    }

    [Fact]
    public void ScanRow_WideBlockWalksNeighbours()
    {
        Av1RefMvsBlock[] row = [Inter(1, 1, 1), default, Inter(1, 2, 2), default];
        Av1MotionVectorStack stack = new();
        bool newMv = false;
        bool refMv = false;

        int extent = stack.ScanRow(row, bw4: 4, w4: 4, maxRows: 2, step: 1, referenceFrame: 1, default, false, ref newMv, ref refMv);

        Assert.Equal(1, extent);
        Assert.Equal(2, stack.Count);
        Assert.Equal(1, stack[0].MotionVector.Y);
        Assert.Equal(4, stack[0].Weight); // len(2) * 2
        Assert.Equal(2, stack[1].MotionVector.Y);
        Assert.Equal(4, stack[1].Weight);
    }

    [Fact]
    public void ScanColumn_EqualHeightAddsWeightedCandidate()
    {
        Av1RefMvsBlock[] column = [Inter(1, 30, 40)];
        Av1MotionVectorStack stack = new();
        bool newMv = false;
        bool refMv = false;

        int extent = stack.ScanColumn(column, bh4: 2, h4: 2, maxColumns: 2, step: 1, referenceFrame: 1, default, false, ref newMv, ref refMv);

        Assert.Equal(1, extent);
        Assert.Equal(1, stack.Count);
        Assert.Equal(30, stack[0].MotionVector.Y);
        Assert.Equal(4, stack[0].Weight);
    }

    [Fact]
    public void AddSingleExtendedCandidate_AppliesSignBiasAndDeduplicates()
    {
        int[] signBias = [0, 0, 1, 0, 0, 0, 0];
        Av1MotionVectorStack stack = new();

        // Reference 3 has opposite sign bias to the predicted sign 0, so the vector is negated.
        stack.AddSingleExtendedCandidate(Inter(3, 10, -20), sign: 0, signBias);
        Assert.Equal(1, stack.Count);
        Assert.Equal(-10, stack[0].MotionVector.Y);
        Assert.Equal(20, stack[0].MotionVector.X);
        Assert.Equal(2, stack[0].Weight);

        // The same negated vector is not added twice.
        stack.AddSingleExtendedCandidate(Inter(3, 10, -20), sign: 0, signBias);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void FillGlobalPredictors_FillsBelowTwoWithoutChangingCount()
    {
        Av1MotionVectorStack stack = new();
        AddSpatial(stack, Inter(1, 7, 9), 4, 1, default, false);
        Assert.Equal(1, stack.Count);

        stack.FillGlobalPredictors(new Av1MotionVector(3, -5));
        Assert.Equal(1, stack.Count);
        Assert.Equal(3, stack[1].MotionVector.Y);
        Assert.Equal(-5, stack[1].MotionVector.X);
    }

    [Fact]
    public void Clamp_RestrictsVectorsToBlockRange()
    {
        Av1MotionVectorStack stack = new();
        AddSpatial(stack, Inter(1, 1_000_000, -1_000_000), 4, 1, default, false);

        // bx4=0, bw4=2, by4=0, bh4=2, iw4=ih4=64.
        stack.Clamp(bx4: 0, bw4: 2, by4: 0, bh4: 2, imageWidth4: 64, imageHeight4: 64);

        int bottom = (64 - 0 + 4) * 4 * 8;
        int left = -(0 + 2 + 4) * 4 * 8;
        Assert.Equal(bottom, stack[0].MotionVector.Y);
        Assert.Equal(left, stack[0].MotionVector.X);
    }
}
