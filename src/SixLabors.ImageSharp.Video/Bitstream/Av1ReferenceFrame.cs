// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A decoded reference frame retained for inter prediction: the reconstructed planes plus the order hint
/// used to order references, and the frame-end context (CDF state and header inheritance values) a later
/// frame with <c>primary_ref_frame</c> pointing at this slot loads instead of the defaults.
/// </summary>
internal sealed class Av1ReferenceFrame
{
    /// <summary>Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class.</summary>
    /// <param name="orderHint">The frame's order hint.</param>
    /// <param name="luma">The reconstructed luma plane.</param>
    /// <param name="chromaU">The reconstructed U plane (or <see langword="null"/> for monochrome).</param>
    /// <param name="chromaV">The reconstructed V plane (or <see langword="null"/> for monochrome).</param>
    /// <param name="cdfs">The frame-end CDF state (or <see langword="null"/> when unavailable, in which
    /// case a primary reference to this slot falls back to the defaults).</param>
    /// <param name="headerState">The frame-end header inheritance state (or <see langword="null"/> for
    /// the specification defaults).</param>
    public Av1ReferenceFrame(int orderHint, Av1Plane luma, Av1Plane? chromaU, Av1Plane? chromaV, Av1FrameCdfSet? cdfs = null, ObuPrimaryReferenceState? headerState = null)
    {
        this.OrderHint = orderHint;
        this.Luma = luma;
        this.ChromaU = chromaU;
        this.ChromaV = chromaV;
        this.Cdfs = cdfs;
        this.HeaderState = headerState;
    }

    /// <summary>Gets the frame's order hint.</summary>
    public int OrderHint { get; }

    /// <summary>Gets the reconstructed luma plane.</summary>
    public Av1Plane Luma { get; }

    /// <summary>Gets the reconstructed U plane, or <see langword="null"/> for monochrome.</summary>
    public Av1Plane? ChromaU { get; }

    /// <summary>Gets the reconstructed V plane, or <see langword="null"/> for monochrome.</summary>
    public Av1Plane? ChromaV { get; }

    /// <summary>Gets the frame-end CDF state, or <see langword="null"/> when unavailable.</summary>
    public Av1FrameCdfSet? Cdfs { get; }

    /// <summary>Gets the frame-end header inheritance state, or <see langword="null"/> for the defaults.</summary>
    public ObuPrimaryReferenceState? HeaderState { get; }
}
