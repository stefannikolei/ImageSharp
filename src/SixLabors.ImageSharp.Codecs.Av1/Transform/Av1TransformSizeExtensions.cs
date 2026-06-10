// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// Dimension helpers for <see cref="Av1TransformSize"/>.
/// </summary>
internal static class Av1TransformSizeExtensions
{
    private static readonly int[] Widths =
        [4, 8, 16, 32, 64, 4, 8, 8, 16, 16, 32, 32, 64, 4, 16, 8, 32, 16, 64];

    private static readonly int[] Heights =
        [4, 8, 16, 32, 64, 8, 4, 16, 8, 32, 16, 64, 32, 16, 4, 32, 8, 64, 16];

    /// <summary>Gets the transform width in samples.</summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The width.</returns>
    public static int GetWidth(this Av1TransformSize size) => Widths[(int)size];

    /// <summary>Gets the transform height in samples.</summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The height.</returns>
    public static int GetHeight(this Av1TransformSize size) => Heights[(int)size];

    /// <summary>Gets the base-2 logarithm of the transform width.</summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The log2 of the width.</returns>
    public static int GetWidthLog2(this Av1TransformSize size) => System.Numerics.BitOperations.Log2((uint)Widths[(int)size]);

    /// <summary>Gets the base-2 logarithm of the transform height.</summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The log2 of the height.</returns>
    public static int GetHeightLog2(this Av1TransformSize size) => System.Numerics.BitOperations.Log2((uint)Heights[(int)size]);
}
