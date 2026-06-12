// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Transform;

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

        // The 32x32 luma block holds a 2x2 grid of TX_16X16 transforms decoded in raster order. Each
        // codes a per-block transform type (txtp_intra) and uses a txb_skip / dc-sign context derived
        // from the neighbouring coefficient-level bytes.
        Av1CoefficientCdfContext coeffCdf = Av1CoefficientCdfContext.CreateDefault(GetQuantizerContext(frameHeader.BaseQIndex));
        byte[] aboveLuma = NewLevelContext(16);
        byte[] leftLuma = NewLevelContext(16);
        uint[] expectedLumaRanges = [49416, 43016, 41488, 50628];
        int[][] positions = [[0, 0], [4, 0], [0, 4], [4, 4]];
        int[] levels = new int[16 * 16];
        for (int i = 0; i < 4; i++)
        {
            int bx4 = positions[i][0];
            int by4 = positions[i][1];
            int skipContext = LumaSkipContext(aboveLuma, leftLuma, bx4, by4, 4, 4);
            int dcSignContext = DcSignContext(aboveLuma, leftLuma, bx4, by4, 4, 4);

            Array.Clear(levels);
            int eob = Av1CoefficientReader.ReadCoefficients(
                decoder, coeffCdf, Av1TransformSize.Size16x16, Av1TransformType.DctDct, 0, skipContext, dcSignContext, levels, modeCdf, 0, false);
            Assert.Equal(expectedLumaRanges[i], decoder.Range);

            byte resContext = LevelContext(levels, eob);
            WriteContext(aboveLuma, bx4, 4, resContext);
            WriteContext(leftLuma, by4, 4, resContext);
        }

        // Chroma: a single TX_16X16 per plane (32x32 luma -> 16x16 chroma in 4:2:0), both all-zero.
        int eobU = Av1CoefficientReader.ReadCoefficients(
            decoder, coeffCdf, Av1TransformSize.Size16x16, Av1TransformType.DctDct, 1, 7, 0, levels);
        Assert.Equal(Av1CoefficientReader.AllZero, eobU);
        Assert.Equal(45117u, decoder.Range);

        int eobV = Av1CoefficientReader.ReadCoefficients(
            decoder, coeffCdf, Av1TransformSize.Size16x16, Av1TransformType.DctDct, 2, 7, 0, levels);
        Assert.Equal(Av1CoefficientReader.AllZero, eobV);
        Assert.Equal(40572u, decoder.Range);
    }

    private static readonly int[][] SkipContextTable =
    [
        [1, 2, 2, 2, 3],
        [2, 4, 4, 4, 5],
        [2, 4, 4, 4, 5],
        [2, 4, 4, 4, 5],
        [3, 5, 5, 5, 6],
    ];

    private static byte[] NewLevelContext(int length)
    {
        byte[] context = new byte[length];
        Array.Fill(context, (byte)0x40);
        return context;
    }

    private static int LumaSkipContext(byte[] above, byte[] left, int bx4, int by4, int txWidth4, int txHeight4)
    {
        int la = 0;
        for (int i = 0; i < txWidth4; i++)
        {
            la |= above[bx4 + i];
        }

        int ll = 0;
        for (int i = 0; i < txHeight4; i++)
        {
            ll |= left[by4 + i];
        }

        return SkipContextTable[Math.Min(la & 0x3F, 4)][Math.Min(ll & 0x3F, 4)];
    }

    private static int DcSignContext(byte[] above, byte[] left, int bx4, int by4, int txWidth4, int txHeight4)
    {
        int sum = 0;
        for (int i = 0; i < txWidth4; i++)
        {
            sum += above[bx4 + i] >> 6;
        }

        for (int i = 0; i < txHeight4; i++)
        {
            sum += left[by4 + i] >> 6;
        }

        int s = sum - txWidth4 - txHeight4;
        return s < 0 ? 1 : s > 0 ? 2 : 0;
    }

    private static byte LevelContext(int[] levels, int eob)
    {
        if (eob == Av1CoefficientReader.AllZero)
        {
            return 0x40;
        }

        int culLevel = 0;
        for (int i = 0; i < levels.Length; i++)
        {
            culLevel += Math.Abs(levels[i]);
        }

        int dcSignLevel = levels[0] == 0 ? 0x40 : levels[0] > 0 ? 0x80 : 0x00;
        return (byte)(Math.Min(culLevel, 63) | dcSignLevel);
    }

    private static void WriteContext(byte[] context, int start, int count, byte value)
    {
        for (int i = 0; i < count; i++)
        {
            context[start + i] = value;
        }
    }

    private static int GetQuantizerContext(int baseQIndex)
        => baseQIndex <= 20 ? 0 : baseQIndex <= 60 ? 1 : baseQIndex <= 120 ? 2 : 3;
}
