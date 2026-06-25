// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Holds the mutable, adaptive inter-mode CDFs for a single tile (the is-inter, inter-mode,
/// dynamic-reference-list, compound and reference-selection syntax), initialized from the default
/// tables. Each tile keeps its own copy so adaptation does not leak across tiles.
/// </summary>
internal sealed class Av1InterModeCdfContext
{
    private Av1InterModeCdfContext()
    {
    }

    /// <summary>Gets the is-inter flag CDFs, indexed by context.</summary>
    public ushort[][] IsInter { get; private set; } = default!;

    /// <summary>Gets the skip-mode flag CDFs, indexed by context.</summary>
    public ushort[][] SkipMode { get; private set; } = default!;

    /// <summary>Gets the new-mv flag CDFs, indexed by context.</summary>
    public ushort[][] NewMv { get; private set; } = default!;

    /// <summary>Gets the global-mv flag CDFs, indexed by context.</summary>
    public ushort[][] GlobalMv { get; private set; } = default!;

    /// <summary>Gets the ref-mv flag CDFs, indexed by context.</summary>
    public ushort[][] RefMv { get; private set; } = default!;

    /// <summary>Gets the dynamic-reference-list bit CDFs, indexed by context.</summary>
    public ushort[][] DrlBit { get; private set; } = default!;

    /// <summary>Gets the compound flag CDFs, indexed by context.</summary>
    public ushort[][] Compound { get; private set; } = default!;

    /// <summary>Gets the single-reference selection CDFs, indexed by bit position then context.</summary>
    public ushort[][][] SingleReference { get; private set; } = default!;

    /// <summary>Creates an inter-mode CDF context initialized from the default tables.</summary>
    /// <returns>A fresh, mutable inter-mode CDF context.</returns>
    public static Av1InterModeCdfContext CreateDefault() => new()
    {
        IsInter = Clone(Av1DefaultInterModeCdf.IsInter),
        SkipMode = Clone(Av1DefaultInterModeCdf.SkipMode),
        NewMv = Clone(Av1DefaultInterModeCdf.NewMv),
        GlobalMv = Clone(Av1DefaultInterModeCdf.GlobalMv),
        RefMv = Clone(Av1DefaultInterModeCdf.RefMv),
        DrlBit = Clone(Av1DefaultInterModeCdf.DrlBit),
        Compound = Clone(Av1DefaultInterModeCdf.Compound),
        SingleReference = Clone(Av1DefaultInterModeCdf.SingleReference),
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
