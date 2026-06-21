// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Default (quantizer-independent) inter-mode CDFs ported from dav1d 1.4.1's <c>default_cdf.m</c>,
/// in the inverse-CDF layout (boundaries, a terminal 0 and an adaptation counter). These drive the
/// single-reference inter block decode: the is-inter flag, the inter prediction mode (NEW / GLOBAL /
/// NEAREST / NEAR) and the dynamic reference list, plus the compound and reference-frame selection.
/// </summary>
internal static class Av1DefaultInterModeCdf
{
    /// <summary>The is-inter flag CDFs, indexed by context [0, 3].</summary>
    public static readonly ushort[][] IsInter =
    [
        [31962, 0, 0],
        [16106, 0, 0],
        [12582, 0, 0],
        [6230, 0, 0],
    ];

    /// <summary>The new-mv flag CDFs, indexed by context [0, 5].</summary>
    public static readonly ushort[][] NewMv =
    [
        [8733, 0, 0],
        [16138, 0, 0],
        [17429, 0, 0],
        [24382, 0, 0],
        [20546, 0, 0],
        [28092, 0, 0],
    ];

    /// <summary>The global-mv flag CDFs, indexed by context [0, 1].</summary>
    public static readonly ushort[][] GlobalMv =
    [
        [30593, 0, 0],
        [31714, 0, 0],
    ];

    /// <summary>The ref-mv flag CDFs, indexed by context [0, 5].</summary>
    public static readonly ushort[][] RefMv =
    [
        [8794, 0, 0],
        [8580, 0, 0],
        [14920, 0, 0],
        [4146, 0, 0],
        [8456, 0, 0],
        [12845, 0, 0],
    ];

    /// <summary>The dynamic-reference-list bit CDFs, indexed by context [0, 2].</summary>
    public static readonly ushort[][] DrlBit =
    [
        [19664, 0, 0],
        [8208, 0, 0],
        [13823, 0, 0],
    ];

    /// <summary>The compound (is-compound) flag CDFs, indexed by context [0, 4].</summary>
    public static readonly ushort[][] Compound =
    [
        [5940, 0, 0],
        [8733, 0, 0],
        [20737, 0, 0],
        [22128, 0, 0],
        [29867, 0, 0],
    ];

    /// <summary>The single-reference selection CDFs, indexed by bit position [0, 5] then context [0, 2].</summary>
    public static readonly ushort[][][] SingleReference =
    [
        [[27871, 0, 0], [15795, 0, 0], [3024, 0, 0]],
        [[31213, 0, 0], [16017, 0, 0], [2489, 0, 0]],
        [[28532, 0, 0], [13121, 0, 0], [1574, 0, 0]],
        [[24118, 0, 0], [7995, 0, 0], [873, 0, 0]],
        [[31864, 0, 0], [21754, 0, 0], [5893, 0, 0]],
        [[31324, 0, 0], [17681, 0, 0], [2464, 0, 0]],
    ];
}
