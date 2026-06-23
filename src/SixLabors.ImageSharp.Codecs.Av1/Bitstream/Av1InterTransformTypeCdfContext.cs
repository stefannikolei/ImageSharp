// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Holds the mutable, adaptive inter transform-type CDFs for a single tile, initialized from dav1d
/// 1.4.1's <c>default_cdf.m.txtp_inter*</c>, together with the <c>tx_types_per_set</c> mapping from the
/// decoded index to the transform type. There are three coded sets: a 16-symbol set (small transforms),
/// a 12-symbol set (16x16 minimum) and a binary set (reduced / 32x32 maximum).
/// </summary>
internal sealed class Av1InterTransformTypeCdfContext
{
    private static readonly ushort[][] DefaultSet1 =
    [
        [28310, 27208, 25073, 23059, 19438, 17979, 15231, 12502, 11264, 9920, 8834, 7294, 5041, 3853, 2137, 0, 0],
        [31123, 30195, 27990, 27057, 24961, 24146, 22246, 17411, 15094, 12360, 10251, 7758, 5652, 3912, 2019, 0, 0],
    ];

    private static readonly ushort[] DefaultSet2 =
        [31998, 30347, 27543, 19861, 16949, 13841, 11207, 8679, 6173, 4242, 2239, 0, 0];

    private static readonly ushort[][] DefaultSet3 =
    [
        [16384, 0, 0],
        [28601, 0, 0],
        [30770, 0, 0],
        [32020, 0, 0],
    ];

    // dav1d_tx_types_per_set, "Inter1" subset (16 entries) mapped to Av1TransformType values.
    private static readonly int[] Set1Types =
        [9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 4, 5, 3, 6, 7, 8];

    // dav1d_tx_types_per_set, "Inter2" subset (12 entries).
    private static readonly int[] Set2Types =
        [9, 10, 11, 0, 1, 2, 4, 5, 3, 6, 7, 8];

    private Av1InterTransformTypeCdfContext()
    {
    }

    /// <summary>Gets the 16-symbol inter transform-type CDFs, indexed by minimum transform category.</summary>
    public ushort[][] Set1 { get; private set; } = default!;

    /// <summary>Gets the 12-symbol inter transform-type CDF (16x16 minimum).</summary>
    public ushort[] Set2 { get; private set; } = default!;

    /// <summary>Gets the binary inter transform-type CDFs, indexed by minimum transform category.</summary>
    public ushort[][] Set3 { get; private set; } = default!;

    /// <summary>Maps a decoded index from the 16-symbol set to its transform type.</summary>
    /// <param name="index">The decoded symbol index.</param>
    /// <returns>The transform type.</returns>
    public static Av1TransformType FromSet1(int index) => (Av1TransformType)Set1Types[index];

    /// <summary>Maps a decoded index from the 12-symbol set to its transform type.</summary>
    /// <param name="index">The decoded symbol index.</param>
    /// <returns>The transform type.</returns>
    public static Av1TransformType FromSet2(int index) => (Av1TransformType)Set2Types[index];

    /// <summary>Creates an inter transform-type CDF context initialized from the default tables.</summary>
    /// <returns>A fresh, mutable inter transform-type CDF context.</returns>
    public static Av1InterTransformTypeCdfContext CreateDefault() => new()
    {
        Set1 = [(ushort[])DefaultSet1[0].Clone(), (ushort[])DefaultSet1[1].Clone()],
        Set2 = (ushort[])DefaultSet2.Clone(),
        Set3 = [(ushort[])DefaultSet3[0].Clone(), (ushort[])DefaultSet3[1].Clone(), (ushort[])DefaultSet3[2].Clone(), (ushort[])DefaultSet3[3].Clone()],
    };
}
