// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The frame-level parameters that govern single-reference inter mode-info decoding: motion-vector
/// precision, interpolation-filter mode, global motion and frame geometry. These are constant for the
/// duration of a frame and are passed to <see cref="Av1InterModeInfoDecoder"/> for every block.
/// </summary>
internal readonly struct Av1InterModeInfoOptions
{
    /// <summary>Initializes a new instance of the <see cref="Av1InterModeInfoOptions"/> struct.</summary>
    /// <param name="bounds">The tile bounds in 4x4 units.</param>
    /// <param name="imageWidth4">The frame width in 4x4 units.</param>
    /// <param name="imageHeight4">The frame height in 4x4 units.</param>
    /// <param name="allowHighPrecisionMv">Whether eighth-pel motion vectors are allowed.</param>
    /// <param name="forceIntegerMv">Whether motion vectors are forced to whole pels.</param>
    /// <param name="filterSwitchable">Whether the interpolation filter is coded per block.</param>
    /// <param name="dualFilter">Whether horizontal/vertical filters are coded independently.</param>
    /// <param name="fixedFilter">The fixed interpolation filter (used when not switchable).</param>
    /// <param name="globalMv">The global-motion (predictor) vector.</param>
    /// <param name="globalMvSubstitution">Whether neighbours substitute the global-motion vector.</param>
    /// <param name="globalMvIsTranslation">Whether the global-motion model is translational.</param>
    /// <param name="signBias">The per-reference sign bias, indexed by zero-based reference.</param>
    /// <param name="temporal">The temporal motion-vector prediction state, or <see langword="null"/>.</param>
    public Av1InterModeInfoOptions(
        Av1TileBounds bounds,
        int imageWidth4,
        int imageHeight4,
        bool allowHighPrecisionMv,
        bool forceIntegerMv,
        bool filterSwitchable,
        bool dualFilter,
        int fixedFilter,
        Av1MotionVector globalMv,
        bool globalMvSubstitution,
        bool globalMvIsTranslation,
        int[] signBias,
        Av1TemporalMvContext? temporal = null)
    {
        this.Bounds = bounds;
        this.ImageWidth4 = imageWidth4;
        this.ImageHeight4 = imageHeight4;
        this.AllowHighPrecisionMv = allowHighPrecisionMv;
        this.ForceIntegerMv = forceIntegerMv;
        this.FilterSwitchable = filterSwitchable;
        this.DualFilter = dualFilter;
        this.FixedFilter = fixedFilter;
        this.GlobalMv = globalMv;
        this.GlobalMvSubstitution = globalMvSubstitution;
        this.GlobalMvIsTranslation = globalMvIsTranslation;
        this.SignBias = signBias;
        this.Temporal = temporal;
    }

    /// <summary>Gets the tile bounds in 4x4 units.</summary>
    public Av1TileBounds Bounds { get; }

    /// <summary>Gets the frame width in 4x4 units.</summary>
    public int ImageWidth4 { get; }

    /// <summary>Gets the frame height in 4x4 units.</summary>
    public int ImageHeight4 { get; }

    /// <summary>Gets a value indicating whether eighth-pel motion vectors are allowed.</summary>
    public bool AllowHighPrecisionMv { get; }

    /// <summary>Gets a value indicating whether motion vectors are forced to whole pels.</summary>
    public bool ForceIntegerMv { get; }

    /// <summary>Gets a value indicating whether the interpolation filter is coded per block.</summary>
    public bool FilterSwitchable { get; }

    /// <summary>Gets a value indicating whether horizontal/vertical filters are coded independently.</summary>
    public bool DualFilter { get; }

    /// <summary>Gets the fixed interpolation filter (used when not switchable).</summary>
    public int FixedFilter { get; }

    /// <summary>Gets the global-motion (predictor) vector.</summary>
    public Av1MotionVector GlobalMv { get; }

    /// <summary>Gets a value indicating whether neighbours substitute the global-motion vector.</summary>
    public bool GlobalMvSubstitution { get; }

    /// <summary>Gets a value indicating whether the global-motion model is translational.</summary>
    public bool GlobalMvIsTranslation { get; }

    /// <summary>Gets the per-reference sign bias, indexed by zero-based reference.</summary>
    public int[] SignBias { get; }

    /// <summary>Gets the temporal motion-vector prediction state, or <see langword="null"/>.</summary>
    public Av1TemporalMvContext? Temporal { get; }
}
