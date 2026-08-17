// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Reads the quantized transform coefficients for a single transform block from the symbol decoder
/// (specification section 5.11.39, <c>coeffs</c>). This is a faithful port of dav1d's
/// <c>decode_coefs</c> level loop, covering the 2D, horizontal and vertical transform classes.
/// </summary>
/// <remarks>
/// The reader produces signed coefficient levels (the magnitude token combined with its sign) indexed
/// by transform-block raster position; dequantization is applied separately. Neighbour-derived skip
/// and dc-sign contexts are supplied by the caller, keeping the reader independent of block state.
/// </remarks>
internal static class Av1CoefficientReader
{
    /// <summary>Sentinel returned when the whole block is signalled as all-zero (txb_skip).</summary>
    public const int AllZero = -1;

    // The txb_skip / coeff context index (t_dim->ctx), per Av1TransformSize enum order.
    private static readonly int[] TxSizeContextTable =
        [0, 1, 2, 3, 4, 1, 1, 2, 2, 3, 3, 4, 4, 1, 1, 2, 2, 3, 3];

    // The intra transform-type sets (dav1d_tx_types_per_set), mapped to Av1TransformType.
    private static readonly Av1TransformType[] IntraTransformSet2 =
    [
        Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.AdstAdst,
        Av1TransformType.AdstDct, Av1TransformType.DctAdst,
    ];

    private static readonly Av1TransformType[] IntraTransformSet1 =
    [
        Av1TransformType.Identity, Av1TransformType.DctDct, Av1TransformType.VerticalDct,
        Av1TransformType.HorizontalDct, Av1TransformType.AdstAdst, Av1TransformType.AdstDct,
        Av1TransformType.DctAdst,
    ];

    /// <summary>Gets the txb_skip / coefficient CDF context (t_dim->ctx) for the given size.</summary>
    public static int GetTransformSizeContext(Av1TransformSize transformSize) => TxSizeContextTable[(int)transformSize];

    /// <summary>
    /// Reads the per-block transform type for an intra luma block (specification section 5.11.40,
    /// <c>transform_type</c>), assuming a non-lossless block with a non-zero quantizer.
    /// </summary>
    private static Av1TransformType ReadIntraTransformType(
        Av1SymbolDecoder decoder,
        Av1ModeInfoCdfContext modeCdf,
        Av1TransformSize transformSize,
        int intraLumaMode,
        bool reducedTransformSet)
    {
        int lw = transformSize.GetWidthLog2() - 2;
        int lh = transformSize.GetHeightLog2() - 2;
        int maxTx = Math.Max(lw, lh);
        int minTx = Math.Min(lw, lh);

        // For transforms of 32x32 and larger the intra transform type is implicitly DCT_DCT.
        if (maxTx >= 3)
        {
            return Av1TransformType.DctDct;
        }

        if (reducedTransformSet || minTx == 2)
        {
            int index = decoder.ReadSymbol(modeCdf.TransformTypeIntra2[minTx][intraLumaMode]);
            return IntraTransformSet2[index];
        }

        int index1 = decoder.ReadSymbol(modeCdf.TransformTypeIntra1[minTx][intraLumaMode]);
        return IntraTransformSet1[index1];
    }

