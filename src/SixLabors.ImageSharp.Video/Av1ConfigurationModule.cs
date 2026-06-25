// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Registers the decoder and mime type detector for the AV1 format.
/// </summary>
/// <remarks>
/// This module is opt-in: it is not part of the default <see cref="Configuration"/>. Enable AV1
/// support with <c>configuration.Configure(new Av1ConfigurationModule())</c>.
/// </remarks>
public sealed class Av1ConfigurationModule : IImageFormatConfigurationModule
{
    /// <inheritdoc/>
    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.SetDecoder(Av1Format.Instance, Av1Decoder.Instance);
        configuration.ImageFormatsManager.AddImageFormatDetector(new Av1ImageFormatDetector());
    }
}
