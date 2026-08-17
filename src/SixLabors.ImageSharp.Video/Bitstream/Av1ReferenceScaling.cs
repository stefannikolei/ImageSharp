// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The per-reference scaling factors of a frame whose reference has a different resolution than the
/// frame's own coded size (the reference decoder's <c>ScalableMotionParams f->svc[i]</c>): a 14-bit
/// fixed-point scale per axis, and the 10-bit fixed-point source step per output sample derived from
/// it. A same-size reference carries scale 0 and takes the unscaled motion-compensation path.
/// </summary>
internal readonly struct Av1ReferenceScaling
{
    private Av1ReferenceScaling(int scaleX, int scaleY, int stepX, int stepY)
    {
        this.ScaleX = scaleX;
        this.ScaleY = scaleY;
        this.StepX = stepX;
        this.StepY = stepY;
    }

    /// <summary>Gets the horizontal scale in 1/16384 units (0 for a same-size reference).</summary>
    public int ScaleX { get; }

    /// <summary>Gets the vertical scale in 1/16384 units.</summary>
    public int ScaleY { get; }

    /// <summary>Gets the horizontal source step per output sample in 1/1024-pel units.</summary>
    public int StepX { get; }

    /// <summary>Gets the vertical source step per output row in 1/1024-pel units.</summary>
    public int StepY { get; }

    /// <summary>Gets a value indicating whether the reference is scaled.</summary>
    public bool IsScaled => this.ScaleX != 0;

    /// <summary>
    /// Computes the scaling of one reference (dav1d's <c>scale_fac</c> setup): scaled when the
    /// reference's stored (upscaled) dimensions differ from the current frame's coded dimensions.
    /// </summary>
    /// <param name="referenceWidth">The reference frame's stored width.</param>
    /// <param name="referenceHeight">The reference frame's stored height.</param>
    /// <param name="frameWidth">The current frame's coded width.</param>
    /// <param name="frameHeight">The current frame's coded height.</param>
    /// <returns>The scaling parameters.</returns>
    public static Av1ReferenceScaling Compute(int referenceWidth, int referenceHeight, int frameWidth, int frameHeight)
    {
        if (referenceWidth == frameWidth && referenceHeight == frameHeight)
        {
            return default;
        }

        int scaleX = ((referenceWidth << 14) + (frameWidth >> 1)) / frameWidth;
        int scaleY = ((referenceHeight << 14) + (frameHeight >> 1)) / frameHeight;
        return new Av1ReferenceScaling(scaleX, scaleY, (scaleX + 8) >> 4, (scaleY + 8) >> 4);
    }
}
