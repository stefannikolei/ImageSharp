// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// The exact inverse of <see cref="Av1CoefficientReader"/>, used to validate the reader by round-trip.
/// It emits the same symbol sequence the reader consumes, deriving every adaptation context from the
/// shared <see cref="Av1CoefficientGeometry"/> so the encoder and decoder CDFs stay in lock-step.
/// </summary>
internal static class Av1CoefficientWriter
{
    public static void WriteCoefficients(
        Av1SymbolEncoder encoder,
        Av1CoefficientCdfContext cdf,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int plane,
        int skipContext,
        int dcSignContext,
        ReadOnlySpan<int> coefficients)
    {
        int chroma = plane != 0 ? 1 : 0;
        int txCtx = Av1CoefficientReader.GetTransformSizeContext(transformSize);
        Av1TransformClass txClass = transformType.GetTransformClass();
        Av1CoefficientGeometry geometry = Av1CoefficientGeometry.Create(transformSize, txClass);
        bool is2d = geometry.Is2d;
        ReadOnlySpan<ushort> scan = is2d ? Av1ScanOrder.GetScan(transformSize) : default;
        int count = is2d ? scan.Length : coefficients.Length;

        int eob = -1;
        for (int i = count - 1; i >= 0; i--)
        {
            geometry.DecodePosition(i, scan, out _, out _, out int rc);
            if (coefficients[rc] != 0)
            {
                eob = i;
                break;
            }
        }

        bool allZero = eob < 0;
        encoder.WriteSymbol(allZero ? 1 : 0, cdf.Skip[(txCtx * 13) + skipContext]);
        if (allZero)
        {
            return;
        }

        int slw = Math.Min(transformSize.GetWidthLog2() - 2, 3);
        int slh = Math.Min(transformSize.GetHeightLog2() - 2, 3);
        int tx2dSizeContext = slw + slh;
        int is1d = is2d ? 0 : 1;

        WriteEndOfBlock(encoder, cdf, txCtx, chroma, tx2dSizeContext, is1d, eob);

        int brSet = Math.Min(txCtx, 3);
        Span<ushort[]> baseRange = cdf.BaseRange.AsSpan(((brSet * 2) + chroma) * 21, 21);
        Span<ushort[]> eobBaseToken = cdf.EobBaseToken.AsSpan(((txCtx * 2) + chroma) * 4, 4);

        int dcToken;
        if (eob != 0)
        {
            Span<ushort[]> baseToken = cdf.BaseToken.AsSpan(((txCtx * 2) + chroma) * 41, 41);
            dcToken = WriteLevelPass(encoder, eobBaseToken, baseToken, baseRange, coefficients, eob, scan, geometry, tx2dSizeContext);
        }
        else
        {
            int mag = Math.Abs(coefficients[0]);
            int tokenBranch = Math.Min(mag, 3) - 1;
            encoder.WriteSymbol(tokenBranch, eobBaseToken[0]);
            if (tokenBranch == 2)
            {
                WriteHighToken(encoder, Math.Min(mag, Av1CoefficientLevels.MaxBaseRangeLevel), baseRange[0]);
            }

            dcToken = mag;
        }

        WriteSignsAndResiduals(encoder, cdf, coefficients, eob, dcToken, scan, geometry, chroma, dcSignContext);
    }

