// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Shared parsing logic for the AV1 decoder. At this stage it covers container demuxing and
/// sequence-header parsing only; full frame reconstruction is tracked in docs/av1-codec-roadmap.md.
/// </summary>
internal static class Av1DecoderCore
{
    /// <summary>
    /// Reads the coded frame dimensions from an IVF-wrapped AV1 stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the IVF container.</param>
    /// <returns>The frame <see cref="Size"/> in pixels.</returns>
    /// <exception cref="InvalidDataException">The stream is not a valid AV1/IVF bitstream.</exception>
    public static Size ReadDimensions(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));

        IvfFileHeader fileHeader = IvfReader.ReadFileHeader(stream);
        if (!fileHeader.IsAv1)
        {
            throw new InvalidDataException($"Unsupported IVF codec FourCC '{fileHeader.FourCc}', expected AV1.");
        }

        // Prefer the authoritative dimensions coded in the AV1 sequence header, falling back to the
        // values declared by the container if no sequence header is found in the first frame.
        if (TryReadSequenceHeaderDimensions(stream, out Size size))
        {
            return size;
        }

        return new Size(fileHeader.Width, fileHeader.Height);
    }

    private static bool TryReadSequenceHeaderDimensions(Stream stream, out Size size)
    {
        size = default;
        if (!IvfReader.TryReadFrame(stream, out _, out byte[] frame))
        {
            return false;
        }

        int offset = 0;
        while (ObuReader.TryRead(frame, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
        {
            if (header.Type == ObuType.SequenceHeader)
            {
                ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(payload);
                size = new Size(sequenceHeader.MaxFrameWidth, sequenceHeader.MaxFrameHeight);
                return true;
            }
        }

        return false;
    }
}
