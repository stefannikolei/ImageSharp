// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A decoded reference frame retained for inter prediction: the reconstructed planes plus the order hint
/// used to order references, and the frame-end context (CDF state and header inheritance values) a later
/// frame with <c>primary_ref_frame</c> pointing at this slot loads instead of the defaults.
/// </summary>
internal sealed class Av1ReferenceFrame
{
    /// <summary>Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class.</summary>
    /// <param name="orderHint">The frame's order hint.</param>
    /// <param name="luma">The reconstructed luma plane.</param>
    /// <param name="chromaU">The reconstructed U plane (or <see langword="null"/> for monochrome).</param>
    /// <param name="chromaV">The reconstructed V plane (or <see langword="null"/> for monochrome).</param>
    /// <param name="cdfs">The frame-end CDF state (or <see langword="null"/> when unavailable, in which
    /// case a primary reference to this slot falls back to the defaults).</param>
    /// <param name="headerState">The frame-end header inheritance state (or <see langword="null"/> for
    /// the specification defaults).</param>
    /// <param name="temporalMvs">The frame's saved temporal motion field (or <see langword="null"/> for
    /// intra frames, which save none).</param>
    /// <param name="referenceOrderHints">The order hints of the frame's own seven references, by name
    /// (all zero for intra frames).</param>
    public Av1ReferenceFrame(int orderHint, Av1Plane luma, Av1Plane? chromaU, Av1Plane? chromaV, Av1FrameCdfSet? cdfs = null, ObuPrimaryReferenceState? headerState = null, Av1TemporalMvs? temporalMvs = null, int[]? referenceOrderHints = null, bool isKeyFrame = false, byte[]? segmentMap = null, int segmentMapColumns = 0, int segmentMapRows = 0)
    {
        this.SegmentMap = segmentMap;
        this.SegmentMapColumns = segmentMapColumns;
        this.SegmentMapRows = segmentMapRows;
        this.IsKeyFrame = isKeyFrame;
        this.OrderHint = orderHint;
        this.Luma = luma;
        this.ChromaU = chromaU;
        this.ChromaV = chromaV;
        this.Cdfs = cdfs;
        this.HeaderState = headerState;
        this.TemporalMvs = temporalMvs;
        this.ReferenceOrderHints = referenceOrderHints ?? new int[7];
    }

    /// <summary>Gets a value indicating whether the frame is a key frame (re-showing it would
    /// require a decoder-state reload).</summary>
    public bool IsKeyFrame { get; }

    /// <summary>Gets the frame's order hint.</summary>
    public int OrderHint { get; }

    /// <summary>Gets the reconstructed luma plane.</summary>
    public Av1Plane Luma { get; }

    /// <summary>Gets the reconstructed U plane, or <see langword="null"/> for monochrome.</summary>
    public Av1Plane? ChromaU { get; }

    /// <summary>Gets the reconstructed V plane, or <see langword="null"/> for monochrome.</summary>
    public Av1Plane? ChromaV { get; }

    /// <summary>Gets the frame-end CDF state, or <see langword="null"/> when unavailable.</summary>
    public Av1FrameCdfSet? Cdfs { get; }

    /// <summary>Gets the frame-end header inheritance state, or <see langword="null"/> for the defaults.</summary>
    public ObuPrimaryReferenceState? HeaderState { get; }

    /// <summary>Gets the frame's saved temporal motion field, or <see langword="null"/> for intra frames.</summary>
    public Av1TemporalMvs? TemporalMvs { get; }

    /// <summary>Gets the order hints of the frame's own seven references, by name.</summary>
    public int[] ReferenceOrderHints { get; }

    /// <summary>Gets the frame's final segment map (4x4 granularity), or <see langword="null"/>
    /// when the frame coded no segmentation.</summary>
    public byte[]? SegmentMap { get; }

    /// <summary>Gets the segment map's column count in 4x4 units.</summary>
    public int SegmentMapColumns { get; }

    /// <summary>Gets the segment map's row count in 4x4 units.</summary>
    public int SegmentMapRows { get; }
}
