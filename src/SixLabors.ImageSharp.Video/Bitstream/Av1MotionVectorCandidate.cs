// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A single entry in the dynamic reference (motion-vector candidate) list produced by the MV
/// prediction process (specification section 7.10), pairing a candidate motion vector with the
/// accumulated weight used to order the list and derive dynamic-reference-list contexts.
/// </summary>
internal readonly struct Av1MotionVectorCandidate
{
    /// <summary>Initializes a new instance of the <see cref="Av1MotionVectorCandidate"/> struct.</summary>
    /// <param name="motionVector">The candidate motion vector.</param>
    /// <param name="weight">The accumulated candidate weight.</param>
    public Av1MotionVectorCandidate(Av1MotionVector motionVector, int weight)
    {
        this.MotionVector = motionVector;
        this.Weight = weight;
    }

    /// <summary>Gets the candidate motion vector.</summary>
    public Av1MotionVector MotionVector { get; }

    /// <summary>Gets the accumulated candidate weight.</summary>
    public int Weight { get; }
}
