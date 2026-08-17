// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;
using SixLabors.ImageSharp.Formats.Video;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// The AV1 implementation of <see cref="IVideoDecoder"/>: detects an IVF-wrapped AV1 stream, reports its
/// container metadata, and creates a lazily-seeking <see cref="Av1VideoFrameSource"/>.
/// </summary>
internal sealed class Av1VideoDecoder : IVideoDecoder
{
    /// <inheritdoc/>
    public bool TryDetect(ReadOnlySpan<byte> header)
        => header.Length >= IvfFileHeader.Size
           && header[..4].SequenceEqual(IvfFileHeader.Signature)
           && header.Slice(8, 4).SequenceEqual(IvfFileHeader.Av1FourCc);

    /// <inheritdoc/>
    public VideoInfo Identify(VideoDecoderOptions options, Stream stream)
    {
        long position = stream.Position;
        Span<byte> buffer = stackalloc byte[IvfFileHeader.Size];
        stream.ReadExactly(buffer);
        stream.Position = position;

        IvfFileHeader fileHeader = IvfFileHeader.Parse(buffer);
        if (!fileHeader.IsAv1)
        {
            throw new InvalidDataException($"Unsupported IVF codec FourCC '{fileHeader.FourCc}', expected AV1.");
        }

        VideoMetadata metadata = new()
        {
            Size = new Size(fileHeader.Width, fileHeader.Height),
            FrameCount = (int)fileHeader.FrameCount,
            FrameRateNumerator = (int)fileHeader.FrameRateNumerator,
            FrameRateDenominator = (int)fileHeader.FrameRateDenominator,
        };

        return new VideoInfo(metadata);
    }

    /// <inheritdoc/>
    public IVideoFrameSource CreateFrameSource(VideoDecoderOptions options, Stream stream)
        => new Av1VideoFrameSource(stream);
}
