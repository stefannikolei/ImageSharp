// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Configuration options for decoding AV1 bitstreams.
/// </summary>
public sealed class Av1DecoderOptions : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; init; } = new();
}
