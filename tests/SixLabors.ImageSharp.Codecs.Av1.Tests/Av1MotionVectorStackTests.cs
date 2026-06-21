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

    [Fact]
    public void AddSpatialCandidate_MergesEqualVectorsAndAccumulatesWeight()
    {
        Av1MotionVectorStack stack = new();
        Av1MotionVector noGlobal = default;

        stack.AddSpatialCandidate(Inter(1, 10, 20, isNewMv: true), 4, 1, noGlobal, globalMvValid: false);
        stack.AddSpatialCandidate(Inter(1, 10, 20), 6, 1, noGlobal, globalMvValid: false);
        stack.AddSpatialCandidate(Inter(1, 5, 5), 2, 1, noGlobal, globalMvValid: false);
        stack.AddSpatialCandidate(Inter(2, 7, 7), 8, 1, noGlobal, globalMvValid: false); // different reference
        stack.AddSpatialCandidate(
            new Av1RefMvsBlock(default, default, 1, -1, Av1BlockSize.Block8x8, false, false, isIntra: true),
            8,
            1,
            noGlobal,
            globalMvValid: false);

        Assert.Equal(2, stack.Count);
        Assert.True(stack.HaveReferenceMv);
        Assert.True(stack.HaveNewMv);
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
        withGlobal.AddSpatialCandidate(Inter(1, 10, 20, isGlobalMv: true), 4, 1, global, globalMvValid: true);
        Assert.Equal(100, withGlobal[0].MotionVector.Y);
        Assert.Equal(200, withGlobal[0].MotionVector.X);

        Av1MotionVectorStack withoutGlobal = new();
        withoutGlobal.AddSpatialCandidate(Inter(1, 10, 20, isGlobalMv: true), 4, 1, global, globalMvValid: false);
        Assert.Equal(10, withoutGlobal[0].MotionVector.Y);
        Assert.Equal(20, withoutGlobal[0].MotionVector.X);
    }

    [Fact]
    public void ApplyNearestWeightBonusAndSort_OrdersByDescendingWeight()
    {
        Av1MotionVectorStack stack = new();
        Av1MotionVector noGlobal = default;
        stack.AddSpatialCandidate(Inter(1, 1, 1), 4, 1, noGlobal, globalMvValid: false);
        stack.AddSpatialCandidate(Inter(1, 2, 2), 10, 1, noGlobal, globalMvValid: false);
        stack.AddSpatialCandidate(Inter(1, 3, 3), 2, 1, noGlobal, globalMvValid: false);

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
        stack.AddSpatialCandidate(Inter(1, 7, 9), 4, 1, default, globalMvValid: false);
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
        stack.AddSpatialCandidate(Inter(1, 1_000_000, -1_000_000), 4, 1, default, globalMvValid: false);

        // bx4=0, bw4=2, by4=0, bh4=2, iw4=ih4=64.
        stack.Clamp(bx4: 0, bw4: 2, by4: 0, bh4: 2, imageWidth4: 64, imageHeight4: 64);

        int bottom = (64 - 0 + 4) * 4 * 8;
        int left = -(0 + 2 + 4) * 4 * 8;
        Assert.Equal(bottom, stack[0].MotionVector.Y);
        Assert.Equal(left, stack[0].MotionVector.X);
    }
}
