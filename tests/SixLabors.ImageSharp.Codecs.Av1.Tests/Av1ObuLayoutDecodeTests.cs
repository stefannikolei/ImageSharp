// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;
using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the separate FRAME_HEADER + TILE_GROUP OBU layout (specification sections 5.9.1 and
/// 5.11.1): every FRAME OBU of two already dav1d-validated clips is repacked into a standalone
/// frame-header OBU (with spec trailing bits) followed by one or more tile-group OBUs, and the
/// repacked stream must decode to exactly the same planes as the original single-OBU layout.
/// </summary>
public class Av1ObuLayoutDecodeTests
{
    [Fact]
    public void DecodeDisplayFrames_RepackedFrameHeaderAndTileGroup_MatchesOriginal()
    {
        byte[] original = Convert.FromBase64String(Av1DefaultToolsDecodeTests.ClipIvfBase64);
        byte[] repacked = RepackIvf(original, tileGroupsPerFrame: 1);

        List<Av1DisplayFrame> expected = Av1DecoderCore.DecodeDisplayFrames(new MemoryStream(original));
        List<Av1DisplayFrame> actual = Av1DecoderCore.DecodeDisplayFrames(new MemoryStream(repacked));

        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertPlanesEqual(expected[i].Luma, actual[i].Luma, $"frame {i} luma");
            AssertPlanesEqual(expected[i].ChromaU, actual[i].ChromaU, $"frame {i} U");
            AssertPlanesEqual(expected[i].ChromaV, actual[i].ChromaV, $"frame {i} V");
        }
    }

    [Fact]
    public void DecodeAllFrames_RepackedMultipleTileGroups_MatchesOriginal()
    {
        // The 2x2-tile clip lets each frame's four tiles split across two tile-group OBUs.
        byte[] original = Convert.FromBase64String(Av1MultiTileDecodeTests.TwoByTwoClipIvfBase64);
        byte[] repacked = RepackIvf(original, tileGroupsPerFrame: 2);

        List<Av1TileDecoder> expected = Av1DecoderCore.DecodeAllFrames(new MemoryStream(original));
        List<Av1TileDecoder> actual = Av1DecoderCore.DecodeAllFrames(new MemoryStream(repacked));

        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertPlanesEqual(expected[i].Luma, actual[i].Luma, $"frame {i} luma");
            AssertPlanesEqual(expected[i].ChromaU, actual[i].ChromaU, $"frame {i} U");
            AssertPlanesEqual(expected[i].ChromaV, actual[i].ChromaV, $"frame {i} V");
        }
    }

    [Fact]
    public void FrameSource_RepackedClip_MatchesOriginal()
    {
        byte[] original = Convert.FromBase64String(Av1DefaultToolsDecodeTests.ClipIvfBase64);
        byte[] repacked = RepackIvf(original, tileGroupsPerFrame: 1);

        using Av1VideoFrameSource expected = new(new MemoryStream(original));
        using Av1VideoFrameSource actual = new(new MemoryStream(repacked));

        Assert.Equal(expected.FrameCount, actual.FrameCount);
        foreach (int index in new[] { 0, expected.FrameCount / 2, expected.FrameCount - 1 })
        {
            using Image<Rgba32> expectedImage = expected.DecodeFrame<Rgba32>(index, Configuration.Default);
            using Image<Rgba32> actualImage = actual.DecodeFrame<Rgba32>(index, Configuration.Default);

            byte[] expectedPixels = new byte[expectedImage.Width * expectedImage.Height * 4];
            byte[] actualPixels = new byte[expectedPixels.Length];
            expectedImage.CopyPixelDataTo(expectedPixels);
            actualImage.CopyPixelDataTo(actualPixels);
            Assert.True(expectedPixels.AsSpan().SequenceEqual(actualPixels), $"display frame {index}: pixels differ");
        }
    }

    private static void AssertPlanesEqual(Av1Plane expected, Av1Plane actual, string what)
        => Assert.True(expected.Samples.AsSpan().SequenceEqual(actual.Samples), $"{what}: planes differ");

    // Rewrites an IVF stream, splitting every FRAME OBU into a FRAME_HEADER OBU plus the requested
    // number of TILE_GROUP OBUs. Frame headers must be parsed with live decoder state (reference order
    // hints, inherited header state), so the repacker decodes the clip as it walks it.
    private static byte[] RepackIvf(byte[] ivf, int tileGroupsPerFrame)
    {
        using MemoryStream input = new(ivf);
        using MemoryStream output = new();

        // The 32-byte IVF file header is unchanged (same temporal-unit count and dimensions).
        byte[] fileHeader = new byte[32];
        input.ReadExactly(fileHeader);
        output.Write(fileHeader);

        ObuSequenceHeader sequenceHeader = default;
        bool haveSequenceHeader = false;
        Av1ReferenceFrameStore referenceStore = new();

        Span<byte> unitHeader = stackalloc byte[12];
        while (input.Read(unitHeader) == unitHeader.Length)
        {
            int unitSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(unitHeader[..4]);
            byte[] unit = new byte[unitSize];
            input.ReadExactly(unit);

            using MemoryStream repackedUnit = new();
            int offset = 0;
            while (true)
            {
                int obuStart = offset;
                if (!ObuReader.TryRead(unit, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
                {
                    break;
                }

                if (header.Type == ObuType.SequenceHeader)
                {
                    sequenceHeader = ObuSequenceHeader.Parse(payload);
                    haveSequenceHeader = true;
                }

                if (header.Type != ObuType.Frame || !haveSequenceHeader)
                {
                    // Temporal delimiters, sequence headers and show_existing frame headers pass
                    // through unchanged.
                    repackedUnit.Write(unit.AsSpan(obuStart, offset - obuStart));
                    continue;
                }

                // Parse (and decode, to keep the reference state live) the frame, then re-emit it split.
                Av1DecoderCore.PendingFrame pending = Av1DecoderCore.ParseFrameHeader(payload, sequenceHeader, referenceStore);
                ObuFrameHeader frameHeader = pending.Header;
                int tileGroupStart = (frameHeader.EndBitPosition + 7) >> 3;
                ReadOnlySpan<byte> tileGroupData = payload[tileGroupStart..];

                WriteObu(repackedUnit, ObuType.FrameHeader, BuildFrameHeaderPayload(payload, frameHeader.EndBitPosition));
                foreach (byte[] group in BuildTileGroups(tileGroupData, frameHeader, tileGroupsPerFrame))
                {
                    WriteObu(repackedUnit, ObuType.TileGroup, group);
                }

                Av1DecoderCore.AddTileGroup(pending, tileGroupData);
                Assert.True(pending.IsComplete);
                Av1DecoderCore.FinishFrame(pending, sequenceHeader, referenceStore);
            }

            byte[] repackedBytes = repackedUnit.ToArray();
            Span<byte> newUnitHeader = stackalloc byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(newUnitHeader[..4], (uint)repackedBytes.Length);
            unitHeader[4..].CopyTo(newUnitHeader[4..]); // keep the presentation timestamp
            output.Write(newUnitHeader);
            output.Write(repackedBytes);
        }

        return output.ToArray();
    }

    // The frame-header bits followed by trailing_bits(): a one bit then zeros to the byte boundary
    // (inside a FRAME OBU the header is instead padded with plain byte_alignment zeros).
    private static byte[] BuildFrameHeaderPayload(ReadOnlySpan<byte> framePayload, int headerBits)
    {
        int fullBytes = headerBits >> 3;
        int remainder = headerBits & 7;
        if (remainder == 0)
        {
            byte[] aligned = new byte[fullBytes + 1];
            framePayload[..fullBytes].CopyTo(aligned);
            aligned[fullBytes] = 0x80;
            return aligned;
        }

        byte[] result = framePayload[..(fullBytes + 1)].ToArray();
        int keepMask = 0xFF << (8 - remainder);
        result[fullBytes] = (byte)((result[fullBytes] & keepMask) | (0x80 >> remainder));
        return result;
    }

    // Splits the frame's tiles across the requested number of tile-group payloads. A single group
    // copies the original tile-group bytes verbatim; multiple groups re-emit each with
    // tile_start_and_end_present_flag = 1 and its tg_start / tg_end range.
    private static List<byte[]> BuildTileGroups(ReadOnlySpan<byte> tileGroupData, in ObuFrameHeader frameHeader, int groupCount)
    {
        if (groupCount == 1)
        {
            return [tileGroupData.ToArray()];
        }

        ObuTileGroup tiles = ObuTileGroup.Parse(tileGroupData, frameHeader);
        Assert.True(tiles.Count >= groupCount, "not enough tiles to split into the requested groups");

        int tileBits = frameHeader.TileColumnsLog2 + frameHeader.TileRowsLog2;
        List<byte[]> groups = [];
        int nextTile = 0;
        for (int g = 0; g < groupCount; g++)
        {
            int remainingGroups = groupCount - g;
            int remainingTiles = tiles.Count - nextTile;
            int inThisGroup = (remainingTiles + remainingGroups - 1) / remainingGroups;
            int tgStart = nextTile;
            int tgEnd = nextTile + inThisGroup - 1;

            using MemoryStream group = new();

            // tile_start_and_end_present_flag (1) + tg_start + tg_end, then byte_alignment().
            int headerBitCount = 1 + (2 * tileBits);
            long headerValue = (1L << (2 * tileBits)) | ((long)tgStart << tileBits) | (uint)tgEnd;
            int paddedBits = (headerBitCount + 7) & ~7;
            headerValue <<= paddedBits - headerBitCount;
            for (int shift = paddedBits - 8; shift >= 0; shift -= 8)
            {
                group.WriteByte((byte)(headerValue >> shift));
            }

            for (int t = tgStart; t <= tgEnd; t++)
            {
                (int tileOffset, int tileLength) = tiles.GetTile(t);
                if (t != tgEnd)
                {
                    // tile_size_minus_1, little-endian over TileSizeBytes; the group's last tile omits it.
                    Span<byte> size = stackalloc byte[frameHeader.TileSizeBytes];
                    for (int b = 0; b < size.Length; b++)
                    {
                        size[b] = (byte)((tileLength - 1) >> (8 * b));
                    }

                    group.Write(size);
                }

                group.Write(tileGroupData.Slice(tileOffset, tileLength));
            }

            groups.Add(group.ToArray());
            nextTile = tgEnd + 1;
        }

        return groups;
    }

    private static void WriteObu(Stream stream, ObuType type, ReadOnlySpan<byte> payload)
    {
        // forbidden = 0, type, no extension, has_size = 1, reserved = 0.
        stream.WriteByte((byte)(((int)type << 3) | 0x02));

        uint size = (uint)payload.Length;
        do
        {
            byte b = (byte)(size & 0x7F);
            size >>= 7;
            if (size != 0)
            {
                b |= 0x80;
            }

            stream.WriteByte(b);
        }
        while (size != 0);

        stream.Write(payload);
    }
}
