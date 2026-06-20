// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A decoded reference frame retained for inter prediction: the reconstructed planes plus the order hint
/// used to order references and derive motion-vector scaling.
/// </summary>
internal sealed class Av1ReferenceFrame
{
    /// <summary>Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class.</summary>
    /// <param name="orderHint">The frame's order hint.</param>
    /// <param name="luma">The reconstructed luma plane.</param>
    /// <param name="chromaU">The reconstructed U plane (or <see langword="null"/> for monochrome).</param>
    /// <param name="chromaV">The reconstructed V plane (or <see langword="null"/> for monochrome).</param>
    public Av1ReferenceFrame(int orderHint, Av1Plane luma, Av1Plane? chromaU, Av1Plane? chromaV)
    {
        this.OrderHint = orderHint;
        this.Luma = luma;
        this.ChromaU = chromaU;
        this.ChromaV = chromaV;
    }

    /// <summary>Gets the frame's order hint.</summary>
    public int OrderHint { get; }

    /// <summary>Gets the reconstructed luma plane.</summary>
    public Av1Plane Luma { get; }

    /// <summary>Gets the reconstructed U plane, or <see langword="null"/> for monochrome.</summary>
    public Av1Plane? ChromaU { get; }

    /// <summary>Gets the reconstructed V plane, or <see langword="null"/> for monochrome.</summary>
    public Av1Plane? ChromaV { get; }
}
