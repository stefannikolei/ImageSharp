// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The square block sizes used by the partition tree. Only square sizes are represented because the
/// current intra decoder supports the <c>PARTITION_NONE</c> and <c>PARTITION_SPLIT</c> paths.
/// </summary>
internal enum Av1BlockSize
{
    /// <summary>A 4x4 block.</summary>
    Block4x4 = 0,

    /// <summary>An 8x8 block.</summary>
    Block8x8 = 1,

    /// <summary>A 16x16 block.</summary>
    Block16x16 = 2,

    /// <summary>A 32x32 block.</summary>
    Block32x32 = 3,

    /// <summary>A 64x64 block.</summary>
    Block64x64 = 4,

    /// <summary>A 128x128 block.</summary>
    Block128x128 = 5,
}
