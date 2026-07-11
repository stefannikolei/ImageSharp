// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The super-resolution horizontal upscaler (specification section 7.16), a port of dav1d's
/// <c>resize_c</c> with the 64-phase 8-tap <c>dav1d_resize_filter</c>. Each output row is produced
/// from the same source row alone; the upscale runs between CDEF and loop restoration.
/// </summary>
internal static class Av1SuperRes
{
    private static readonly sbyte[][] ResizeFilter =
    [
        [0, 0, 0, -128, 0, 0, 0, 0],
        [0, 0, 1, -128, -2, 1, 0, 0],
        [0, -1, 3, -127, -4, 2, -1, 0],
        [0, -1, 4, -127, -6, 3, -1, 0],
        [0, -2, 6, -126, -8, 3, -1, 0],
        [0, -2, 7, -125, -11, 4, -1, 0],
        [1, -2, 8, -125, -13, 5, -2, 0],
        [1, -3, 9, -124, -15, 6, -2, 0],
        [1, -3, 10, -123, -18, 6, -2, 1],
        [1, -3, 11, -122, -20, 7, -3, 1],
        [1, -4, 12, -121, -22, 8, -3, 1],
        [1, -4, 13, -120, -25, 9, -3, 1],
        [1, -4, 14, -118, -28, 9, -3, 1],
        [1, -4, 15, -117, -30, 10, -4, 1],
        [1, -5, 16, -116, -32, 11, -4, 1],
        [1, -5, 16, -114, -35, 12, -4, 1],
        [1, -5, 17, -112, -38, 12, -4, 1],
        [1, -5, 18, -111, -40, 13, -5, 1],
        [1, -5, 18, -109, -43, 14, -5, 1],
        [1, -6, 19, -107, -45, 14, -5, 1],
        [1, -6, 19, -105, -48, 15, -5, 1],
        [1, -6, 19, -103, -51, 16, -5, 1],
        [1, -6, 20, -101, -53, 16, -6, 1],
        [1, -6, 20, -99, -56, 17, -6, 1],
        [1, -6, 20, -97, -58, 17, -6, 1],
        [1, -6, 20, -95, -61, 18, -6, 1],
        [2, -7, 20, -93, -64, 18, -6, 2],
        [2, -7, 20, -91, -66, 19, -6, 1],
        [2, -7, 20, -88, -69, 19, -6, 1],
        [2, -7, 20, -86, -71, 19, -6, 1],
        [2, -7, 20, -84, -74, 20, -7, 2],
        [2, -7, 20, -81, -76, 20, -7, 1],
        [2, -7, 20, -79, -79, 20, -7, 2],
        [1, -7, 20, -76, -81, 20, -7, 2],
        [2, -7, 20, -74, -84, 20, -7, 2],
        [1, -6, 19, -71, -86, 20, -7, 2],
        [1, -6, 19, -69, -88, 20, -7, 2],
        [1, -6, 19, -66, -91, 20, -7, 2],
        [2, -6, 18, -64, -93, 20, -7, 2],
        [1, -6, 18, -61, -95, 20, -6, 1],
        [1, -6, 17, -58, -97, 20, -6, 1],
        [1, -6, 17, -56, -99, 20, -6, 1],
        [1, -6, 16, -53, -101, 20, -6, 1],
        [1, -5, 16, -51, -103, 19, -6, 1],
        [1, -5, 15, -48, -105, 19, -6, 1],
        [1, -5, 14, -45, -107, 19, -6, 1],
        [1, -5, 14, -43, -109, 18, -5, 1],
        [1, -5, 13, -40, -111, 18, -5, 1],
        [1, -4, 12, -38, -112, 17, -5, 1],
        [1, -4, 12, -35, -114, 16, -5, 1],
        [1, -4, 11, -32, -116, 16, -5, 1],
        [1, -4, 10, -30, -117, 15, -4, 1],
        [1, -3, 9, -28, -118, 14, -4, 1],
        [1, -3, 9, -25, -120, 13, -4, 1],
        [1, -3, 8, -22, -121, 12, -4, 1],
        [1, -3, 7, -20, -122, 11, -3, 1],
        [1, -2, 6, -18, -123, 10, -3, 1],
        [0, -2, 6, -15, -124, 9, -3, 1],
        [0, -2, 5, -13, -125, 8, -2, 1],
        [0, -1, 4, -11, -125, 7, -2, 0],
        [0, -1, 3, -8, -126, 6, -2, 0],
        [0, -1, 3, -6, -127, 4, -1, 0],
        [0, -1, 2, -4, -127, 3, -1, 0],
        [0, 0, 1, -2, -128, 1, 0, 0],
    ];

