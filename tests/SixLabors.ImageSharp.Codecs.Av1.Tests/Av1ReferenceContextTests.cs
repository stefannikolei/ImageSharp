// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the inter reference and interpolation-filter context derivation
/// (<see cref="Av1ReferenceContext"/>) against values generated from the reference decoder's
/// <c>av1_get_*_ref_ctx</c> and <c>get_filter_ctx</c> functions.
/// </summary>
public class Av1ReferenceContextTests
{
    private static Av1ReferenceNeighbour Intra() => new(true, -1, -1, false, 3, 3);

    private static Av1ReferenceNeighbour Inter(int ref0, int filter0 = 3, int filter1 = 3)
        => new(false, ref0, -1, false, filter0, filter1);

    private static Av1ReferenceNeighbour Compound(int ref0, int ref1)
        => new(false, ref0, ref1, true, 3, 3);

    private static (Av1ReferenceNeighbour Above, Av1ReferenceNeighbour Left, bool HaveTop, bool HaveLeft, int[] Expected) Case(int index)
        => index switch
        {
            0 => (Intra(), Intra(), false, false, [1, 1, 1, 1, 1, 1]),
            1 => (Inter(0, 1), Intra(), true, false, [2, 1, 2, 2, 1, 1]),
            2 => (Inter(6, 2), Intra(), true, false, [0, 0, 1, 1, 1, 1]),
            3 => (Inter(4), Inter(2, 1, 1), true, true, [1, 2, 0, 1, 2, 2]),
            4 => (Compound(0, 5), Inter(3), true, true, [2, 2, 1, 2, 0, 0]),
            _ => (Intra(), Inter(1, 2, 2), true, true, [2, 1, 2, 0, 1, 1]),
        };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ComputeSingleReferenceContexts_MatchesReference(int index)
    {
        (Av1ReferenceNeighbour above, Av1ReferenceNeighbour left, bool haveTop, bool haveLeft, int[] expected) = Case(index);
        int[] actual = Av1ReferenceContext.ComputeSingleReferenceContexts(above, left, haveTop, haveLeft);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeFilterContext_MatchesReference()
    {
        Av1ReferenceNeighbour both = Inter(0, 1, 2);
        Assert.Equal(1, Av1ReferenceContext.ComputeFilterContext(both, both, isCompound: false, direction: 0, reference: 0));
        Assert.Equal(2, Av1ReferenceContext.ComputeFilterContext(both, both, isCompound: false, direction: 1, reference: 0));

        Av1ReferenceNeighbour leftMismatch = new(false, 3, -1, false, 0, 0);
        Assert.Equal(1, Av1ReferenceContext.ComputeFilterContext(both, leftMismatch, isCompound: false, direction: 0, reference: 0));

        Av1ReferenceNeighbour aboveMismatch = Inter(5);
        Assert.Equal(5, Av1ReferenceContext.ComputeFilterContext(aboveMismatch, both, isCompound: true, direction: 0, reference: 0));
    }
}
