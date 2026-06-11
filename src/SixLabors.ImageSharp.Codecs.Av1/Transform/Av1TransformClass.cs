// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// The transform class used by the coefficient reader to select scan order and neighbour contexts
/// (specification section 8.3.2 / <c>Tx_Type_To_Class</c>).
/// </summary>
internal enum Av1TransformClass
{
    /// <summary>Two-dimensional transform (both passes non-identity, or identity-identity).</summary>
    TwoDimensional,

    /// <summary>Horizontal class: a 1D transform horizontally, identity vertically.</summary>
    Horizontal,

    /// <summary>Vertical class: a 1D transform vertically, identity horizontally.</summary>
    Vertical,
}
