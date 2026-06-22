// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Generates the motion-compensated prediction for a single-reference inter block, deriving the
/// integer reference position and sub-pixel offsets from the block position and motion vector and
/// invoking the validated <see cref="Av1Convolve"/> sub-pixel interpolation. This is a port of the
/// per-plane motion-compensation setup in the reference decoder's <c>mc</c> (<c>recon_tmpl.c</c>) for
/// non-scaled, single-reference prediction.
/// </summary>
internal static class Av1InterPredictor
{
    /// <summary>
    /// Writes the motion-compensated prediction of a block plane into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination plane buffer.</param>
    /// <param name="destinationOffset">The offset of the block's top-left sample in the destination.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="reference">The reference plane buffer.</param>
    /// <param name="referenceWidth">The reference plane width in samples.</param>
    /// <param name="referenceHeight">The reference plane height in samples.</param>
    /// <param name="referenceStride">The reference plane row stride.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="blockWidth4">The block width in 4x4 units.</param>
    /// <param name="blockHeight4">The block height in 4x4 units.</param>
    /// <param name="motionVector">The block motion vector in eighth-pel units.</param>
    /// <param name="filter0">The first (vertical) interpolation filter.</param>
    /// <param name="filter1">The second (horizontal) interpolation filter.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling (0 for luma).</param>
    /// <param name="subsamplingY">The vertical chroma subsampling (0 for luma).</param>
    public static void Predict(
        byte[] destination,
        int destinationOffset,
        int destinationStride,
        byte[] reference,
        int referenceWidth,
        int referenceHeight,
        int referenceStride,
        int bx4,
        int by4,
        int blockWidth4,
        int blockHeight4,
        Av1MotionVector motionVector,
        int filter0,
        int filter1,
        int subsamplingX,
        int subsamplingY)
    {
        Coordinates c = Derive(bx4, by4, blockWidth4, blockHeight4, motionVector, filter0, filter1, subsamplingX, subsamplingY);
        Av1Convolve.PredictBlock(
            destination,
            destinationOffset,
            destinationStride,
            reference,
            referenceWidth,
            referenceHeight,
            referenceStride,
            c.Dx,
            c.Dy,
            c.Width,
            c.Height,
            c.Mx,
            c.My,
            c.FilterType);
    }

    /// <summary>
    /// Produces the int16 compound-prediction intermediate for one reference of a block plane (the
    /// <c>prep</c> pass that precedes a compound blend).
    /// </summary>
    /// <param name="intermediate">The destination intermediate buffer (length width*height).</param>
    /// <param name="reference">The reference plane buffer.</param>
    /// <param name="referenceWidth">The reference plane width in samples.</param>
    /// <param name="referenceHeight">The reference plane height in samples.</param>
    /// <param name="referenceStride">The reference plane row stride.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="blockWidth4">The block width in 4x4 units.</param>
    /// <param name="blockHeight4">The block height in 4x4 units.</param>
    /// <param name="motionVector">The block motion vector in eighth-pel units.</param>
    /// <param name="filter0">The first (vertical) interpolation filter.</param>
    /// <param name="filter1">The second (horizontal) interpolation filter.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling (0 for luma).</param>
    /// <param name="subsamplingY">The vertical chroma subsampling (0 for luma).</param>
    public static void Prepare(
        short[] intermediate,
        byte[] reference,
        int referenceWidth,
        int referenceHeight,
        int referenceStride,
        int bx4,
        int by4,
        int blockWidth4,
        int blockHeight4,
        Av1MotionVector motionVector,
        int filter0,
        int filter1,
        int subsamplingX,
        int subsamplingY)
    {
        Coordinates c = Derive(bx4, by4, blockWidth4, blockHeight4, motionVector, filter0, filter1, subsamplingX, subsamplingY);
        Av1Convolve.PrepBlock(
            intermediate,
            reference,
            referenceWidth,
            referenceHeight,
            referenceStride,
            c.Dx,
            c.Dy,
            c.Width,
            c.Height,
            c.Mx,
            c.My,
            c.FilterType);
    }

    private static Coordinates Derive(
        int bx4,
        int by4,
        int blockWidth4,
        int blockHeight4,
        Av1MotionVector motionVector,
        int filter0,
        int filter1,
        int subsamplingX,
        int subsamplingY)
    {
        int horizontalMultiplier = 4 >> subsamplingX;
        int verticalMultiplier = 4 >> subsamplingY;
        int horizontalShift = subsamplingX == 0 ? 1 : 0; // !ss_hor
        int verticalShift = subsamplingY == 0 ? 1 : 0;    // !ss_ver

        return new Coordinates
        {
            Dx = (bx4 * horizontalMultiplier) + (motionVector.X >> (3 + subsamplingX)),
            Dy = (by4 * verticalMultiplier) + (motionVector.Y >> (3 + subsamplingY)),
            Mx = (motionVector.X & (15 >> horizontalShift)) << horizontalShift,
            My = (motionVector.Y & (15 >> verticalShift)) << verticalShift,
            Width = blockWidth4 * horizontalMultiplier,
            Height = blockHeight4 * verticalMultiplier,
            FilterType = filter1 | (filter0 << 2),
        };
    }

    private struct Coordinates
    {
        public int Dx;
        public int Dy;
        public int Mx;
        public int My;
        public int Width;
        public int Height;
        public int FilterType;
    }
}
