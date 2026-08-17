// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp;

/// <summary>
/// The result of identifying a video without decoding any pixels.
/// </summary>
public sealed class VideoInfo
{
    /// <summary>Initializes a new instance of the <see cref="VideoInfo"/> class.</summary>
    /// <param name="metadata">The container-level metadata.</param>
    public VideoInfo(VideoMetadata metadata) => this.Metadata = metadata;

    /// <summary>Gets the container-level metadata.</summary>
    public VideoMetadata Metadata { get; }

    /// <summary>Gets the frame dimensions in pixels.</summary>
    public Size Size => this.Metadata.Size;

    /// <summary>Gets the number of coded frames.</summary>
    public int FrameCount => this.Metadata.FrameCount;
}
