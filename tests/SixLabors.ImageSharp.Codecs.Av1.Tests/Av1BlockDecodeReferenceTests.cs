// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Prediction;
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

    [Fact]
    public void IntraSuperblock_ReconstructsAgainstDav1d()
    {
        Av1SymbolDecoder decoder = new(TileData);
        Av1ModeInfoCdfContext modeCdf = Av1ModeInfoCdfContext.CreateDefault();

        decoder.ReadSymbol(modeCdf.Partition[1][0]); // partition
        decoder.ReadSymbol(modeCdf.Skip[0]);         // skip
        decoder.ReadSymbol(modeCdf.KeyFrameYMode[0][0]); // luma mode (DC_PRED)
        decoder.ReadSymbol(modeCdf.UvMode[0][0]);    // chroma mode (DC_PRED)

        Av1CoefficientCdfContext coeffCdf = Av1CoefficientCdfContext.CreateDefault(3);
        int[] levels = new int[32 * 32];
        Av1CoefficientReader.ReadCoefficients(
            decoder, coeffCdf, Av1TransformSize.Size64x64, Av1TransformType.DctDct, 0, 0, 0, levels);

        // Dequantize into a 64x64 row-major buffer; coded coefficients fill the top-left 32x32.
        const int baseQ = 160;
        int[] coeff = new int[64 * 64];
        for (int rc = 0; rc < levels.Length; rc++)
        {
            if (levels[rc] != 0)
            {
                coeff[((rc >> 5) * 64) + (rc & 31)] =
                    Av1QuantizationLookup.Dequantize(levels[rc], rc == 0, baseQ, 8, Av1TransformSize.Size64x64);
            }
        }

        int[] residual = new int[64 * 64];
        Av1InverseTransform2d.Reconstruct(Av1TransformType.DctDct, Av1TransformSize.Size64x64, coeff, residual, 8);

        // DC_PRED with no neighbours predicts 128 for every sample; reconstruct = clip(128 + residual).
        byte[] luma = new byte[64 * 64];
        for (int i = 0; i < luma.Length; i++)
        {
            luma[i] = (byte)Math.Clamp(128 + residual[i], 0, 255);
        }

        byte[] reference = Convert.FromBase64String(Dav1dLumaBase64);

        // The decode, dequantization, inverse transform and DC prediction reproduce dav1d's luma to
        // within a single level. The residual is in fact bit-exact with dav1d's inverse transform (the
        // dequantized coefficients fed through dav1d's own inv_dct64_1d produce an identical residual);
        // the remaining off-by-one versus the final decoded frame is the CDEF post-filter, which this
        // frame enables and which is not applied here. The entropy decode is bit-exact, as the range
        // checks in the companion test confirm.
        int exact = 0;
        for (int i = 0; i < luma.Length; i++)
        {
            Assert.True(Math.Abs(luma[i] - reference[i]) <= 1, $"Luma sample {i}: got {luma[i]}, dav1d {reference[i]}.");
            if (luma[i] == reference[i])
            {
                exact++;
            }
        }

        Assert.True(exact >= luma.Length * 95 / 100, $"Only {exact}/{luma.Length} luma samples matched exactly.");

        // The all-zero chroma blocks are DC_PRED with no residual, so they reconstruct to exactly 128.
        ushort[] chromaPrediction = new ushort[32 * 32];
        Av1IntraPrediction.Dc128Predict(chromaPrediction, 32, 32, 32, 8);
        Assert.All(chromaPrediction, sample => Assert.Equal(128, sample));
    }

    // dav1d 1.4.1 reconstructed luma (64x64, 8-bit) for the first frame of the reference clip.
    private const string Dav1dLumaBase64 =
        "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IyQlJicpKissLS4vMDExMjM0NTY4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJUVVVWV1hYWVkfHx8gISEiIyQlJicpKissLS4vMDEyMjM0NTY4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJUVVZXV1hZWVpaICAhISIiIyQlJicpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJUVVZXWFlZWlpbWyEhIiIjJCQlJicpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkRFRkdISElKS0xNTk9QUVJUVVZXWFlaWltbXFwiIiMkJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkRFRkdISUpLS0xNTk9QUVJUVVZXWFlaW1tcXV1dIyQkJSUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkRFRkdISUpLTExNTk9QUVJUVVZXWFlaW1xdXV5eXiUlJSYmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkRFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl5fX18mJiYnJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkRFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9fYGBgJycnKCgpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYGFhYSgoKCkpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWFiYmMpKSkqKissLS4vMDEyMzQ1Nzc4OTo7PD0+P0BBQkNERkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJiY2NkKioqKyssLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJjY2RkZSorKywsLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJjZGRlZWUrLCwtLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSEpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYWJjZGVlZmZmLC0tLi4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKTE1OT1BRUlNUVVZXWFlZW1xdXl9gYWJjZGVmZmdnZy0uLi8vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS01OT1BRUlNUVVZXWFlZW1xdXl9gYWJjZGVmZ2doaGguLi8vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNT1BRUlNUVVZXWFlZWltcXl9gYWJjZGVmZ2hoaWlpLy8wMTEyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNT1BRUlNUVVZXWFlZWltcXV9gYWJjZGVmZ2hpaWpqajAwMTIyMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUlNUVVZXWFlaWltcXV5fYGJjZGVmZ2hpampra2sxMTIzMzQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUlNUVVZXWFlaW1tcXV5fYGJjZGVmZ2hpamtrbGxsMjMzNDQ1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVNUVVZXWFlaW1tcXV5fYGJjZGVmZ2hpamtsbG1tbTM0NDU1Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVNUVVZXWFlaW1xdXl9fYGFjZGVmZ2hpamtsbW1ubm40NTU2Njc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJUVVZXWFlaW1xdXl9gYGJjZGVmZ2hpamtsbW5ub29vNjY2Nzc4OTo7PD0+P0BBQkNERUZHSElKS0xNTk9QUVJUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW5vb3BwcDc3Nzg4OTo7PD0+P0BBQkNERkZHSElKS0xNTk9QUVJUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW5vcHBxcXI4ODg5OTo7PD0+P0BBQkRFRkdISUpLS0xNTk9QUVNUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW9vcHFxcnNzOTk5Ojs7PD0+P0BBQkRFRkdISUpLTE1NTk9QUVNUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW9wcXFyc3N0dDo6Ozs8PD0+P0BBQkRFRkdISUpLTE1OTk9QUVNUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW9wcXJyc3R0dXU6Ozw8PT0+P0BBQkNFRkdISUpLTE1OT09QUVJUVVZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW5wcXJzdHR1dXZ2Ozw9PT4+P0BBQkNERkdISUpLTE1OT09QUVJTVFZXWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW5vcXJzdHR1dnZ3dz09PT4+P0BBQkNERUZISUpLTE1OT1BQUVJTVFVWWFlaW1xdXl9gYWJjZGVmZ2hpamtsbW5vcHFzdHR1dnd3eHg9Pj4/P0BBQkNERUZHSElKS0xNTk9QUVJTVFVWV1hZWltdXl9gYWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd3eHh4Pj4/P0BBQUJDREVGSElKS0xNTk9QUVJTU1RVVlhZWltcXV5fYGFiY2RlZmdoaWprbG1ub3Bxc3R1dnd3eHh5eT8/P0BAQUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVpbXV5fYGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHh5eXk/Pz9AQUFCQ0RFRkdJSktMTU5PUFFSU1NUVVZYWVpbXF1eX2BhYmNkZWZnaGlqa2xtbm9wcXJ0dXZ3d3h5eXp6Pz9AQEFCQ0RERUdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl9gYmNkZWZmZ2hpamtsbW5vcHJzdHV2d3h4eXl6eg==";
}
