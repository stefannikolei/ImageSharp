// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The adaptive interpolation-filter CDFs for a single tile, initialized from dav1d 1.4.1's
/// <c>default_cdf.m.filter</c>. The two filter directions (horizontal and vertical, used when the
/// sequence enables dual filtering) each have eight neighbour-derived contexts selecting one of three
/// switchable subpel filters (regular, smooth, sharp).
/// </summary>
internal sealed class Av1InterpolationFilterCdfContext
{
    private static readonly ushort[][][] Default =
    [
        [
            [833, 48, 0, 0],
            [27200, 49, 0, 0],
            [32346, 29830, 0, 0],
            [4524, 160, 0, 0],
            [1562, 815, 0, 0],
            [27906, 647, 0, 0],
            [31998, 31616, 0, 0],
            [11879, 7131, 0, 0],
        ],
        [
            [858, 44, 0, 0],
            [28648, 56, 0, 0],
            [32463, 30521, 0, 0],
            [5365, 132, 0, 0],
            [1746, 759, 0, 0],
            [29805, 675, 0, 0],
            [32167, 31825, 0, 0],
            [17799, 11370, 0, 0],
        ],
    ];

    private Av1InterpolationFilterCdfContext()
    {
    }

    /// <summary>Gets the filter CDFs, indexed by direction [0, 1] then context [0, 7].</summary>
    public ushort[][][] Filter { get; private set; } = default!;

    /// <summary>Creates an interpolation-filter CDF context initialized from the default table.</summary>
    /// <returns>A fresh, mutable interpolation-filter CDF context.</returns>
    public static Av1InterpolationFilterCdfContext CreateDefault()
    {
        ushort[][][] filter = new ushort[Default.Length][][];
        for (int d = 0; d < Default.Length; d++)
        {
            filter[d] = new ushort[Default[d].Length][];
            for (int c = 0; c < Default[d].Length; c++)
            {
                filter[d][c] = (ushort[])Default[d][c].Clone();
            }
        }

        return new Av1InterpolationFilterCdfContext { Filter = filter };
    }

    /// <summary>Creates a deep copy of this context (used to inherit a frame's adapted state).</summary>
    /// <returns>An independent copy.</returns>
    public Av1InterpolationFilterCdfContext Clone() => new() { Filter = Av1CdfTables.Copy(this.Filter) };
}
