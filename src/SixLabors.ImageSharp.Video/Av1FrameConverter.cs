// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Converts a reconstructed AV1 frame (planar YUV) into an <see cref="Image{TPixel}"/> using the
/// specification's default BT.601 limited-range matrix. The conversion is stateless and shared by the
/// image decode path (<see cref="Av1Decoder"/>) and the lazy video decode path.
/// </summary>
internal static class Av1FrameConverter
{
    /// <summary>
    /// Converts a decoded frame into a new single-frame <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="frame">The decoded frame holding the reconstructed luma and chroma planes.</param>
    /// <param name="configuration">The configuration used to allocate the image.</param>
    /// <returns>The converted image.</returns>
    public static Image<TPixel> ToImage<TPixel>(Av1TileDecoder frame, Configuration configuration)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Av1Plane luma = frame.Luma;
        Av1Plane chromaU = frame.ChromaU;
        Av1Plane chromaV = frame.ChromaV;
        int width = luma.CropWidth;
        int height = luma.CropHeight;

        // Chroma subsampling ratios inferred from the plane dimensions (4:2:0, 4:2:2 or 4:4:4).
        int subsampleX = luma.Width > chromaU.Width ? 1 : 0;
        int subsampleY = luma.Height > chromaU.Height ? 1 : 0;

        Image<TPixel> image = new(configuration, width, height);
        Buffer2D<TPixel> buffer = image.Frames.RootFrame.PixelBuffer;

        Rgba32 rgba = default;
        for (int y = 0; y < height; y++)
        {
            Span<TPixel> row = buffer.DangerousGetRowSpan(y);
            int cy = y >> subsampleY;
            for (int x = 0; x < width; x++)
            {
                int cx = x >> subsampleX;
                YuvToRgb(luma[x, y], chromaU[cx, cy], chromaV[cx, cy], ref rgba);
                row[x] = TPixel.FromRgba32(rgba);
            }
        }

        return image;
    }

    // BT.601 limited-range YUV to RGB conversion (specification's default matrix for 8-bit content).
    private static void YuvToRgb(byte yy, byte uu, byte vv, ref Rgba32 rgba)
    {
        float y = 1.164f * (yy - 16);
        float u = uu - 128;
        float v = vv - 128;
        rgba.R = ClampToByte(y + (1.596f * v));
        rgba.G = ClampToByte(y - (0.391f * u) - (0.813f * v));
        rgba.B = ClampToByte(y + (2.018f * u));
        rgba.A = 255;
    }

    private static byte ClampToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
}
