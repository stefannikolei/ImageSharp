// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A single reconstructed image plane (8-bit samples) backed by a dense row-major buffer. The allocated
/// area covers the frame's whole 4x4 block grid so transform blocks reconstruct fully even when they
/// overhang the visible frame (the reference decoder pads its planes the same way); the crop dimensions
/// give the visible size used for output and for motion-compensation edge replication.
/// </summary>
internal sealed class Av1Plane
{
    private readonly byte[] samples;

    public Av1Plane(int width, int height)
        : this(width, height, width, height)
    {
    }

    public Av1Plane(int width, int height, int cropWidth, int cropHeight)
    {
        this.Width = width;
        this.Height = height;
        this.CropWidth = cropWidth;
        this.CropHeight = cropHeight;
        this.samples = new byte[width * height];
    }

    /// <summary>Gets the allocated plane width in samples (the row stride).</summary>
    public int Width { get; }

    /// <summary>Gets the allocated plane height in samples.</summary>
    public int Height { get; }

    /// <summary>Gets the visible frame width in samples.</summary>
    public int CropWidth { get; }

    /// <summary>Gets the visible frame height in samples.</summary>
    public int CropHeight { get; }

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
