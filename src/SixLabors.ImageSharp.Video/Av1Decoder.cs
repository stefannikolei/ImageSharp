// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Image decoder for AV1 bitstreams.
/// </summary>
/// <remarks>
/// This is an early foundation: <see cref="Identify(DecoderOptions, Stream, CancellationToken)"/>
/// parses the container and sequence header to report the image dimensions, while the pixel
/// reconstruction path is not yet implemented. See docs/av1-codec-roadmap.md for the full plan.
/// </remarks>
public sealed class Av1Decoder : SpecializedImageDecoder<Av1DecoderOptions>
{
    private Av1Decoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static Av1Decoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        Size size = Av1DecoderCore.ReadDimensions(stream);
        return new ImageInfo(size, new ImageMetadata());
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(Av1DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        List<Av1TileDecoder> frames = Av1DecoderCore.DecodeAllFrames(stream);

        int width = frames[0].Luma.Width;
        int height = frames[0].Luma.Height;
        Image<TPixel> image = new(width, height);
        CopyFrame(frames[0], image.Frames.RootFrame);
        for (int i = 1; i < frames.Count; i++)
        {
            TPixel[] buffer = new TPixel[width * height];
            ConvertFrame(frames[i], buffer);
            image.Frames.AddFrame(buffer);
        }

        return image;
    }

    private static void CopyFrame<TPixel>(Av1TileDecoder frame, ImageFrame<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TPixel[] buffer = new TPixel[destination.Width * destination.Height];
        ConvertFrame(frame, buffer);
        for (int y = 0; y < destination.Height; y++)
        {
            Span<TPixel> row = destination.PixelBuffer.DangerousGetRowSpan(y);
            buffer.AsSpan(y * destination.Width, destination.Width).CopyTo(row);
        }
    }

    private static void ConvertFrame<TPixel>(Av1TileDecoder frame, TPixel[] buffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Av1Plane luma = frame.Luma;
        Av1Plane chromaU = frame.ChromaU;
        Av1Plane chromaV = frame.ChromaV;
        int width = luma.Width;
        int height = luma.Height;

        // Chroma subsampling ratios inferred from the plane dimensions (4:2:0, 4:2:2 or 4:4:4).
        int subsampleX = width > chromaU.Width ? 1 : 0;
        int subsampleY = height > chromaU.Height ? 1 : 0;

        Rgba32 rgba = default;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cx = x >> subsampleX;
                int cy = y >> subsampleY;
                YuvToRgb(luma[x, y], chromaU[cx, cy], chromaV[cx, cy], ref rgba);
                buffer[(y * width) + x] = TPixel.FromRgba32(rgba);
            }
        }
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

    /// <inheritdoc/>
    protected override Image Decode(Av1DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override Av1DecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
        => new() { GeneralOptions = options };
}
