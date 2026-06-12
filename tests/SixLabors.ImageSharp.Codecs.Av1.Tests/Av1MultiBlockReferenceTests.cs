// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the multi-block intra decode prologue (partition split, then the first 32x32 block's
/// mode info, filter-intra flag and transform-size selection) against a dav1d reference trace from a
/// real 128x128 all-intra clip that uses PARTITION_SPLIT and TX_MODE_SELECT.
/// </summary>
public class Av1MultiBlockReferenceTests
{
    private static readonly byte[] SequencePayload = Convert.FromHexString("1819bfff6c02");

    private static readonly byte[] FramePayload = Convert.FromHexString(
        "1320000016000010b56bc38e978a0c0750ae75c7d26176605ce99456713e51845b945b1eb9bed91483640cab35294167d12477847d589cb9e653b78549566f0b5732c2e3f6b6a91bb0863644dbe492724bb2de3b336e758ef6d6e087006e8b2770");

    [Fact]
    public void DecodePrologue_RealStream_MatchesDav1dRanges()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);
        Assert.Equal(128, sequenceHeader.MaxFrameWidth);
        Assert.Equal(128, sequenceHeader.MaxFrameHeight);

        Av1BitStreamReader reader = new(FramePayload);
        ObuFrameHeader frameHeader = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);
        Assert.Equal(2, frameHeader.TxMode); // TX_MODE_SELECT

        int tileGroupStart = (frameHeader.EndBitPosition + 7) >> 3;
        ObuTileGroup tileGroup = ObuTileGroup.Parse(FramePayload.AsSpan(tileGroupStart), frameHeader);
        (int offset, int length) = tileGroup.GetTile(0);
        byte[] tileData = FramePayload.AsSpan(tileGroupStart + offset, length).ToArray();

        Av1SymbolDecoder decoder = new(tileData);
        Av1ModeInfoCdfContext modeCdf = Av1ModeInfoCdfContext.CreateDefault();

        // 64x64 superblock -> PARTITION_SPLIT.
        Assert.Equal(3, decoder.ReadSymbol(modeCdf.Partition[1][0]));
        Assert.Equal(51744u, decoder.Range);

        // First 32x32 sub-block -> PARTITION_NONE.
        Assert.Equal(0, decoder.ReadSymbol(modeCdf.Partition[2][0]));
        Assert.Equal(58370u, decoder.Range);

        // skip = 0.
        Assert.Equal(0, decoder.ReadSymbol(modeCdf.Skip[0]));
        Assert.Equal(56428u, decoder.Range);

        // luma DC_PRED.
        Assert.Equal(0, decoder.ReadSymbol(modeCdf.KeyFrameYMode[0][0]));
        Assert.Equal(53800u, decoder.Range);

        // chroma DC_PRED (cfl allowed for 32x32 -> the cfl variant of the uv-mode CDF).
        Assert.Equal(0, decoder.ReadSymbol(modeCdf.UvMode[1][0]));
        Assert.Equal(34206u, decoder.Range);

        // use_filter_intra = 0 for the 32x32 block.
        Assert.Equal(0, decoder.ReadSymbol(modeCdf.UseFilterIntra[(int)Av1BlockSize.Block32x32]));
        Assert.Equal(46858u, decoder.Range);

        // tx depth for a 32x32 block (max transform TX_32X32, context 0) -> TX_16X16 (depth 1).
        Assert.Equal(1, decoder.ReadSymbol(modeCdf.TransformDepth[2][0]));
        Assert.Equal(51296u, decoder.Range);
    }
}
