// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A motion vector in eighth-pel units, matching the reference decoder's <c>mv</c> type
/// (specification section 7.10, motion-vector prediction). The row component is <see cref="Y"/>
/// and the column component is <see cref="X"/>.
/// </summary>
internal readonly struct Av1MotionVector
{
    /// <summary>Initializes a new instance of the <see cref="Av1MotionVector"/> struct.</summary>
    /// <param name="y">The row (vertical) component in eighth-pel units.</param>
    /// <param name="x">The column (horizontal) component in eighth-pel units.</param>
    public Av1MotionVector(int y, int x)
    {
        this.Y = y;
        this.X = x;
    }

    /// <summary>Gets the row (vertical) component in eighth-pel units.</summary>
    public int Y { get; }

    /// <summary>Gets the column (horizontal) component in eighth-pel units.</summary>
    public int X { get; }
}
