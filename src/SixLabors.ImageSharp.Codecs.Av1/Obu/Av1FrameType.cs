// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// The AV1 frame type (specification section 6.8.2, <c>frame_type</c>).
/// </summary>
internal enum Av1FrameType
{
    /// <summary>A key frame: intra-coded and a random-access point.</summary>
    Key = 0,

    /// <summary>An inter-coded frame.</summary>
    Inter = 1,

    /// <summary>An intra-only frame that is not a random-access point.</summary>
    IntraOnly = 2,

    /// <summary>A switch frame.</summary>
    Switch = 3,
}
