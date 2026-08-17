// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Video;

/// <summary>
/// A codec/container decoder for a video format. Each codec (AV1 today, AV2 later) implements this and
/// is registered with a <see cref="VideoFormatManager"/>; <see cref="Video.Load(Stream)"/> picks the
/// decoder whose <see cref="TryDetect"/> recognizes the stream's header.
/// </summary>
public interface IVideoDecoder
{
    /// <summary>
    /// Returns whether this decoder recognizes the stream from its leading bytes.
    /// </summary>
    /// <param name="header">The first bytes of the stream.</param>
    /// <returns><see langword="true"/> if the format is recognized.</returns>
    bool TryDetect(ReadOnlySpan<byte> header);

    /// <summary>
    /// Reads the container/sequence headers to report the video's dimensions, frame count and frame
    /// rate without decoding any pixels. The stream must be positioned at the start of the container.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="stream">The seekable input stream.</param>
    /// <returns>The identified video info.</returns>
    VideoInfo Identify(VideoDecoderOptions options, Stream stream);

    /// <summary>
    /// Creates a stateful frame source over the stream (building the keyframe index). The frame source
    /// borrows the stream; the owning <see cref="Video"/> keeps it open until disposed.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="stream">The seekable input stream.</param>
    /// <returns>A frame source for lazy frame decoding.</returns>
    IVideoFrameSource CreateFrameSource(VideoDecoderOptions options, Stream stream);
}
