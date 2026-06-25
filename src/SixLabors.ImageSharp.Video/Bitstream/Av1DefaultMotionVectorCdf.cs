// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Default (quantizer-independent) motion-vector CDFs ported from dav1d 1.4.1's <c>default_cdf.mv</c>,
/// in the inverse-CDF layout (boundaries, a terminal 0 and an adaptation counter). The reference
/// decoder applies the same component defaults to both the row and column components.
/// </summary>
internal static class Av1DefaultMotionVectorCdf
{
    /// <summary>The motion-vector joint CDF (4 symbols).</summary>
    public static readonly ushort[] Joint = [28672, 21504, 13440, 0, 0];

    /// <summary>The per-component magnitude-class CDF (11 symbols).</summary>
    public static readonly ushort[] Classes = [4096, 1792, 910, 448, 217, 112, 28, 11, 6, 1, 0, 0];

    /// <summary>The per-component class-0 integer bit CDF.</summary>
    public static readonly ushort[] Class0 = [5120, 0, 0];

    /// <summary>The per-component class-N integer bit CDFs, indexed by bit position [0, 9].</summary>
    public static readonly ushort[][] ClassN =
    [
        [15360, 0, 0],
        [14848, 0, 0],
        [13824, 0, 0],
        [12288, 0, 0],
        [10240, 0, 0],
        [8192, 0, 0],
        [4096, 0, 0],
        [2816, 0, 0],
        [2816, 0, 0],
        [2048, 0, 0],
    ];

    /// <summary>The per-component class-0 fractional-pel CDFs, indexed by class-0 integer bit.</summary>
    public static readonly ushort[][] Class0Fp =
    [
        [16384, 8192, 6144, 0, 0],
        [20480, 11520, 8640, 0, 0],
    ];

    /// <summary>The per-component class-N fractional-pel CDF (4 symbols).</summary>
    public static readonly ushort[] ClassNFp = [24576, 15360, 11520, 0, 0];

    /// <summary>The per-component class-0 high-precision bit CDF.</summary>
    public static readonly ushort[] Class0Hp = [12288, 0, 0];

    /// <summary>The per-component class-N high-precision bit CDF.</summary>
    public static readonly ushort[] ClassNHp = [16384, 0, 0];

    /// <summary>The per-component sign bit CDF.</summary>
    public static readonly ushort[] Sign = [16384, 0, 0];
}
