// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Constants used by the AV1 codec.
/// </summary>
internal static class Av1Constants
{
    /// <summary>
    /// The list of file extensions associated with AV1 bitstreams handled by this codec.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["ivf", "obu", "av1"];

    /// <summary>
    /// The list of mime types associated with AV1.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["video/AV1", "video/x-ivf"];
}