    /// <summary>Computes the 14-bit x step for an upscale (dav1d <c>scale_fac</c>).</summary>
    /// <param name="inWidth">The coded (downscaled) width.</param>
    /// <param name="outWidth">The upscaled width.</param>
    /// <returns>The per-output-pixel source step in 1/16384 units.</returns>
    public static int ComputeStep(int inWidth, int outWidth) => ((inWidth << 14) + (outWidth >> 1)) / outWidth;

    /// <summary>Computes the initial subpixel offset (dav1d <c>get_upscale_x0</c>).</summary>
    /// <param name="inWidth">The coded (downscaled) width.</param>
    /// <param name="outWidth">The upscaled width.</param>
    /// <param name="step">The step from <see cref="ComputeStep"/>.</param>
    /// <returns>The 14-bit start offset.</returns>
    public static int ComputeStart(int inWidth, int outWidth, int step)
    {
        int err = (outWidth * step) - (inWidth << 14);
        int x0 = ((-((outWidth - inWidth) << 13) + (outWidth >> 1)) / outWidth) + 128 - (err / 2);
        return x0 & 0x3fff;
    }

    /// <summary>
    /// Horizontally upscales the visible area of a plane into a new plane of the target width. The
    /// allocated width rounds up to the plane's 4x4 grid like the decoder's other allocations.
    /// </summary>
    /// <param name="source">The coded-resolution plane.</param>
    /// <param name="outWidth">The upscaled visible width.</param>
    /// <param name="bitDepth">The stream bit depth.</param>
    /// <param name="readWidth">The source width the kernel's edge clamp uses. The reference decoder
    /// clamps at the 4x4-aligned coded width (<c>src_w = 4*f->bw >> ss_hor</c>), so the kernel reads
    /// real reconstructed samples beyond an unaligned crop width; the step and phase still derive
    /// from the cropped width. Zero uses the crop width.</param>
    /// <returns>The upscaled plane.</returns>
    public static Av1Plane Upscale(Av1Plane source, int outWidth, int bitDepth, int readWidth = 0)
    {
        int srcWidth = source.CropWidth;
        if (readWidth == 0)
        {
            readWidth = srcWidth;
        }

        int step = ComputeStep(srcWidth, outWidth);
        int start = ComputeStart(srcWidth, outWidth, step);
        int allocWidth = Math.Max(outWidth, (outWidth + 7) & ~7);
        Av1Plane dst = new(allocWidth, source.Height, outWidth, source.CropHeight);
        int maxValue = (1 << bitDepth) - 1;

        for (int y = 0; y < source.Height; y++)
        {
            int srcRow = y * source.Width;
            int dstRow = y * dst.Width;
            int mx = start;
            int srcX = -1;
            for (int x = 0; x < outWidth; x++)
            {
                sbyte[] f = ResizeFilter[mx >> 8];
                int sum = 0;
                for (int t = 0; t < 8; t++)
                {
                    sum += f[t] * source.Samples[srcRow + Math.Clamp(srcX - 3 + t, 0, readWidth - 1)];
                }

                dst.Samples[dstRow + x] = (ushort)Math.Clamp((-sum + 64) >> 7, 0, maxValue);
                mx += step;
                srcX += mx >> 14;
                mx &= 0x3fff;
            }

            // Extend the row to the allocated width so later kernels reading the padding stay defined.
            for (int x = outWidth; x < dst.Width; x++)
            {
                dst.Samples[dstRow + x] = dst.Samples[dstRow + outWidth - 1];
            }
        }

        return dst;
    }
}
