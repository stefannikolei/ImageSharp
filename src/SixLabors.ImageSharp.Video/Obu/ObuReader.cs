// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// Parses the framing of Open Bitstream Units (OBUs) from a byte buffer, following the AV1
/// specification, section 5.2 (<c>open_bitstream_unit</c>) and the <c>leb128</c> primitive in
/// section 4.10.5.
/// </summary>
internal static class ObuReader
{
    /// <summary>
    /// The maximum number of bytes a <c>leb128</c> value may span.
    /// </summary>
    public const int MaxLeb128Bytes = 8;

    /// <summary>
    /// Reads a single OBU starting at <paramref name="offset"/> within <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The buffer containing one or more OBUs.</param>
    /// <param name="offset">
    /// The offset to start reading from. On success it is advanced past the OBU payload.
    /// </param>
    /// <param name="header">When this method returns, contains the parsed OBU header.</param>
    /// <param name="payload">When this method returns, contains the OBU payload bytes.</param>
    /// <returns>
    /// <see langword="true"/> if an OBU was read; <see langword="false"/> if the buffer end was
    /// reached before any data could be read.
    /// </returns>
    /// <exception cref="InvalidDataException">The OBU framing is malformed.</exception>
    public static bool TryRead(
        ReadOnlySpan<byte> data,
        ref int offset,
        out ObuHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (offset >= data.Length)
        {
            return false;
        }

        header = ReadHeader(data, ref offset);

        long size;
        if (header.HasSize)
        {
            size = ReadLeb128(data, ref offset);
        }
        else
        {
            // Without an explicit size field the OBU extends to the end of the buffer.
            size = data.Length - offset;
        }

        if (size < 0 || offset + size > data.Length)
        {
            throw new InvalidDataException("AV1 OBU size exceeds the available data.");
        }

        payload = data.Slice(offset, (int)size);
        offset += (int)size;
        return true;
    }

    /// <summary>
    /// Reads the <c>obu_header</c> (and optional <c>obu_extension_header</c>).
    /// </summary>
    /// <param name="data">The buffer to read from.</param>
    /// <param name="offset">The offset to read from, advanced past the header.</param>
    /// <returns>The parsed header.</returns>
    public static ObuHeader ReadHeader(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset >= data.Length)
        {
            throw new InvalidDataException("Unexpected end of data while reading an AV1 OBU header.");
        }

        byte b = data[offset++];

        bool forbidden = (b & 0x80) != 0;
        if (forbidden)
        {
            throw new InvalidDataException("AV1 OBU forbidden bit is set.");
        }

        ObuType type = (ObuType)((b >> 3) & 0x0F);
        bool hasExtension = (b & 0x04) != 0;
        bool hasSize = (b & 0x02) != 0;

        int temporalId = 0;
        int spatialId = 0;
        if (hasExtension)
        {
            if (offset >= data.Length)
            {
                throw new InvalidDataException("Unexpected end of data while reading an AV1 OBU extension header.");
            }

            byte ext = data[offset++];
            temporalId = (ext >> 5) & 0x07;
            spatialId = (ext >> 3) & 0x03;
        }

        return new ObuHeader(type, hasExtension, hasSize, temporalId, spatialId);
    }

    /// <summary>
    /// Reads a little-endian base-128 variable length integer (<c>leb128</c>).
    /// </summary>
    /// <param name="data">The buffer to read from.</param>
    /// <param name="offset">The offset to read from, advanced past the value.</param>
    /// <returns>The decoded value.</returns>
    public static long ReadLeb128(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong value = 0;
        for (int i = 0; i < MaxLeb128Bytes; i++)
        {
            if (offset >= data.Length)
            {
                throw new InvalidDataException("Unexpected end of data while reading an AV1 leb128 value.");
            }

            byte b = data[offset++];
            value |= (ulong)(b & 0x7F) << (i * 7);
            if ((b & 0x80) == 0)
            {
                return (long)value;
            }
        }

        throw new InvalidDataException("AV1 leb128 value is longer than the permitted 8 bytes.");
    }
}
