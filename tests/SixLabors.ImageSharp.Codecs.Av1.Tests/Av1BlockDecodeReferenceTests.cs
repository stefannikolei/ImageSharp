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

        // 1) partition: CDF9(20137,...) for the 64x64 split, ctx 0 -> PARTITION_NONE.
        int partition = decoder.ReadSymbol(InverseCdf(20137, 21547, 23078, 29566, 29837, 30261, 30524, 30892, 31724));
        Assert.Equal(0, partition);
        Assert.Equal(40248u, decoder.Range);

        // 2) skip: m.skip ctx 0 = CDF1(31671) -> not skipped.
        int skip = decoder.ReadSymbol(InverseCdf(31671));
        Assert.Equal(0, skip);
        Assert.Equal(38910u, decoder.Range);

        // (cdef_idx reads 0 bits: cdef_bits == 0 in the frame header.)

        // 3) luma intra mode: kfym[0][0] = CDF12(15588,...) -> DC_PRED.
        int yMode = decoder.ReadSymbol(InverseCdf(15588, 17027, 19338, 20218, 20682, 21110, 21825, 23244, 24189, 28165, 29093, 30466));
        Assert.Equal(0, yMode);
        Assert.Equal(37256u, decoder.Range);

        // 4) chroma intra mode: uv_mode[cfl=0][DC_PRED] = CDF12(22631,...) -> DC_PRED.
        int uvMode = decoder.ReadSymbol(InverseCdf(22631, 24152, 25378, 25661, 25986, 26520, 27055, 27923, 28244, 30059, 30941, 31961));
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

    private static ushort[] InverseCdf(params int[] cumulative)
    {
        ushort[] cdf = new ushort[cumulative.Length + 2];
        for (int i = 0; i < cumulative.Length; i++)
        {
            cdf[i] = (ushort)(32768 - cumulative[i]);
        }

        return cdf;
    }
}