    private static void WriteEndOfBlock(Av1SymbolEncoder encoder, Av1CoefficientCdfContext cdf, int txCtx, int chroma, int tx2dSizeContext, int is1d, int eob)
    {
        ushort[] eobBinCdf = tx2dSizeContext <= 4
            ? cdf.EobBin[tx2dSizeContext][(chroma * 2) + is1d]
            : cdf.EobBin[tx2dSizeContext][chroma];

        if (eob <= 1)
        {
            encoder.WriteSymbol(eob, eobBinCdf);
            return;
        }

        int eobBin = (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)eob)) - 1; // floor(log2(eob)) - 1
        int symbol = eobBin + 2;
        encoder.WriteSymbol(symbol, eobBinCdf);

        int eobHiBit = (eob >> eobBin) & 1;
        encoder.WriteSymbol(eobHiBit, cdf.EobHighBit[(((txCtx * 2) + chroma) * 9) + eobBin]);

        int low = eob & ((1 << eobBin) - 1);
        encoder.WriteLiteral((uint)low, eobBin);
    }

    private static int WriteLevelPass(
        Av1SymbolEncoder encoder,
        Span<ushort[]> eobBaseToken,
        Span<ushort[]> baseToken,
        Span<ushort[]> baseRange,
        ReadOnlySpan<int> coefficients,
        int eob,
        ReadOnlySpan<ushort> scan,
        Av1CoefficientGeometry geometry,
        int tx2dSizeContext)
    {
        bool is2d = geometry.Is2d;
        byte[] levels = new byte[geometry.LevelBufferLength];

        // eob coefficient.
        geometry.DecodePosition(eob, scan, out int x, out int y, out int rc);
        int eobMag = Math.Abs(coefficients[rc]);
        int eobContext = 1 + (eob > (2 << tx2dSizeContext) ? 1 : 0) + (eob > (4 << tx2dSizeContext) ? 1 : 0);
        int eobToken = Math.Min(eobMag, 3) - 1;
        encoder.WriteSymbol(eobToken, eobBaseToken[eobContext]);
        int levelByte = (eobToken + 1) * 0x41;
        if (eobToken == 2)
        {
            int hiContext = (is2d ? (x | y) > 1 : y != 0) ? 14 : 7;
            int hiToken = Math.Min(eobMag, Av1CoefficientLevels.MaxBaseRangeLevel);
            WriteHighToken(encoder, hiToken, baseRange[hiContext]);
            levelByte = hiToken + (3 << 6);
        }

        levels[geometry.LevelIndex(x, y, rc)] = (byte)levelByte;

        // AC coefficients.
        for (int i = eob - 1; i > 0; i--)
        {
            geometry.DecodePosition(i, scan, out x, out y, out int rcI);
            int levelIndex = geometry.LevelIndex(x, y, rcI);
            int context = geometry.GetLowContext(levels, levelIndex, x, y, out int mag);
            int yForHi = is2d ? (y | x) : y;
            int magI = Math.Abs(coefficients[rcI]);
            int token = Math.Min(magI, 3);
            encoder.WriteSymbol(token, baseToken[context]);
            if (token == 3)
            {
                mag &= 63;
                int hiContext = ((yForHi > (is2d ? 1 : 0)) ? 14 : 7) + (mag > 12 ? 6 : (mag + 1) >> 1);
                int hiToken = Math.Min(magI, Av1CoefficientLevels.MaxBaseRangeLevel);
                WriteHighToken(encoder, hiToken, baseRange[hiContext]);
                levels[levelIndex] = (byte)(hiToken + (3 << 6));
            }
            else
            {
                levels[levelIndex] = (byte)(token * 0x41);
            }
        }

        // DC term.
        int dcMag = Math.Abs(coefficients[0]);
        int dcContext = is2d ? 0 : geometry.GetLowContext(levels, 0, 0, 0, out _);
        int dcBase = Math.Min(dcMag, 3);
        encoder.WriteSymbol(dcBase, baseToken[dcContext]);
        if (dcBase == 3)
        {
            int magForCtx;
            if (is2d)
            {
                int stride = geometry.Stride;
                magForCtx = levels[(0 * stride) + 1] + levels[(1 * stride) + 0] + levels[(1 * stride) + 1];
            }
            else
            {
                geometry.GetLowContext(levels, 0, 0, 0, out magForCtx);
            }

            magForCtx &= 63;
            int hiContext = magForCtx > 12 ? 6 : (magForCtx + 1) >> 1;
            WriteHighToken(encoder, Math.Min(dcMag, Av1CoefficientLevels.MaxBaseRangeLevel), baseRange[hiContext]);
        }

        return dcMag;
    }

    private static void WriteSignsAndResiduals(
        Av1SymbolEncoder encoder,
        Av1CoefficientCdfContext cdf,
        ReadOnlySpan<int> coefficients,
        int eob,
        int dcToken,
        ReadOnlySpan<ushort> scan,
        Av1CoefficientGeometry geometry,
        int chroma,
        int dcSignContext)
    {
        if (dcToken != 0)
        {
            int dc = coefficients[0];
            encoder.WriteSymbol(dc < 0 ? 1 : 0, cdf.DcSign[(chroma * 3) + dcSignContext]);
            int mag = Math.Abs(dc);
            if (mag >= Av1CoefficientLevels.MaxBaseRangeLevel)
            {
                encoder.WriteGolomb((uint)(mag - Av1CoefficientLevels.MaxBaseRangeLevel));
            }
        }

        for (int i = 1; i <= eob; i++)
        {
            geometry.DecodePosition(i, scan, out _, out _, out int rc);
            int value = coefficients[rc];
            if (value == 0)
            {
                continue;
            }

            encoder.WriteBool(value < 0 ? 1 : 0);
            int mag = Math.Abs(value);
            if (mag >= Av1CoefficientLevels.MaxBaseRangeLevel)
            {
                encoder.WriteGolomb((uint)(mag - Av1CoefficientLevels.MaxBaseRangeLevel));
            }
        }
    }

    private static void WriteHighToken(Av1SymbolEncoder encoder, int level, Span<ushort> baseRangeCdf)
    {
        const int maxSymbol = Av1CoefficientLevels.BaseRangeCdfSize - 1;
        int remaining = level - (1 + Av1CoefficientLevels.NumBaseLevels);
        for (int index = 0; index < Av1CoefficientLevels.CoefficientBaseRange; index += maxSymbol)
        {
            int coefficientBaseRange = Math.Min(remaining, maxSymbol);
            encoder.WriteSymbol(coefficientBaseRange, baseRangeCdf);
            remaining -= coefficientBaseRange;
            if (coefficientBaseRange < maxSymbol)
            {
                break;
            }
        }
    }
}
