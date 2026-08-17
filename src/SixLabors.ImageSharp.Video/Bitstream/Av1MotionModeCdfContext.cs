// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Holds the mutable, adaptive motion-mode CDFs for a single tile (the three-way SIMPLE / OBMC / WARP
/// CDF and the binary OBMC CDF), initialized from the default tables and indexed by block size.
/// </summary>
internal sealed class Av1MotionModeCdfContext
{
    private Av1MotionModeCdfContext()
    {
    }

    /// <summary>Gets the motion-mode CDFs (SIMPLE / OBMC / WARP), indexed by block size.</summary>
    public ushort[]?[] MotionMode { get; private set; } = default!;

    /// <summary>Gets the OBMC flag CDFs (SIMPLE / OBMC), indexed by block size.</summary>
    public ushort[]?[] Obmc { get; private set; } = default!;

    /// <summary>Creates a motion-mode CDF context initialized from the default tables.</summary>
    /// <returns>A fresh, mutable motion-mode CDF context.</returns>
    public static Av1MotionModeCdfContext CreateDefault() => new()
    {
        MotionMode = Clone(Av1DefaultMotionModeCdf.MotionMode),
        Obmc = Clone(Av1DefaultMotionModeCdf.Obmc),
    };

    /// <summary>Creates a deep copy of this context (used to inherit a frame's adapted state).</summary>
    /// <returns>An independent copy.</returns>
    public Av1MotionModeCdfContext Clone() => new()
    {
        MotionMode = Clone(this.MotionMode),
        Obmc = Clone(this.Obmc),
    };

    private static ushort[]?[] Clone(ushort[]?[] group)
    {
        ushort[]?[] result = new ushort[group.Length][];
        for (int i = 0; i < group.Length; i++)
        {
            result[i] = group[i] is { } entry ? (ushort[])entry.Clone() : null;
        }

        return result;
    }
}
