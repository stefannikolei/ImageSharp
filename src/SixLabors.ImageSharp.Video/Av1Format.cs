// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Registers the image decoders and mime type detectors for the AV1 format.
/// </summary>
public sealed class Av1Format : IImageFormat
{
    private Av1Format()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static Av1Format Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "AV1";

    /// <inheritdoc/>
    public string DefaultMimeType => "video/AV1";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => Av1Constants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => Av1Constants.FileExtensions;
}