    /// <summary>
    /// Reads the coefficients of one transform block into <paramref name="coefficients"/> as signed
    /// levels, returning the index of the last non-zero coefficient (the "eob"), or
    /// <see cref="AllZero"/> when the block is signalled as all-zero.
    /// </summary>
    /// <param name="decoder">The symbol decoder.</param>
    /// <param name="cdf">The adaptive coefficient CDF context for the tile.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The transform type (determines the transform class).</param>
    /// <param name="plane">The plane index (0 = luma, 1/2 = chroma).</param>
    /// <param name="skipContext">The neighbour-derived txb_skip context.</param>
    /// <param name="dcSignContext">The neighbour-derived dc_sign context.</param>
    /// <param name="coefficients">Receives the signed coefficient levels, indexed by raster position; must be cleared by the caller.</param>
    /// <param name="modeCdf">The mode-info CDF context used to decode the intra transform type; pass <see langword="null"/> to use <paramref name="transformType"/> directly.</param>
    /// <param name="intraLumaMode">The intra luma prediction mode, used to select the transform-type CDF.</param>
    /// <param name="reducedTransformSet">Whether the reduced transform-type set is in use.</param>
    /// <returns>The eob index, or <see cref="AllZero"/>.</returns>
    public static int ReadCoefficients(
        Av1SymbolDecoder decoder,
        Av1CoefficientCdfContext cdf,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int plane,
        int skipContext,
        int dcSignContext,
        Span<int> coefficients,
        Av1ModeInfoCdfContext? modeCdf = null,
        int intraLumaMode = 0,
        bool reducedTransformSet = false)
        => ReadCoefficients(decoder, cdf, transformSize, transformType, plane, skipContext, dcSignContext, coefficients, modeCdf, intraLumaMode, reducedTransformSet, out _);

    /// <summary>
    /// As <see cref="ReadCoefficients(Av1SymbolDecoder, Av1CoefficientCdfContext, Av1TransformSize, Av1TransformType, int, int, int, Span{int}, Av1ModeInfoCdfContext, int, bool)"/>,
    /// additionally returning the transform type actually used (which may be decoded from the stream).
    /// </summary>
    /// <param name="decoder">The symbol decoder.</param>
    /// <param name="cdf">The coefficient CDF context.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformType">The fallback transform type.</param>
    /// <param name="plane">The plane index.</param>
    /// <param name="skipContext">The txb-skip context.</param>
    /// <param name="dcSignContext">The dc-sign context.</param>
    /// <param name="coefficients">The decoded coefficient levels.</param>
    /// <param name="modeCdf">The mode-info CDFs, or null to use <paramref name="transformType"/>.</param>
    /// <param name="intraLumaMode">The intra direction for the transform-type CDF.</param>
    /// <param name="reducedTransformSet">Whether the reduced transform set is in use.</param>
    /// <param name="decodedType">Receives the transform type used.</param>
    /// <returns>The eob index, or <see cref="AllZero"/>.</returns>
    public static int ReadCoefficients(
        Av1SymbolDecoder decoder,
        Av1CoefficientCdfContext cdf,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int plane,
        int skipContext,
        int dcSignContext,
        Span<int> coefficients,
        Av1ModeInfoCdfContext? modeCdf,
        int intraLumaMode,
        bool reducedTransformSet,
        out Av1TransformType decodedType,
        Func<Av1TransformSize, Av1TransformType>? interTransformTypeReader = null)
    {
        int chroma = plane != 0 ? 1 : 0;
        int txCtx = GetTransformSizeContext(transformSize);

        // txb_skip: does the block carry any non-zero coefficient?
        bool allZero = decoder.ReadSymbol(cdf.Skip[(txCtx * 13) + skipContext]) != 0;
        if (allZero)
        {
            decodedType = transformType;
            return AllZero;
        }

        // The luma transform type is coded after txb_skip: an inter block reads it from the inter
        // transform-type sets (supplied callback); an intra block reads it from the intra sets. Chroma
        // and 64x64 transforms use the supplied type as-is.
        if (plane == 0)
        {
            if (interTransformTypeReader is not null)
            {
                transformType = interTransformTypeReader(transformSize);
            }
            else if (modeCdf is not null)
            {
                transformType = ReadIntraTransformType(decoder, modeCdf, transformSize, intraLumaMode, reducedTransformSet);
            }
        }

        decodedType = transformType;

        int slw = Math.Min(transformSize.GetWidthLog2() - 2, 3);
        int slh = Math.Min(transformSize.GetHeightLog2() - 2, 3);
        int tx2dSizeContext = slw + slh;
        Av1TransformClass txClass = transformType.GetTransformClass();
        int is1d = txClass != Av1TransformClass.TwoDimensional ? 1 : 0;

        int eob = ReadEndOfBlock(decoder, cdf, txCtx, chroma, tx2dSizeContext, is1d);

        int brSet = Math.Min(txCtx, 3);
        Span<ushort[]> baseRange = cdf.BaseRange.AsSpan(((brSet * 2) + chroma) * 21, 21);
        Span<ushort[]> eobBaseToken = cdf.EobBaseToken.AsSpan(((txCtx * 2) + chroma) * 4, 4);

        int rc;
        int dcToken;

        if (eob != 0)
        {
            Span<ushort[]> baseToken = cdf.BaseToken.AsSpan(((txCtx * 2) + chroma) * 41, 41);
            Av1CoefficientGeometry geometry = Av1CoefficientGeometry.Create(transformSize, txClass);
            rc = ReadLevelPass(decoder, eobBaseToken, baseToken, baseRange, coefficients, eob, transformSize, geometry, tx2dSizeContext, out dcToken);
        }
        else
        {
            // dc-only block.
            int tokenBranch = decoder.ReadSymbol(eobBaseToken[0]);
            dcToken = 1 + tokenBranch;
            if (tokenBranch == 2)
            {
                dcToken = Av1CoefficientLevels.ReadHighToken(decoder, baseRange[0]);
            }

            rc = 0;
        }

        ReadSignsAndResiduals(decoder, cdf, coefficients, dcToken, rc, chroma, dcSignContext);
        return eob;
    }

