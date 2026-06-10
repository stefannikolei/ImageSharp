// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// The type of an Open Bitstream Unit (OBU) as defined in the AV1 specification, section 6.2.2.
/// </summary>
internal enum ObuType
{
    /// <summary>Reserved value 0.</summary>
    Reserved0 = 0,

    /// <summary>Sequence header OBU.</summary>
    SequenceHeader = 1,

    /// <summary>Temporal delimiter OBU.</summary>
    TemporalDelimiter = 2,

    /// <summary>Frame header OBU.</summary>
    FrameHeader = 3,

    /// <summary>Tile group OBU.</summary>
    TileGroup = 4,

    /// <summary>Metadata OBU.</summary>
    Metadata = 5,

    /// <summary>Frame OBU (combined frame header and tile group).</summary>
    Frame = 6,

    /// <summary>Redundant frame header OBU.</summary>
    RedundantFrameHeader = 7,

    /// <summary>Tile list OBU.</summary>
    TileList = 8,

    /// <summary>Padding OBU.</summary>
    Padding = 15,
}
