// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp;

/// <summary>
/// Options controlling how a <see cref="Video"/> is opened and how its frames are decoded.
/// </summary>
public sealed class VideoDecoderOptions
{
    /// <summary>
    /// Gets the configuration used to allocate decoded frame images.
    /// </summary>
    public Configuration Configuration { get; init; } = Configuration.Default;
}
