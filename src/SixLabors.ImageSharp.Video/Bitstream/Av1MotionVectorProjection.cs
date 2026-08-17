// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Projects a temporal motion vector onto a reference by the ratio of order-hint distances, a port of
/// the reference decoder's <c>mv_projection</c> (specification section 7.9.3). Used by the temporal
/// candidate path of motion-vector prediction. The arithmetic is performed in 32-bit integers to match
/// the reference decoder's overflow behaviour.
/// </summary>
internal static class Av1MotionVectorProjection
{
    // Reciprocal multipliers (1 << 14) / den, indexed by denominator (dav1d div_mult).
    private static readonly ushort[] DivisionMultiplier =
    [
        0, 16384, 8192, 5461, 4096, 3276, 2730, 2340,
        2048, 1820, 1638, 1489, 1365, 1260, 1170, 1092,
        1024, 963, 910, 862, 819, 780, 744, 712,
        682, 655, 630, 606, 585, 564, 546, 528,
    ];

    /// <summary>
    /// Projects a motion vector by the ratio <paramref name="numerator"/> / <paramref name="denominator"/>.
    /// </summary>
    /// <param name="motionVector">The motion vector to project.</param>
    /// <param name="numerator">The order-hint distance numerator (in the range (-32, 32)).</param>
    /// <param name="denominator">The order-hint distance denominator (in the range [1, 31]).</param>
    /// <returns>The projected, rounded and clipped motion vector.</returns>
    public static Av1MotionVector Project(Av1MotionVector motionVector, int numerator, int denominator)
    {
        unchecked
        {
            int fraction = numerator * DivisionMultiplier[denominator];
            int y = motionVector.Y * fraction;
            int x = motionVector.X * fraction;
            return new Av1MotionVector(
                Clip((y + 8192 + (y >> 31)) >> 14),
                Clip((x + 8192 + (x >> 31)) >> 14));
        }
    }

    private static int Clip(int value) => Math.Clamp(value, -0x3fff, 0x3fff);
}
