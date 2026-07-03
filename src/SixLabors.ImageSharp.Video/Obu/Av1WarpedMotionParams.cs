// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>The warped-motion model type, in coding order.</summary>
internal enum Av1WarpModelType
{
    /// <summary>No motion.</summary>
    Identity = 0,

    /// <summary>Pure translation (two parameters).</summary>
    Translation = 1,

    /// <summary>Rotation and zoom (four parameters).</summary>
    RotZoom = 2,

    /// <summary>Full affine model (six parameters).</summary>
    Affine = 3,
}

/// <summary>
/// A warped-motion model: the type and the six-entry affine matrix in the specification's fixed-point
/// format (translation in 1/65536 pel, the inner 2x2 in Q16 around an identity of 1 &lt;&lt; 16). The
/// reference decoder's <c>Dav1dWarpedMotionParams</c>.
/// </summary>
internal sealed class Av1WarpedMotionParams
{
    /// <summary>The identity model every reference starts from.</summary>
    public static readonly Av1WarpedMotionParams Identity = new();

    /// <summary>Initializes a new instance of the <see cref="Av1WarpedMotionParams"/> class as identity.</summary>
    public Av1WarpedMotionParams()
        => this.Matrix = [0, 0, 1 << 16, 0, 0, 1 << 16];

    /// <summary>Initializes a new instance of the <see cref="Av1WarpedMotionParams"/> class.</summary>
    /// <param name="type">The model type.</param>
    /// <param name="matrix">The six matrix entries.</param>
    public Av1WarpedMotionParams(Av1WarpModelType type, int[] matrix)
    {
        this.Type = type;
        this.Matrix = matrix;
    }

    /// <summary>Gets the model type.</summary>
    public Av1WarpModelType Type { get; }

    /// <summary>Gets the six matrix entries.</summary>
    public int[] Matrix { get; }
}
