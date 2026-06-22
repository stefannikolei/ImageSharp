// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the temporal motion-vector projection (<see cref="Av1MotionVectorProjection"/>) against
/// values generated from the reference decoder's <c>mv_projection</c>.
/// </summary>
public class Av1MotionVectorProjectionTests
{
    [Theory]
    [InlineData(100, 200, 2, 4, 50, 100)]
    [InlineData(-100, 200, 3, 8, -38, 75)]
    [InlineData(1000, -500, 1, 1, 1000, -500)]
    [InlineData(0, 0, 5, 3, 0, 0)]
    [InlineData(8000, -8000, 7, 2, 16383, -16383)] // clipped
    [InlineData(-3, 31, -5, 16, 1, -10)]
    [InlineData(16000, 16000, 31, 1, -16383, -16383)] // 32-bit overflow wraps, then clips
    public void Project_MatchesReference(int y, int x, int numerator, int denominator, int expectedY, int expectedX)
    {
        Av1MotionVector result = Av1MotionVectorProjection.Project(new Av1MotionVector(y, x), numerator, denominator);
        Assert.Equal(expectedY, result.Y);
        Assert.Equal(expectedX, result.X);
    }
}
