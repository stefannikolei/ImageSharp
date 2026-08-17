// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Detects AV1 bitstreams carried in an IVF container.
/// </summary>
/// <remarks>
/// Only the IVF container is auto-detected, because it carries the unambiguous "DKIF" signature.
/// A raw low-overhead OBU bitstream has no reliable magic bytes and must be decoded by selecting
/// the <see cref="Av1Decoder"/> explicitly.
/// </remarks>
public sealed class Av1ImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc />
    public int HeaderSize => 12;

    /// <inheritdoc />
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? Av1Format.Instance : null;
        return format != null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= this.HeaderSize
        && header[..4].SequenceEqual(IvfFileHeader.Signature)
        && header.Slice(8, 4).SequenceEqual(IvfFileHeader.Av1FourCc);
}
