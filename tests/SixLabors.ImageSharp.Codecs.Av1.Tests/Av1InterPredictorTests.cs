// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the single-reference motion-compensation glue (<see cref="Av1InterPredictor"/>): the
/// integer-position and sub-pixel-offset derivation against the validated <see cref="Av1Convolve"/>
/// primitive, and the whole-pel copy semantics.
/// </summary>
public class Av1InterPredictorTests
{
    private static byte[] Gradient(int width, int height)
    {
        byte[] plane = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                plane[(y * width) + x] = (byte)((x * 3) + (y * 7));
            }
        }

        return plane;
    }

    [Fact]
    public void Predict_WholePelMotion_CopiesShiftedReference()
    {
        const int rw = 32;
        const int rh = 32;
        byte[] reference = Gradient(rw, rh);
        byte[] destination = new byte[8 * 8];

        // bx4=1,by4=1 -> pixel (4,4); mv (8,16) = +1 row, +2 cols, no sub-pixel.
        Av1InterPredictor.Predict(
            destination, 0, 8, reference, rw, rh, rw,
            bx4: 1, by4: 1, blockWidth4: 2, blockHeight4: 2,
            new Av1MotionVector(8, 16), filter0: 0, filter1: 0, subsamplingX: 0, subsamplingY: 0);

        // Source top-left = (dx, dy) = (4 + 2, 4 + 1) = (6, 5).
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Assert.Equal(reference[((5 + y) * rw) + 6 + x], destination[(y * 8) + x]);
            }
        }
    }

    [Fact]
    public void Predict_SubPixelLuma_MatchesConvolvePrimitive()
    {
        const int rw = 32;
        const int rh = 32;
        byte[] reference = Gradient(rw, rh);

        Av1MotionVector mv = new(11, 22); // y: int 1 frac 3, x: int 2 frac 6
        byte[] actual = new byte[16 * 16];
        Av1InterPredictor.Predict(
            actual, 0, 16, reference, rw, rh, rw,
            bx4: 2, by4: 2, blockWidth4: 4, blockHeight4: 4,
            mv, filter0: 1, filter1: 2, subsamplingX: 0, subsamplingY: 0);

        // Hand-derived coordinates: dx = 2*4 + (22>>3) = 8 + 2 = 10; dy = 2*4 + (11>>3) = 8 + 1 = 9.
        // mx = (22 & 7) << 1 = 6 << 1 = 12; my = (11 & 7) << 1 = 3 << 1 = 6.
        // filterType = filter1 | (filter0 << 2) = 2 | (1 << 2) = 6.
        byte[] expected = new byte[16 * 16];
        Av1Convolve.PredictBlock(expected, 0, 16, reference, rw, rh, rw, dx: 10, dy: 9, w: 16, h: 16, mx: 12, my: 6, filterType: 6);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Predict_Chroma420_DerivesSubsampledCoordinates()
    {
        const int rw = 16;
        const int rh = 16;
        byte[] reference = Gradient(rw, rh);

        Av1MotionVector mv = new(20, 36);
        byte[] actual = new byte[8 * 8];
        Av1InterPredictor.Predict(
            actual, 0, 8, reference, rw, rh, rw,
            bx4: 2, by4: 2, blockWidth4: 4, blockHeight4: 4,
            mv, filter0: 0, filter1: 0, subsamplingX: 1, subsamplingY: 1);

        // Chroma 4:2:0: h_mul=v_mul=2; dx = 2*2 + (36>>4) = 4 + 2 = 6; dy = 2*2 + (20>>4) = 4 + 1 = 5.
        // mx = (36 & 15) << 0 = 4; my = (20 & 15) << 0 = 4; width=height=4*2=8.
        byte[] expected = new byte[8 * 8];
        Av1Convolve.PredictBlock(expected, 0, 8, reference, rw, rh, rw, dx: 6, dy: 5, w: 8, h: 8, mx: 4, my: 4, filterType: 0);

        Assert.Equal(expected, actual);
    }
}
