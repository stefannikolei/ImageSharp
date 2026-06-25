// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The block sizes used by the partition tree (specification section 6.10.4), in dav1d's
/// <c>BlockSize</c> ordering. Square sizes drive the partition tree; rectangular sizes are produced as
/// partition leaves.
/// </summary>
internal enum Av1BlockSize
{
    /// <summary>A 128x128 block.</summary>
    Block128x128 = 0,

    /// <summary>A 128x64 block.</summary>
    Block128x64 = 1,

    /// <summary>A 64x128 block.</summary>
    Block64x128 = 2,

    /// <summary>A 64x64 block.</summary>
    Block64x64 = 3,

    /// <summary>A 64x32 block.</summary>
    Block64x32 = 4,

    /// <summary>A 64x16 block.</summary>
    Block64x16 = 5,

    /// <summary>A 32x64 block.</summary>
    Block32x64 = 6,

    /// <summary>A 32x32 block.</summary>
    Block32x32 = 7,

    /// <summary>A 32x16 block.</summary>
    Block32x16 = 8,

    /// <summary>A 32x8 block.</summary>
    Block32x8 = 9,

    /// <summary>A 16x64 block.</summary>
    Block16x64 = 10,

    /// <summary>A 16x32 block.</summary>
    Block16x32 = 11,

    /// <summary>A 16x16 block.</summary>
    Block16x16 = 12,

    /// <summary>A 16x8 block.</summary>
    Block16x8 = 13,

    /// <summary>A 16x4 block.</summary>
    Block16x4 = 14,

    /// <summary>An 8x32 block.</summary>
    Block8x32 = 15,

    /// <summary>An 8x16 block.</summary>
    Block8x16 = 16,

    /// <summary>An 8x8 block.</summary>
    Block8x8 = 17,

    /// <summary>An 8x4 block.</summary>
    Block8x4 = 18,

    /// <summary>A 4x16 block.</summary>
    Block4x16 = 19,

    /// <summary>A 4x8 block.</summary>
    Block4x8 = 20,

    /// <summary>A 4x4 block.</summary>
    Block4x4 = 21,
}
