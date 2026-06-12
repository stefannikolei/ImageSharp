// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the intra block decode against a dav1d reference trace captured from a real 64x64 single-
/// tile all-intra clip (dav1d 1.4.1, DEBUG_BLOCK_INFO enabled). The superblock decodes as a single
/// BLOCK_64X64 / PARTITION_NONE with DC intra prediction and a TX_64X64 luma transform; dav1d reports
/// the arithmetic-decoder range (and eob) after each syntax element. Reproducing those ranges on the
/// same tile data proves the decode is bit-exact with the reference through to the coefficient reader.
/// </summary>
public class Av1BlockDecodeReferenceTests
{
    private static readonly byte[] TileData = Convert.FromHexString("1ff8195e23effcafeea34da6");

    [Fact]
    public void IntraSuperblock_DecodesBitExactWithDav1d()
    {
        Av1SymbolDecoder decoder = new(TileData);
        Av1ModeInfoCdfContext modeCdf = Av1ModeInfoCdfContext.CreateDefault();

        // 1) partition: the 64x64 block level (index 1), context 0 -> PARTITION_NONE.
        int partition = decoder.ReadSymbol(modeCdf.Partition[1][0]);
        Assert.Equal(0, partition);
        Assert.Equal(40248u, decoder.Range);

        // 2) skip: context 0 -> not skipped.
        int skip = decoder.ReadSymbol(modeCdf.Skip[0]);
        Assert.Equal(0, skip);
        Assert.Equal(38910u, decoder.Range);

        // (cdef_idx reads 0 bits: cdef_bits == 0 in the frame header.)

        // 3) luma intra mode: key-frame y-mode with above/left context 0 -> DC_PRED.
        int yMode = decoder.ReadSymbol(modeCdf.KeyFrameYMode[0][0]);
        Assert.Equal(0, yMode);
        Assert.Equal(37256u, decoder.Range);

        // 4) chroma intra mode: uv-mode (cfl not allowed for 64x64) for luma DC_PRED -> DC_PRED.
        int uvMode = decoder.ReadSymbol(modeCdf.UvMode[0][0]);
        Assert.Equal(0, uvMode);
        Assert.Equal(51506u, decoder.Range);

        // (tx size reads 0 bits: TxMode == TX_MODE_LARGEST forces TX_64X64.)

        // 5) luma coefficients: TX_64X64, DCT_DCT, base_q_idx 160 => quantizer context 3.
        Av1CoefficientCdfContext coeffCdf = Av1CoefficientCdfContext.CreateDefault(3);
        int[] luma = new int[32 * 32];
        int eob = Av1CoefficientReader.ReadCoefficients(
            decoder, coeffCdf, Av1TransformSize.Size64x64, Av1TransformType.DctDct, plane: 0, skipContext: 0, dcSignContext: 0, luma);

        // dav1d: Post-y-cf-blk[tx=4,txtp=0,eob=20]: r=55048
        Assert.Equal(20, eob);
        Assert.Equal(55048u, decoder.Range);

        // 6) chroma U: TX_32X32, DCT_DCT, all-zero (txb_skip set).
        int[] chroma = new int[32 * 32];
        int eobU = Av1CoefficientReader.ReadCoefficients(
            decoder, coeffCdf, Av1TransformSize.Size32x32, Av1TransformType.DctDct, plane: 1, skipContext: 7, dcSignContext: 0, chroma);

        // dav1d: Post-uv-cf-blk[pl=0,tx=3,txtp=0,eob=-1]: r=47196
        Assert.Equal(Av1CoefficientReader.AllZero, eobU);
        Assert.Equal(47196u, decoder.Range);

        // 7) chroma V: TX_32X32, DCT_DCT, all-zero.
        int eobV = Av1CoefficientReader.ReadCoefficients(
            decoder, coeffCdf, Av1TransformSize.Size32x32, Av1TransformType.DctDct, plane: 2, skipContext: 7, dcSignContext: 0, chroma);

        // dav1d: Post-uv-cf-blk[pl=1,tx=3,txtp=0,eob=-1]: r=40760
        Assert.Equal(Av1CoefficientReader.AllZero, eobV);
        Assert.Equal(40760u, decoder.Range);
    }
}
