// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates locating the tile group compressed data through the full header chain of a real,
/// dav1d-decodable AV1 stream (a 64x64 single-tile all-intra clip from aomenc 3.8.2).
/// </summary>
public class ObuTileGroupRealStreamTests
{
    private static readonly byte[] SequencePayload = Convert.FromHexString("00000002afff9b5f3008");

    private static readonly byte[] FramePayload = Convert.FromHexString("1000d00000028800001ff8195e23effcafeea34da6");

    [Fact]
    public void Parse_RealStream_LocatesSingleTile()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);
        Av1BitStreamReader reader = new(FramePayload);
        ObuFrameHeader frameHeader = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);

        // The tile group syntax begins at the byte-aligned end of the uncompressed header.
        int tileGroupStart = (frameHeader.EndBitPosition + 7) >> 3;
        Assert.Equal(9, tileGroupStart);

        ReadOnlySpan<byte> tileGroupData = FramePayload.AsSpan(tileGroupStart);
        ObuTileGroup tileGroup = ObuTileGroup.Parse(tileGroupData, frameHeader);

        Assert.Equal(1, tileGroup.Count);
        Assert.Equal(0, tileGroup.FirstTile);
        Assert.Equal(0, tileGroup.LastTile);

        (int offset, int length) = tileGroup.GetTile(0);
        Assert.Equal(0, offset);
        Assert.Equal(12, length); // the whole remaining payload is the single tile

        // The tile's compressed data must initialise a symbol decoder (range starts at 0x8000).
        byte[] tileBytes = tileGroupData.Slice(offset, length).ToArray();
        Av1SymbolDecoder decoder = new(tileBytes);
        _ = decoder.ReadBool(); // first arithmetic-decoded bit, must not throw
    }
}
