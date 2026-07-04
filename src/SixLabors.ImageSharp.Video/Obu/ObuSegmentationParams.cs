// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// The frame's segmentation parameters (specification section 5.9.14, <c>segmentation_params</c>): the
/// update flags and the per-segment feature data. A frame whose header does not update the data inherits
/// the feature table from its primary reference.
/// </summary>
internal sealed record ObuSegmentationParams
{
    /// <summary>The number of segments.</summary>
    public const int SegmentCount = 8;

    /// <summary>Gets a value indicating whether segmentation is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets a value indicating whether the segment map is coded in this frame.</summary>
    public bool UpdateMap { get; init; }

    /// <summary>Gets a value indicating whether the segment map may be predicted temporally.</summary>
    public bool TemporalUpdate { get; init; }

    /// <summary>Gets a value indicating whether the feature data is coded in this frame.</summary>
    public bool UpdateData { get; init; }

    /// <summary>Gets a value indicating whether segment ids are coded before the skip flag (set when
    /// any segment uses the reference, skip or global-mv feature).</summary>
    public bool PreSkip { get; init; }

    /// <summary>Gets the highest segment id with any active feature.</summary>
    public int LastActiveSegmentId { get; init; } = -1;

    /// <summary>Gets the per-segment quantizer delta.</summary>
    public int[] DeltaQ { get; init; } = new int[SegmentCount];

    /// <summary>Gets the per-segment luma vertical loop-filter delta.</summary>
    public int[] DeltaLfYVertical { get; init; } = new int[SegmentCount];

    /// <summary>Gets the per-segment luma horizontal loop-filter delta.</summary>
    public int[] DeltaLfYHorizontal { get; init; } = new int[SegmentCount];

    /// <summary>Gets the per-segment chroma U loop-filter delta.</summary>
    public int[] DeltaLfU { get; init; } = new int[SegmentCount];

    /// <summary>Gets the per-segment chroma V loop-filter delta.</summary>
    public int[] DeltaLfV { get; init; } = new int[SegmentCount];

    /// <summary>Gets the per-segment forced reference frame (-1 for none).</summary>
    public int[] Reference { get; init; } = CreateNoReference();

    /// <summary>Gets the per-segment forced-skip flags.</summary>
    public bool[] Skip { get; init; } = new bool[SegmentCount];

    /// <summary>Gets the per-segment forced-global-mv flags.</summary>
    public bool[] GlobalMv { get; init; } = new bool[SegmentCount];

    /// <summary>Gets the disabled-segmentation default.</summary>
    public static ObuSegmentationParams Disabled { get; } = new();

    private static int[] CreateNoReference()
    {
        int[] reference = new int[SegmentCount];
        Array.Fill(reference, -1);
        return reference;
    }
}
