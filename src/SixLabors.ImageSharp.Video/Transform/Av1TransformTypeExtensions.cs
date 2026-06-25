// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// Maps a <see cref="Av1TransformType"/> to its vertical (column) and horizontal (row)
/// one-dimensional transforms.
/// </summary>
internal static class Av1TransformTypeExtensions
{
    private static readonly Av1Transform1dType[] Vertical =
    [
        Av1Transform1dType.Dct,       // DctDct
        Av1Transform1dType.Adst,      // AdstDct
        Av1Transform1dType.Dct,       // DctAdst
        Av1Transform1dType.Adst,      // AdstAdst
        Av1Transform1dType.FlipAdst,  // FlipAdstDct
        Av1Transform1dType.Dct,       // DctFlipAdst
        Av1Transform1dType.FlipAdst,  // FlipAdstFlipAdst
        Av1Transform1dType.Adst,      // AdstFlipAdst
        Av1Transform1dType.FlipAdst,  // FlipAdstAdst
        Av1Transform1dType.Identity,  // Identity
        Av1Transform1dType.Dct,       // VerticalDct
        Av1Transform1dType.Identity,  // HorizontalDct
        Av1Transform1dType.Adst,      // VerticalAdst
        Av1Transform1dType.Identity,  // HorizontalAdst
        Av1Transform1dType.FlipAdst,  // VerticalFlipAdst
        Av1Transform1dType.Identity,  // HorizontalFlipAdst
    ];

    private static readonly Av1Transform1dType[] Horizontal =
    [
        Av1Transform1dType.Dct,       // DctDct
        Av1Transform1dType.Dct,       // AdstDct
        Av1Transform1dType.Adst,      // DctAdst
        Av1Transform1dType.Adst,      // AdstAdst
        Av1Transform1dType.Dct,       // FlipAdstDct
        Av1Transform1dType.FlipAdst,  // DctFlipAdst
        Av1Transform1dType.FlipAdst,  // FlipAdstFlipAdst
        Av1Transform1dType.FlipAdst,  // AdstFlipAdst
        Av1Transform1dType.Adst,      // FlipAdstAdst
        Av1Transform1dType.Identity,  // Identity
        Av1Transform1dType.Identity,  // VerticalDct
        Av1Transform1dType.Dct,       // HorizontalDct
        Av1Transform1dType.Identity,  // VerticalAdst
        Av1Transform1dType.Adst,      // HorizontalAdst
        Av1Transform1dType.Identity,  // VerticalFlipAdst
        Av1Transform1dType.FlipAdst,  // HorizontalFlipAdst
    ];

    /// <summary>Gets the vertical (column) 1D transform for a transform type.</summary>
    /// <param name="type">The transform type.</param>
    /// <returns>The vertical 1D transform.</returns>
    public static Av1Transform1dType GetVertical(this Av1TransformType type) => Vertical[(int)type];

    /// <summary>Gets the horizontal (row) 1D transform for a transform type.</summary>
    /// <param name="type">The transform type.</param>
    /// <returns>The horizontal 1D transform.</returns>
    public static Av1Transform1dType GetHorizontal(this Av1TransformType type) => Horizontal[(int)type];

    /// <summary>
    /// Gets the transform class (2D / horizontal / vertical) used by the coefficient reader for
    /// scan order and neighbour context selection.
    /// </summary>
    /// <param name="type">The transform type.</param>
    /// <returns>The transform class.</returns>
    public static Av1TransformClass GetTransformClass(this Av1TransformType type)
    {
        // Vertical class: a non-identity vertical transform with identity horizontal.
        if (type is Av1TransformType.VerticalDct or Av1TransformType.VerticalAdst or Av1TransformType.VerticalFlipAdst)
        {
            return Av1TransformClass.Vertical;
        }

        // Horizontal class: a non-identity horizontal transform with identity vertical.
        if (type is Av1TransformType.HorizontalDct or Av1TransformType.HorizontalAdst or Av1TransformType.HorizontalFlipAdst)
        {
            return Av1TransformClass.Horizontal;
        }

        return Av1TransformClass.TwoDimensional;
    }
}
