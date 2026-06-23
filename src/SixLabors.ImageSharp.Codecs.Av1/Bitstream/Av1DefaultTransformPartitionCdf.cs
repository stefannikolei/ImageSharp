// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Default (quantizer-independent) inter transform-partition CDFs ported from dav1d 1.4.1's
/// <c>default_cdf.m.txpart</c>, in the inverse-CDF layout. The variable-transform tree reads a split
/// flag at each node, indexed by a category derived from the node's maximum size and recursion depth
/// and a context from the neighbouring transform sizes.
/// </summary>
internal static class Av1DefaultTransformPartitionCdf
{
    /// <summary>The transform-partition split CDFs, indexed by category [0, 6] then context [0, 2].</summary>
    public static readonly ushort[][][] Split =
    [
        [[4187, 0, 0], [8922, 0, 0], [11921, 0, 0]],
        [[8453, 0, 0], [14572, 0, 0], [20635, 0, 0]],
        [[13977, 0, 0], [21881, 0, 0], [21763, 0, 0]],
        [[5589, 0, 0], [12764, 0, 0], [21487, 0, 0]],
        [[6219, 0, 0], [13460, 0, 0], [18544, 0, 0]],
        [[4753, 0, 0], [11222, 0, 0], [18368, 0, 0]],
        [[4603, 0, 0], [10367, 0, 0], [16680, 0, 0]],
    ];
}
