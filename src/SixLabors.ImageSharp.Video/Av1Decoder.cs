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

        Configuration configuration = options.GeneralOptions.Configuration;
        List<Av1TileDecoder> frames = Av1DecoderCore.DecodeAllFrames(stream);

        Image<TPixel> image = Av1FrameConverter.ToImage<TPixel>(frames[0], configuration);
        for (int i = 1; i < frames.Count; i++)
        {
            using Image<TPixel> frameImage = Av1FrameConverter.ToImage<TPixel>(frames[i], configuration);
            image.Frames.AddFrame(frameImage.Frames.RootFrame);
        }

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(Av1DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override Av1DecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
        => new() { GeneralOptions = options };
}
