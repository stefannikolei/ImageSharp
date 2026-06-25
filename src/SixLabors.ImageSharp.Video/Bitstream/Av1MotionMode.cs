// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The inter motion model applied to a block (specification section 6.10.24), matching the reference
/// decoder's <c>MotionMode</c> ordering.
/// </summary>
internal enum Av1MotionMode
{
    /// <summary>Simple translational motion compensation.</summary>
    Translation = 0,

    /// <summary>Overlapped block motion compensation.</summary>
    Obmc = 1,

    /// <summary>Local warped motion compensation.</summary>
    Warp = 2,
}
