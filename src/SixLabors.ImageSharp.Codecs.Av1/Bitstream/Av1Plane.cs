// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A single reconstructed image plane (8-bit samples) backed by a dense row-major buffer.
/// </summary>
internal sealed class Av1Plane
{
    private readonly byte[] samples;

    public Av1Plane(int width, int height)
    {
        this.Width = width;
        this.Height = height;
        this.samples = new byte[width * height];
    }

    /// <summary>Gets the plane width in samples.</summary>
    public int Width { get; }

    /// <summary>Gets the plane height in samples.</summary>
    public int Height { get; }

    /// <summary>Gets the backing sample buffer in row-major order.</summary>
    public byte[] Samples => this.samples;

    /// <summary>Gets or sets the sample at the given coordinate.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    public byte this[int x, int y]
    {
        get => this.samples[(y * this.Width) + x];
        set => this.samples[(y * this.Width) + x] = value;
    }
}
