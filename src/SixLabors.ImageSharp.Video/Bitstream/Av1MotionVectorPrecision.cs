// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Adjusts motion-vector precision to the frame's allowed resolution, a port of the reference decoder's
/// <c>fix_mv_precision</c> and <c>fix_int_mv_precision</c> (<c>env.h</c>).
/// </summary>
internal static class Av1MotionVectorPrecision
{
    /// <summary>
    /// Rounds a motion vector to the frame's precision: whole pels when force-integer is set, even
    /// (quarter-pel) values when high precision is disabled, otherwise unchanged.
    /// </summary>
    /// <param name="motionVector">The motion vector to adjust.</param>
    /// <param name="allowHighPrecision">Whether eighth-pel precision is allowed.</param>
    /// <param name="forceInteger">Whether motion vectors are forced to whole pels.</param>
    /// <returns>The precision-adjusted motion vector.</returns>
    public static Av1MotionVector Fix(Av1MotionVector motionVector, bool allowHighPrecision, bool forceInteger)
    {
        int x = motionVector.X;
        int y = motionVector.Y;
        if (forceInteger)
        {
            x = (x - (x >> 15) + 3) & ~7;
            y = (y - (y >> 15) + 3) & ~7;
            return new Av1MotionVector(y, x);
        }

        if (!allowHighPrecision)
        {
            x = (x - (x >> 15)) & ~1;
            y = (y - (y >> 15)) & ~1;
            return new Av1MotionVector(y, x);
        }

        return motionVector;
    }
}
