// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Default (quantizer-independent) motion-mode CDFs ported from dav1d 1.4.1's <c>default_cdf.m</c>,
/// in the inverse-CDF layout. Both tables are indexed by <see cref="Av1BlockSize"/>; entries are only
/// present for block sizes whose smaller dimension is at least eight pixels (the sizes for which a
/// motion mode is coded), with the remaining entries left null.
/// </summary>
internal static class Av1DefaultMotionModeCdf
{
    /// <summary>The motion-mode CDFs (SIMPLE / OBMC / WARP), used when warped motion is allowed.</summary>
    public static readonly ushort[]?[] MotionMode =
    [
        [261, 210, 0, 0],
        [1890, 1433, 0, 0],
        [3870, 2371, 0, 0],
        [3252, 2067, 0, 0],
        [11089, 5938, 0, 0],
        [3026, 1565, 0, 0],
        [12408, 4706, 0, 0],
        [6508, 3652, 0, 0],
        [21162, 8460, 0, 0],
        [6337, 1994, 0, 0],
        [3795, 1174, 0, 0],
        [27645, 9162, 0, 0],
        [13349, 5958, 0, 0],
        [27377, 7240, 0, 0],
        null,
        [3969, 1378, 0, 0],
        [28030, 8003, 0, 0],
        [25117, 8008, 0, 0],
        null,
        null,
        null,
        null,
    ];

    /// <summary>The OBMC flag CDFs (SIMPLE / OBMC), used when warped motion is not allowed.</summary>
    public static readonly ushort[]?[] Obmc =
    [
        [130, 0, 0],
        [1208, 0, 0],
        [1754, 0, 0],
        [2640, 0, 0],
        [10685, 0, 0],
        [5889, 0, 0],
        [9945, 0, 0],
        [6951, 0, 0],
        [17626, 0, 0],
        [11867, 0, 0],
        [8760, 0, 0],
        [18345, 0, 0],
        [15336, 0, 0],
        [23467, 0, 0],
        null,
        [9104, 0, 0],
        [23397, 0, 0],
        [22331, 0, 0],
        null,
        null,
        null,
        null,
    ];
}