    /// <summary>
    /// Reads the end-of-block position (eob_pt symbol, the high bit and the remaining low bits),
    /// returning the zero-based index of the last non-zero coefficient.
    /// </summary>
    private static int ReadEndOfBlock(Av1SymbolDecoder decoder, Av1CoefficientCdfContext cdf, int txCtx, int chroma, int tx2dSizeContext, int is1d)
    {
        // The eob_bin CDFs for tx2dszctx 0..4 are split by plane and 1D/2D; 5 and 6 only by plane.
        ushort[] eobBinCdf = tx2dSizeContext <= 4
            ? cdf.EobBin[tx2dSizeContext][(chroma * 2) + is1d]
            : cdf.EobBin[tx2dSizeContext][chroma];
        int eob = decoder.ReadSymbol(eobBinCdf);

        if (eob > 1)
        {
            int eobBin = eob - 2;
            ushort[] eobHiBitCdf = cdf.EobHighBit[(((txCtx * 2) + chroma) * 9) + eobBin];
            int eobHiBit = decoder.ReadSymbol(eobHiBitCdf);
            eob = ((eobHiBit | 2) << eobBin) | (int)decoder.ReadLiteral(eobBin);
        }

        return eob;
    }

    /// <summary>
    /// Decodes the magnitude tokens for every coefficient from the eob position down to the DC term,
    /// building the linked list of non-zero positions in <paramref name="coefficients"/>. Returns the
    /// raster position of the lowest-frequency non-zero AC coefficient (or 0), and the DC token.
    /// </summary>
    private static int ReadLevelPass(
        Av1SymbolDecoder decoder,
        Span<ushort[]> eobBaseToken,
        Span<ushort[]> baseToken,
        Span<ushort[]> baseRange,
        Span<int> coefficients,
        int eob,
        Av1TransformSize transformSize,
        Av1CoefficientGeometry geometry,
        int tx2dSizeContext,
        out int dcToken)
    {
        bool is2d = geometry.Is2d;
        ReadOnlySpan<ushort> scan = is2d ? Av1ScanOrder.GetScan(transformSize) : default;
        byte[] levels = new byte[geometry.LevelBufferLength];

        // eob coefficient: its own base token, optionally extended by a high token.
        geometry.DecodePosition(eob, scan, out int x, out int y, out int rc);

        int eobContext = 1 + (eob > (2 << tx2dSizeContext) ? 1 : 0) + (eob > (4 << tx2dSizeContext) ? 1 : 0);
        int eobToken = decoder.ReadSymbol(eobBaseToken[eobContext]);
        int token = eobToken + 1;
        int levelByte = token * 0x41;
        if (eobToken == 2)
        {
            int hiContext = (is2d ? (x | y) > 1 : y != 0) ? 14 : 7;
            token = Av1CoefficientLevels.ReadHighToken(decoder, baseRange[hiContext]);
            levelByte = token + (3 << 6);
        }

        coefficients[rc] = token << 11;
        levels[geometry.LevelIndex(x, y, rc)] = (byte)levelByte;

        // AC coefficients, from just below eob down to (but excluding) the DC term.
        for (int i = eob - 1; i > 0; i--)
        {
            geometry.DecodePosition(i, scan, out x, out y, out int rcI);
            int levelIndex = geometry.LevelIndex(x, y, rcI);

            int context = geometry.GetLowContext(levels, levelIndex, x, y, out int mag);
            int yForHi = is2d ? (y | x) : y;
            token = decoder.ReadSymbol(baseToken[context]);
            if (token == 3)
            {
                mag &= 63;
                int hiContext = ((yForHi > (is2d ? 1 : 0)) ? 14 : 7) + (mag > 12 ? 6 : (mag + 1) >> 1);
                token = Av1CoefficientLevels.ReadHighToken(decoder, baseRange[hiContext]);
                levels[levelIndex] = (byte)(token + (3 << 6));
                coefficients[rcI] = (token << 11) | rc;
                rc = rcI;
            }
            else
            {
                levels[levelIndex] = (byte)(token * 0x41);
                if (token != 0)
                {
                    coefficients[rcI] = (token << 11) | rc;
                    rc = rcI;
                }
            }
        }

        // DC term.
        int dcContext = is2d ? 0 : geometry.GetLowContext(levels, 0, 0, 0, out _);
        dcToken = decoder.ReadSymbol(baseToken[dcContext]);
        if (dcToken == 3)
        {
            int dcMag;
            if (is2d)
            {
                int stride = geometry.Stride;
                dcMag = levels[(0 * stride) + 1] + levels[(1 * stride) + 0] + levels[(1 * stride) + 1];
            }
            else
            {
                geometry.GetLowContext(levels, 0, 0, 0, out dcMag);
            }

            dcMag &= 63;
            int hiContext = dcMag > 12 ? 6 : (dcMag + 1) >> 1;
            dcToken = Av1CoefficientLevels.ReadHighToken(decoder, baseRange[hiContext]);
        }

        return rc;
    }

