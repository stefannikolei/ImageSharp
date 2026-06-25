// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Video;

/// <summary>
/// A codec-specific, stateful source of decoded video frames. Implementations own the decode state
/// (e.g. the reference-frame buffer and a keyframe index) and decode a requested frame on demand,
/// seeking to the nearest preceding keyframe and decoding forward when necessary.
/// </summary>
public interface IVideoFrameSource : IDisposable
{
    /// <summary>Gets the frame dimensions in pixels.</summary>
    Size Size { get; }

    /// <summary>Gets the number of coded frames.</summary>
    int FrameCount { get; }

    /// <summary>Gets the container-level metadata.</summary>
    VideoMetadata Metadata { get; }

    /// <summary>
    /// Decodes the frame at the given index into a new single-frame image.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="frameIndex">The zero-based frame index.</param>
    /// <param name="configuration">The configuration used to allocate the image.</param>
    /// <returns>The decoded frame as a standalone image owned by the caller.</returns>
    Image<TPixel> DecodeFrame<TPixel>(int frameIndex, Configuration configuration)
        where TPixel : unmanaged, IPixel<TPixel>;
}
