// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1;

namespace SixLabors.ImageSharp.Formats.Video;

/// <summary>
/// A registry of the available <see cref="IVideoDecoder"/> implementations. The built-in
/// <see cref="Default"/> instance has the AV1 decoder pre-registered; further codecs (e.g. AV2) can be
/// added with <see cref="AddDecoder"/>.
/// </summary>
public sealed class VideoFormatManager
{
    private readonly List<IVideoDecoder> decoders = [];

    /// <summary>Gets the default manager, with the built-in codecs registered.</summary>
    public static VideoFormatManager Default { get; } = CreateDefault();

    /// <summary>Registers a video decoder.</summary>
    /// <param name="decoder">The decoder to register.</param>
    public void AddDecoder(IVideoDecoder decoder)
    {
        Guard.NotNull(decoder, nameof(decoder));
        this.decoders.Add(decoder);
    }

    /// <summary>
    /// Finds the registered decoder that recognizes the given stream header.
    /// </summary>
    /// <param name="header">The first bytes of the stream.</param>
    /// <param name="decoder">When this method returns, the matching decoder, if any.</param>
    /// <returns><see langword="true"/> if a decoder was found.</returns>
    public bool TryFindDecoder(ReadOnlySpan<byte> header, out IVideoDecoder? decoder)
    {
        foreach (IVideoDecoder candidate in this.decoders)
        {
            if (candidate.TryDetect(header))
            {
                decoder = candidate;
                return true;
            }
        }

        decoder = null;
        return false;
    }

    private static VideoFormatManager CreateDefault()
    {
        VideoFormatManager manager = new();
        manager.AddDecoder(new Av1VideoDecoder());
        return manager;
    }
}