    /// <summary>
    /// Walks the linked list of non-zero coefficients from the DC term outwards, reading the sign of
    /// each and the Exp-Golomb residual for saturated magnitudes, writing the final signed level.
    /// </summary>
    private static void ReadSignsAndResiduals(
        Av1SymbolDecoder decoder,
        Av1CoefficientCdfContext cdf,
        Span<int> coefficients,
        int dcToken,
        int rc,
        int chroma,
        int dcSignContext)
    {
        if (dcToken != 0)
        {
            int dcSign = decoder.ReadSymbol(cdf.DcSign[(chroma * 3) + dcSignContext]);
            int dcLevel = dcToken;
            if (dcToken == Av1CoefficientLevels.MaxBaseRangeLevel)
            {
                dcLevel = Av1CoefficientLevels.MaxBaseRangeLevel + (int)decoder.ReadGolomb();
            }

            coefficients[0] = dcSign != 0 ? -dcLevel : dcLevel;
        }

        while (rc != 0)
        {
            int sign = decoder.ReadBool();
            int packed = coefficients[rc];
            int token = packed >> 11;
            int next = packed & 0x3ff;
            int level = token;
            if (token == Av1CoefficientLevels.MaxBaseRangeLevel)
            {
                level = Av1CoefficientLevels.MaxBaseRangeLevel + (int)decoder.ReadGolomb();
            }

            coefficients[rc] = sign != 0 ? -level : level;
            rc = next;
        }
    }
}
