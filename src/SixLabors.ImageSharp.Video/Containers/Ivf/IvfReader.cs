// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;

/// <summary>
/// Reads frames from an IVF container stream.
/// </summary>
internal static class IvfReader
{
    /// <summary>
    /// The size of the per-frame IVF header in bytes (u32 frame size + u64 timestamp).
    /// </summary>
    public const int FrameHeaderSize = 12;

    /// <summary>
    /// Reads and parses the IVF file header from the current position of the stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the IVF container.</param>
    /// <returns>The parsed <see cref="IvfFileHeader"/>.</returns>
    public static IvfFileHeader ReadFileHeader(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));

        Span<byte> buffer = stackalloc byte[IvfFileHeader.Size];
        stream.ReadExactly(buffer);
        return IvfFileHeader.Parse(buffer);
    }

    /// <summary>
    /// Reads the next coded frame from the stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of a frame header.</param>
    /// <param name="timestamp">When this method returns, contains the frame presentation timestamp.</param>
    /// <param name="frame">When this method returns, contains the coded frame bytes.</param>
    /// <returns>
    /// <see langword="true"/> if a frame was read; <see langword="false"/> at the end of the stream.
    /// </returns>
    /// <exception cref="InvalidDataException">The frame header is truncated or invalid.</exception>
    public static bool TryReadFrame(Stream stream, out ulong timestamp, out byte[] frame)
    {
        Guard.NotNull(stream, nameof(stream));

        timestamp = 0;
        frame = [];

        Span<byte> header = stackalloc byte[FrameHeaderSize];
        int read = ReadAtMost(stream, header);
        if (read == 0)
        {
            return false;
        }

        if (read < FrameHeaderSize)
        {
            throw new InvalidDataException("Truncated IVF frame header.");
        }

        uint frameSize = BinaryPrimitives.ReadUInt32LittleEndian(header[..4]);
        timestamp = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(4, 8));

        frame = new byte[frameSize];
        if (frameSize > 0)
        {
            stream.ReadExactly(frame);
        }

        return true;
    }

    private static int ReadAtMost(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
