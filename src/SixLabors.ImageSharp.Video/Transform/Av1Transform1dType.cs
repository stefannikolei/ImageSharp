// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// The one-dimensional transform kinds that compose a 2D AV1 transform.
/// </summary>
internal enum Av1Transform1dType
{
    /// <summary>Inverse discrete cosine transform.</summary>
    Dct,

    /// <summary>Inverse asymmetric discrete sine transform.</summary>
    Adst,

    /// <summary>Inverse flipped ADST (ADST with reversed output).</summary>
    FlipAdst,

    /// <summary>Identity transform (scaling only).</summary>
    Identity,
}
