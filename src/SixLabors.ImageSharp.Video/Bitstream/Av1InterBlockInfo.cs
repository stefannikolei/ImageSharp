// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The decoded mode information of a single single-reference inter block: the reference frame, the
/// prediction mode and dynamic-reference-list index, the resolved motion vector, the interpolation
/// filters and the motion mode.
/// </summary>
internal readonly struct Av1InterBlockInfo
{
    /// <summary>Initializes a new instance of the <see cref="Av1InterBlockInfo"/> struct.</summary>
    /// <param name="reference">The zero-based reference frame index.</param>
    /// <param name="mode">The inter prediction mode.</param>
    /// <param name="dynamicReferenceIndex">The dynamic-reference-list index.</param>
    /// <param name="motionVector">The resolved motion vector.</param>
    /// <param name="filter0">The horizontal interpolation filter.</param>
    /// <param name="filter1">The vertical interpolation filter.</param>
    /// <param name="motionMode">The motion mode.</param>
    /// <param name="warpMatrix">The derived local warp matrix (WARP motion mode with a successful
    /// derivation), or <see langword="null"/>.</param>
    /// <param name="warpShear">The derived local warp shear parameters, or <see langword="null"/>.</param>
    public Av1InterBlockInfo(
        int reference,
        Av1InterPredictionMode mode,
        int dynamicReferenceIndex,
        Av1MotionVector motionVector,
        int filter0,
        int filter1,
        Av1MotionMode motionMode,
        int[]? warpMatrix = null,
        short[]? warpShear = null)
    {
        this.Reference = reference;
        this.Mode = mode;
        this.DynamicReferenceIndex = dynamicReferenceIndex;
        this.MotionVector = motionVector;
        this.Filter0 = filter0;
        this.Filter1 = filter1;
        this.MotionMode = motionMode;
        this.WarpMatrix = warpMatrix;
        this.WarpShear = warpShear;
    }

    /// <summary>Gets the zero-based reference frame index.</summary>
    public int Reference { get; }

    /// <summary>Gets the inter prediction mode.</summary>
    public Av1InterPredictionMode Mode { get; }

    /// <summary>Gets the dynamic-reference-list index.</summary>
    public int DynamicReferenceIndex { get; }

    /// <summary>Gets the resolved motion vector.</summary>
    public Av1MotionVector MotionVector { get; }

    /// <summary>Gets the horizontal interpolation filter.</summary>
    public int Filter0 { get; }

    /// <summary>Gets the vertical interpolation filter.</summary>
    public int Filter1 { get; }

    /// <summary>Gets the motion mode.</summary>
    public Av1MotionMode MotionMode { get; }

    /// <summary>Gets the derived local warp matrix, or <see langword="null"/> when the block is not
    /// warped (or the derivation degenerated and the block falls back to translation).</summary>
    public int[]? WarpMatrix { get; }

    /// <summary>Gets the derived local warp shear parameters, or <see langword="null"/>.</summary>
    public short[]? WarpShear { get; }
}
