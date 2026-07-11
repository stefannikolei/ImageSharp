// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Verifies that bitstreams using features the decoder does not implement yet are rejected with a
/// clear <see cref="NotSupportedException"/> instead of silently decoding incorrectly.
/// </summary>
public class Av1UnsupportedFeatureGuardTests
{
    // The hand-encoded reduced-still-picture sequence header from Av1TestData
    // (0x18 0x15 0x7F 0xBC 0x00 0x08) with only the colour configuration varied:
    // the colour config starts at bit 36 (high_bitdepth), inside the fifth byte.

    [Fact]
    public void SequenceHeader_TenBit_Parses()
    {
        // high_bitdepth = 1 with seq_profile 0 selects 10-bit, which the decoder supports.
        byte[] payload = [0x18, 0x15, 0x7F, 0xBC, 0x08, 0x08];
        ObuSequenceHeader header = ObuSequenceHeader.Parse(payload);
        Assert.Equal(10, header.BitDepth);
    }

    [Fact]
    public void SequenceHeader_TwelveBit_Parses()
    {
        // seq_profile 2 with high_bitdepth = 1 and twelve_bit = 1 selects 12-bit, which the decoder
        // supports.
        byte[] payload = [0x58, 0x15, 0x7F, 0xBC, 0x0C, 0x61];
        ObuSequenceHeader header = ObuSequenceHeader.Parse(payload);
        Assert.Equal(12, header.BitDepth);
    }

    [Fact]
    public void SequenceHeader_Monochrome_Parses()
    {
        // mono_chrome = 1, which the decoder supports.
        byte[] payload = [0x18, 0x15, 0x7F, 0xBC, 0x04, 0x08];
        ObuSequenceHeader header = ObuSequenceHeader.Parse(payload);
        Assert.Equal(1, header.NumPlanes);
    }

    [Fact]
    public void SequenceHeader_Srgb444_Parses()
    {
        // color_description_present = 1 with BT.709 primaries / sRGB transfer / identity matrix
        // selects 4:4:4 (no subsampling), which the decoder supports.
        byte[] payload = [0x18, 0x15, 0x7F, 0xBC, 0x02, 0x02, 0x1A, 0x00, 0x40];
        ObuSequenceHeader header = ObuSequenceHeader.Parse(payload);
        Assert.Equal(0, header.SubsamplingX);
        Assert.Equal(0, header.SubsamplingY);
    }

    [Fact]
    public void SequenceHeader_Baseline_Parses()
    {
        // The unmodified 8-bit 4:2:0 header the variants above are derived from stays parseable.
        ObuSequenceHeader header = ObuSequenceHeader.Parse(Av1TestData.SequenceHeaderPayload);
        Assert.Equal(8, header.BitDepth);
        Assert.False(header.MonoChrome);
        Assert.Equal(1, header.SubsamplingX);
        Assert.Equal(1, header.SubsamplingY);
    }

    // A real key-frame sequence/frame header pair (from Av1IntraTileDecoderTests); the frame header
    // is mutated per feature to trigger each tile-decoder guard.
    private static readonly byte[] SequencePayload = Convert.FromHexString("00000002afff9b5f3008");

    private static readonly byte[] FramePayload = Convert.FromHexString("1000d00000028800001ff8195e23effcafeea34da6");

    private static (ObuSequenceHeader Sequence, ObuFrameHeader Frame) ParseHeaders()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);
        Av1BitStreamReader reader = new(FramePayload);
        return (sequenceHeader, ObuFrameHeader.ParseIntra(ref reader, sequenceHeader));
    }

    [Fact]
    public void TileDecoder_QuantizerMatrix_Throws()
    {
        (ObuSequenceHeader sequence, ObuFrameHeader frame) = ParseHeaders();
        Assert.Throws<NotSupportedException>(() => new Av1TileDecoder(sequence, frame with { UsingQMatrix = true }));
    }

    [Fact]
    public void TileDecoder_CodedLossless_Throws()
    {
        (ObuSequenceHeader sequence, ObuFrameHeader frame) = ParseHeaders();
        Assert.Throws<NotSupportedException>(() => new Av1TileDecoder(sequence, frame with { CodedLossless = true }));
    }

    [Fact]
    public void EnsureBaseLayer_EnhancementLayer_Throws()
    {
        ObuHeader temporal = new(ObuType.Frame, hasExtension: true, hasSize: true, temporalId: 1, spatialId: 0);
        ObuHeader spatial = new(ObuType.Frame, hasExtension: true, hasSize: true, temporalId: 0, spatialId: 1);
        Assert.Throws<NotSupportedException>(() => Av1DecoderCore.EnsureBaseLayer(temporal));
        Assert.Throws<NotSupportedException>(() => Av1DecoderCore.EnsureBaseLayer(spatial));
    }

    [Fact]
    public void EnsureBaseLayer_BaseLayer_DoesNotThrow()
    {
        Av1DecoderCore.EnsureBaseLayer(new ObuHeader(ObuType.Frame, hasExtension: false, hasSize: true, temporalId: 0, spatialId: 0));
        Av1DecoderCore.EnsureBaseLayer(new ObuHeader(ObuType.Frame, hasExtension: true, hasSize: true, temporalId: 0, spatialId: 0));
    }
}
