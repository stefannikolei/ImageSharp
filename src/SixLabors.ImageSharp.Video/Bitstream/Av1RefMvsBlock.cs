// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A neighbour block entry in the 4x4-resolution motion-vector reference grid (the reference decoder's
/// <c>refmvs_block</c>). It pairs the block's motion vectors and reference indices with its block size
/// and the mode flags used to weight candidates. Reference indices are one-based: zero means "no
/// reference" and a positive value is the reference frame index plus one.
/// </summary>
internal readonly struct Av1RefMvsBlock
{
    /// <summary>Initializes a new instance of the <see cref="Av1RefMvsBlock"/> struct.</summary>
    /// <param name="motionVector0">The first motion vector.</param>
    /// <param name="motionVector1">The second motion vector (compound prediction).</param>
    /// <param name="reference0">The first one-based reference index (zero for none).</param>
    /// <param name="reference1">The second one-based reference index (zero or -1 for none).</param>
    /// <param name="blockSize">The neighbour block size.</param>
    /// <param name="isNewMv">Whether the neighbour coded a new motion vector.</param>
    /// <param name="isGlobalMv">Whether the neighbour used the global-motion vector.</param>
    /// <param name="isIntra">Whether the neighbour is an intra block (no inter motion vector).</param>
    public Av1RefMvsBlock(
        Av1MotionVector motionVector0,
        Av1MotionVector motionVector1,
        int reference0,
        int reference1,
        Av1BlockSize blockSize,
        bool isNewMv,
        bool isGlobalMv,
        bool isIntra)
    {
        this.MotionVector0 = motionVector0;
        this.MotionVector1 = motionVector1;
        this.Reference0 = reference0;
        this.Reference1 = reference1;
        this.BlockSize = blockSize;
        this.IsNewMv = isNewMv;
        this.IsGlobalMv = isGlobalMv;
        this.IsIntra = isIntra;
    }

    /// <summary>Gets the first motion vector.</summary>
    public Av1MotionVector MotionVector0 { get; }

    /// <summary>Gets the second motion vector (compound prediction).</summary>
    public Av1MotionVector MotionVector1 { get; }

    /// <summary>Gets the first one-based reference index (zero for none).</summary>
    public int Reference0 { get; }

    /// <summary>Gets the second one-based reference index (zero or -1 for none).</summary>
    public int Reference1 { get; }

    /// <summary>Gets the neighbour block size.</summary>
    public Av1BlockSize BlockSize { get; }

    /// <summary>Gets a value indicating whether the neighbour coded a new motion vector.</summary>
    public bool IsNewMv { get; }

    /// <summary>Gets a value indicating whether the neighbour used the global-motion vector.</summary>
    public bool IsGlobalMv { get; }

    /// <summary>Gets a value indicating whether the neighbour is an intra block.</summary>
    public bool IsIntra { get; }
}
