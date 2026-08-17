// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The partition types of the block-partition tree (specification section 6.10.4).
/// </summary>
internal enum Av1Partition
{
    /// <summary>No further split; the block is coded as a unit.</summary>
    None = 0,

    /// <summary>Split into two horizontal halves.</summary>
    Horizontal = 1,

    /// <summary>Split into two vertical halves.</summary>
    Vertical = 2,

    /// <summary>Split into four quadrants.</summary>
    Split = 3,

    /// <summary>T-shaped split with the top split into two.</summary>
    HorizontalA = 4,

    /// <summary>T-shaped split with the bottom split into two.</summary>
    HorizontalB = 5,

    /// <summary>T-shaped split with the left split into two.</summary>
    VerticalA = 6,

    /// <summary>T-shaped split with the right split into two.</summary>
    VerticalB = 7,

    /// <summary>Split into four horizontal strips.</summary>
    Horizontal4 = 8,

    /// <summary>Split into four vertical strips.</summary>
    Vertical4 = 9,
}
