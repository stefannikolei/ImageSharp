// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Holds the mutable, adaptive mode-info CDFs for a single tile (partition, skip and intra-mode
/// syntax), initialized from the quantizer-independent default tables. Each tile keeps its own copy so
/// adaptation does not leak across tiles.
/// </summary>
internal sealed class Av1ModeInfoCdfContext
{
    private Av1ModeInfoCdfContext()
    {
    }

    /// <summary>Gets the skip flag CDFs, indexed by context.</summary>
    public ushort[][] Skip { get; private set; } = default!;

    /// <summary>Gets the partition CDFs, indexed by block level then context.</summary>
    public ushort[][][] Partition { get; private set; } = default!;

    /// <summary>Gets the key-frame luma intra-mode CDFs, indexed by above then left mode context.</summary>
    public ushort[][][] KeyFrameYMode { get; private set; } = default!;

    /// <summary>Gets the chroma intra-mode CDFs, indexed by cfl-allowed then luma mode.</summary>
    public ushort[][][] UvMode { get; private set; } = default!;

    /// <summary>Gets the use_filter_intra CDFs, indexed by square block size.</summary>
    public ushort[][] UseFilterIntra { get; private set; } = default!;

    /// <summary>Gets the filter_intra mode CDF.</summary>
    public ushort[] FilterIntraMode { get; private set; } = default!;

    /// <summary>Gets the tx-depth CDFs, indexed by (max transform size - 1) then context.</summary>
    public ushort[][][] TransformDepth { get; private set; } = default!;

    /// <summary>Gets the intra txtp_intra1 CDFs, indexed by tx-size-min then luma mode.</summary>
    public ushort[][][] TransformTypeIntra1 { get; private set; } = default!;

    /// <summary>Gets the intra txtp_intra2 CDFs, indexed by tx-size-min then luma mode.</summary>
    public ushort[][][] TransformTypeIntra2 { get; private set; } = default!;

    /// <summary>
    /// Creates a mode-info CDF context initialized from the default tables.
    /// </summary>
    /// <returns>A fresh, mutable mode-info CDF context.</returns>
    public static Av1ModeInfoCdfContext CreateDefault() => new()
    {
        Skip = Clone(Av1DefaultModeInfoCdf.Skip),
        Partition = Clone(Av1DefaultModeInfoCdf.Partition),
        KeyFrameYMode = Clone(Av1DefaultModeInfoCdf.KeyFrameYMode),
        UvMode = Clone(Av1DefaultModeInfoCdf.UvMode),
        UseFilterIntra = Clone(Av1DefaultModeInfoCdf.UseFilterIntra),
        FilterIntraMode = (ushort[])Av1DefaultModeInfoCdf.FilterIntraMode.Clone(),
        TransformDepth = Clone(Av1DefaultModeInfoCdf.TransformDepth),
        TransformTypeIntra1 = Clone(Av1DefaultModeInfoCdf.TransformTypeIntra1),
        TransformTypeIntra2 = Clone(Av1DefaultModeInfoCdf.TransformTypeIntra2),
    };

    private static ushort[][] Clone(ushort[][] group)
    {
        ushort[][] result = new ushort[group.Length][];
        for (int i = 0; i < group.Length; i++)
        {
            result[i] = (ushort[])group[i].Clone();
        }

        return result;
    }

    private static ushort[][][] Clone(ushort[][][] group)
    {
        ushort[][][] result = new ushort[group.Length][][];
        for (int i = 0; i < group.Length; i++)
        {
            result[i] = Clone(group[i]);
        }

        return result;
    }
}
