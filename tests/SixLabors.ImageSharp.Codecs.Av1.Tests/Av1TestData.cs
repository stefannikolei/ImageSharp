// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Builds small, hand-encoded AV1/IVF byte buffers used across the unit tests.
/// </summary>
internal static class Av1TestData
{
    /// <summary>The frame width encoded by <see cref="SequenceHeaderPayload"/>.</summary>
    public const int ExpectedWidth = 64;

    /// <summary>The frame height encoded by <see cref="SequenceHeaderPayload"/>.</summary>
    public const int ExpectedHeight = 48;

    /// <summary>
    /// A hand-encoded reduced-still-picture sequence-header payload:
    /// <c>seq_profile=0, still_picture=1, reduced_still_picture_header=1</c>,
    /// <c>frame_width_bits_minus_1=5, frame_height_bits_minus_1=5</c>,
    /// <c>max_frame_width_minus_1=63 (=> 64), max_frame_height_minus_1=47 (=> 48)</c>,
    /// followed by the coding-tool flags (all disabled), an 8-bit 4:2:0 colour configuration and the
    /// trailing bits, forming a complete sequence header.
    /// </summary>
    public static byte[] SequenceHeaderPayload => [0x18, 0x15, 0x7F, 0xBC, 0x00, 0x08];

    /// <summary>
    /// Wraps <see cref="SequenceHeaderPayload"/> in a sequence-header OBU with an explicit size.
    /// </summary>
    /// <returns>The OBU bytes.</returns>
    public static byte[] SequenceHeaderObu()
    {
        byte[] payload = SequenceHeaderPayload;

        // 0x0A: forbidden=0, type=1 (sequence header), extension=0, has_size=1, reserved=0.
        byte[] obu = new byte[2 + payload.Length];
        obu[0] = 0x0A;
        obu[1] = (byte)payload.Length; // leb128 size (single byte for small payloads).
        payload.CopyTo(obu, 2);
        return obu;
    }

    /// <summary>
    /// Builds a temporal-delimiter OBU (type 2) with a zero-length payload.
    /// </summary>
    /// <returns>The OBU bytes.</returns>
    public static byte[] TemporalDelimiterObu() => [0x12, 0x00];

    /// <summary>
    /// Builds the coded data of a single temporal unit: a temporal delimiter followed by a
    /// sequence header OBU.
    /// </summary>
    /// <returns>The frame payload bytes.</returns>
    public static byte[] FrameData()
    {
        byte[] td = TemporalDelimiterObu();
        byte[] seq = SequenceHeaderObu();
        byte[] data = new byte[td.Length + seq.Length];
        td.CopyTo(data, 0);
        seq.CopyTo(data, td.Length);
        return data;
    }

    /// <summary>
    /// Builds a complete one-frame AV1/IVF file.
    /// </summary>
    /// <returns>The IVF file bytes.</returns>
    public static byte[] IvfFile()
    {
        byte[] frame = FrameData();
        using MemoryStream ms = new();

        ms.Write("DKIF"u8);
        WriteU16(ms, 0);   // version
        WriteU16(ms, 32);  // header length
        ms.Write("AV01"u8);
        WriteU16(ms, ExpectedWidth);
        WriteU16(ms, ExpectedHeight);
        WriteU32(ms, 30);  // frame-rate numerator
        WriteU32(ms, 1);   // frame-rate denominator
        WriteU32(ms, 1);   // frame count
        WriteU32(ms, 0);   // reserved

        WriteU32(ms, (uint)frame.Length); // frame size
        WriteU64(ms, 0);                  // timestamp
        ms.Write(frame);

        return ms.ToArray();
    }

    private static void WriteU16(MemoryStream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)value);
        stream.Write(buffer);
    }

    private static void WriteU32(MemoryStream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteU64(MemoryStream stream, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }
}
