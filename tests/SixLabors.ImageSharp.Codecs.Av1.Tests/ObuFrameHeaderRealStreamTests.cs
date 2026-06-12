// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the intra uncompressed-frame-header parse against the frame OBU of a real, dav1d-decodable
/// AV1 stream (a 64x64 all-intra clip from aomenc 3.8.2). The expected values were derived by an
/// independent bit-accurate reference parse of specification section 5.9.
/// </summary>
public class ObuFrameHeaderRealStreamTests
{
    // Sequence header from the same stream (full 8-bit 4:2:0, order_hint_bits=7, cdef/restoration on).
    private static readonly byte[] SequencePayload = Convert.FromHexString("00000002afff9b5f3008");

    // Frame OBU payload: the uncompressed header followed by byte alignment and the tile group.
    private static readonly byte[] FramePayload = Convert.FromHexString("1000d00000028800001ff8195e23effcafeea34da6");

    [Fact]
    public void ParseIntra_RealStream_MatchesReferenceFields()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);
        Av1BitStreamReader reader = new(FramePayload);
        ObuFrameHeader h = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);

        Assert.Equal(Av1FrameType.Key, h.FrameType);
        Assert.True(h.FrameIsIntra);
        Assert.True(h.ShowFrame);
        Assert.False(h.DisableCdfUpdate);
        Assert.False(h.AllowScreenContentTools);
        Assert.False(h.AllowIntraBlockCopy);

        Assert.Equal(64, h.FrameWidth);
        Assert.Equal(64, h.FrameHeight);
        Assert.Equal(64, h.RenderWidth);
        Assert.Equal(64, h.RenderHeight);
        Assert.Equal(16, h.ModeInfoColumns);
        Assert.Equal(16, h.ModeInfoRows);

        Assert.Equal(0, h.TileColumnsLog2);
        Assert.Equal(0, h.TileRowsLog2);

        Assert.Equal(160, h.BaseQIndex);
        Assert.Equal(0, h.DeltaQYDc);
        Assert.Equal(0, h.DeltaQUDc);
        Assert.Equal(0, h.DeltaQUAc);
        Assert.False(h.UsingQMatrix);
        Assert.False(h.SegmentationEnabled);
        Assert.False(h.DeltaQPresent);
        Assert.False(h.CodedLossless);
        Assert.Equal(1, h.TxMode); // TX_MODE_LARGEST
        Assert.False(h.ReducedTxSet);

        // The header occupies exactly 9 bytes; the remaining 12 bytes are the tile group data.
        Assert.Equal(72, h.EndBitPosition);
    }
}
