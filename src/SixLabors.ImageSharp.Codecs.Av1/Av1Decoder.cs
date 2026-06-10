// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        // Validate the container and headers so malformed input still fails cleanly before
        // reporting that pixel reconstruction is not yet available.
        _ = Av1DecoderCore.ReadDimensions(stream);

        throw new NotSupportedException(
            "AV1 frame decoding is not yet implemented. The current build supports container and " +
            "sequence-header parsing (Identify) only. See docs/av1-codec-roadmap.md.");
    }

    /// <inheritdoc/>
    protected override Image Decode(Av1DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override Av1DecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
        => new() { GeneralOptions = options };
}
