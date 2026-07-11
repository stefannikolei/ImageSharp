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
    /// <param name="globalMotion">The seven per-reference global-motion models.</param>
    /// <param name="signBias">The per-reference sign bias, indexed by zero-based reference.</param>
    /// <param name="allowWarpedMotion">Whether local warped motion is enabled for the frame.</param>
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
        Obu.Av1WarpedMotionParams[] globalMotion,
        int[] signBias,
        bool allowWarpedMotion = false,
        Av1TemporalMvContext? temporal = null,
        bool enableMaskedCompound = false,
        bool enableInterIntra = false,
        bool[]? referenceIsScaled = null,
        bool enableJntComp = false,
        int[]? referencePocDistance = null)
    {
        this.AllowWarpedMotion = allowWarpedMotion;
        this.EnableMaskedCompound = enableMaskedCompound;
        this.EnableInterIntra = enableInterIntra;
        this.Bounds = bounds;
        this.ImageWidth4 = imageWidth4;
        this.ImageHeight4 = imageHeight4;
        this.AllowHighPrecisionMv = allowHighPrecisionMv;
        this.ForceIntegerMv = forceIntegerMv;
        this.FilterSwitchable = filterSwitchable;
        this.DualFilter = dualFilter;
        this.FixedFilter = fixedFilter;
        this.GlobalMotion = globalMotion;
        this.SignBias = signBias;
        this.Temporal = temporal;
        this.ReferenceIsScaled = referenceIsScaled ?? new bool[7];
        this.EnableJntComp = enableJntComp;
        this.ReferencePocDistance = referencePocDistance ?? new int[7];
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

    /// <summary>Gets a value indicating whether local warped motion is enabled for the frame.</summary>
    public bool AllowWarpedMotion { get; }

    /// <summary>Gets a value indicating whether masked compound prediction is enabled.</summary>
    public bool EnableMaskedCompound { get; }

    /// <summary>Gets a value indicating whether inter-intra prediction is enabled.</summary>
    public bool EnableInterIntra { get; }

    /// <summary>Gets the seven per-reference global-motion models.</summary>
    public Obu.Av1WarpedMotionParams[] GlobalMotion { get; }

    /// <summary>Gets the per-reference sign bias, indexed by zero-based reference.</summary>
    public int[] SignBias { get; }

    /// <summary>Gets the temporal motion-vector prediction state, or <see langword="null"/>.</summary>
    public Av1TemporalMvContext? Temporal { get; }

    /// <summary>Gets, per zero-based reference, whether the reference is scaled (which disallows
    /// the WARP motion mode for blocks predicting from it).</summary>
    public bool[] ReferenceIsScaled { get; }

    /// <summary>Gets a value indicating whether distance-weighted compound prediction is enabled.</summary>
    public bool EnableJntComp { get; }

    /// <summary>Gets, per zero-based reference, the absolute order-hint distance to the current
    /// frame (the distance-weighted compound context input).</summary>
    public int[] ReferencePocDistance { get; }

    /// <summary>Returns a copy of these options with the tile bounds replaced (per-tile decoding).</summary>
    /// <param name="bounds">The new tile bounds.</param>
    /// <returns>The re-bounded options.</returns>
    public Av1InterModeInfoOptions WithBounds(Av1TileBounds bounds) => new(
        bounds,
        this.ImageWidth4,
        this.ImageHeight4,
        this.AllowHighPrecisionMv,
        this.ForceIntegerMv,
        this.FilterSwitchable,
        this.DualFilter,
        this.FixedFilter,
        this.GlobalMotion,
        this.SignBias,
        this.AllowWarpedMotion,
        this.Temporal,
        this.EnableMaskedCompound,
        this.EnableInterIntra,
        this.ReferenceIsScaled,
        this.EnableJntComp,
        this.ReferencePocDistance);
}
