// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// The transform block sizes defined by AV1 (specification section 6.10.x). Values 0-4 are the
/// square sizes; values 5-18 are the rectangular sizes. The numeric order matches the AV1 reference.
/// </summary>
internal enum Av1TransformSize
{
    /// <summary>4x4 transform.</summary>
    Size4x4,

    /// <summary>8x8 transform.</summary>
    Size8x8,

    /// <summary>16x16 transform.</summary>
    Size16x16,

    /// <summary>32x32 transform.</summary>
    Size32x32,

    /// <summary>64x64 transform.</summary>
    Size64x64,

    /// <summary>4x8 transform.</summary>
    Size4x8,

    /// <summary>8x4 transform.</summary>
    Size8x4,

    /// <summary>8x16 transform.</summary>
    Size8x16,

    /// <summary>16x8 transform.</summary>
    Size16x8,

    /// <summary>16x32 transform.</summary>
    Size16x32,

    /// <summary>32x16 transform.</summary>
    Size32x16,

    /// <summary>32x64 transform.</summary>
    Size32x64,

    /// <summary>64x32 transform.</summary>
    Size64x32,

    /// <summary>4x16 transform.</summary>
    Size4x16,

    /// <summary>16x4 transform.</summary>
    Size16x4,

    /// <summary>8x32 transform.</summary>
    Size8x32,

    /// <summary>32x8 transform.</summary>
    Size32x8,

    /// <summary>16x64 transform.</summary>
    Size16x64,

    /// <summary>64x16 transform.</summary>
    Size64x16,
}
