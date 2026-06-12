// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Pins <see cref="Av1SymbolDecoder"/> to output that is bit-exact with the dav1d reference MSAC
/// implementation. The byte stream below was produced by <see cref="Av1SymbolEncoder"/> and then
/// decoded by dav1d 1.4.1's <c>dav1d_msac_*</c> functions; the expected values are dav1d's output,
/// confirming the encoder (and, via the round-trip tests, the decoder) match the reference bit-for-bit.
/// The script exercises equiprobable bits, multi-bit literals, Exp-Golomb codes and adaptive symbols
/// over alphabet sizes 2, 4, 8 and 16 with CDF adaptation, all interleaved on a single MSAC state.
/// </summary>
public class Av1SymbolDecoderReferenceTests
{
    private const string DataBase64 =
        "AAAmb7jOQMTP4aYAtpZVk2bqqqfHrK3vRKBlSs8006oCfxSaAimO+lbwVSaQG4QfGgHooM5TEuS/IsVg35px/DEIQf+8FZ7CZ1wceKCRHgvQRfxrDev8UhEQJ3032UeTWALgfgvdwqWlE6zzYPiBRutTvj6+CGYujAWFpWn7x5EkD2a3mOFUR/OAF26raqgHJqUkQK38anwIwNVQ/Ja7uOBawxRSHMgh5LfLwRocKKgaubJSIQRkYDpIcsHBUyiDBtyduCb5VqVOEAm9VxznS4HIK/WgKhP8kDL3flKVGJ55ptvr7q+Mwir0CZlxmvZhyACV/PkACY8pMQClDKIcEClYYLKUCIyyrCBGEuSYEvfNcAmnwUWwCGmge7AGaRqB8EICnfRs7m/hoLfeNPw9N+EFRnVvPJmQQ5Dx4wHh8wOlz4mo8AF8YgfMZEgErRB3HJKvIkSQBbc3QC2KSQoAd0Z9m+Bx501VQV9DpokPVDwJqFCqofrmIfD922Iuy+BykkbhQCiTYbvCQCxXysMOqwVZwBjllH9QN4Dty8EDu4IlMwJOnH+2Qa9oLhiYkvqZELaAIoE2RHzNHQhMPSWc22pDXAddIaBhHHUURHHsDijWAyC175iBVxKc5gCXPwIiNPjeDjazgyFhcC8qvVizoA==";

    private const string Script =
        "G;S3;G;S3;S1;G;S3;S0;L2;E;L15;L5;S0;S1;L2;S3;L3;L15;E;S1;L9;S2;L6;G;L15;E;S2;E;G;G;S0;S0;E;L2;L8;S3;L10;S2;S1;G;G;S1;S3;E;L7;S2;E;E;S0;E;S0;S0;S1;E;L10;S0;S3;E;S0;S1;S2;L11;S2;L16;S3;S0;G;G;S1;L5;E;S2;L12;S1;G;S1;S3;L9;S1;S3;S0;G;E;L8;S1;G;S0;G;L5;S0;E;L7;E;S1;S0;S1;E;E;E;S0;L10;S2;S1;E;E;E;E;E;E;E;E;E;S0;E;S0;G;E;E;S0;S0;E;S2;G;E;S1;L7;L10;G;S1;L5;G;L16;E;E;G;G;G;E;S2;G;G;G;L15;S0;L7;S3;S0;E;S2;G;L12;G;S3;S3;E;E;E;E;S2;L7;L4;L10;L5;S1;S3;G;S0;S1;L5;G;G;L8;S1;G;G;E;L9;S2;G;S1;S0;S0;E;L12;L16;S1;E;L2;S0;S2;L8;G;L9;L8;S1;S0;E;S3;L4;S0;L14;L8;S3;S1;G;E;S3;L15;E;L14;L5;E;E;G;L16;E;S2;S3;G;G;G;S0;S1;S0;G;S1;S3;G;G;G;E;S2;G;S2;E;G;S3;G;L5;G;L3;L16;S0;E;S1;S2;S0;L2;E;G;E;L12;L11;G;L15;S0;G;E;G;L3;E;E;S2;L13;L16;G;S0;S3;E;L16;S2;L8;E;S2;E;E;G;S3;E;S2;E;L13;L14;G;L3;G;S0;S0;G;L4;G;S0;S2;L5;L1;S3;E;L10;E;S1;L7;L16;E;L9;E;G;E;S3;L7;E;E;L4;L2;S1;G;S3;S1;S3;S3;S1;S2;E;G;S2;E;G;G;L10;E;G;S0;S1;G;S3;G;L5;L1;E;L2;E;G;S0;E;E;E;G;S0;G;G;L16;S0;S1;S3;S3;E;G;S0;L4;S2;S2;S0;E;L15;L6;S1;S2;E;L15;G;L10;G;G;L9;E;S3;S1;L8;E;E;E;L15;E;G;S3;G;S3;G;E;S2;L14;L2;E;S3;G;G;G;L14;E;S3;S1;";

    private const string Expected =
        "307585 15 48115 11 0 365287 0 1 3 0 19536 3 0 1 0 0 4 25581 0 3 51 1 37 344775 4127 0 4 0 17331 480403 0 0 0 0 202 8 667 1 2 181427 402721 2 1 1 63 0 0 1 1 0 0 0 3 1 757 1 14 0 0 1 3 74 4 4552 10 1 154606 404770 2 20 0 1 572 2 452791 2 3 81 1 15 1 371850 1 113 0 82473 1 255181 16 0 0 33 0 2 1 0 0 1 1 0 440 4 2 1 0 1 0 1 0 1 0 0 0 0 1 446006 1 1 1 0 0 6 74895 0 1 35 894 67213 0 11 198146 218 0 1 270246 186591 411167 1 5 329539 250557 403378 7362 0 112 4 0 0 1 457482 2457 341676 14 7 1 1 0 0 0 98 5 836 15 1 10 2331 1 1 17 491144 100254 47 1 304022 489334 1 223 2 175301 0 1 1 0 1162 7394 1 0 2 1 6 52 476913 333 84 3 0 0 14 5 1 2111 51 8 2 123148 1 0 22071 1 383 31 0 1 283477 11098 1 1 1 415999 212103 457771 1 3 1 279643 1 7 405121 415054 6799 1 5 271735 4 0 412613 15 495556 1 155395 5 40236 0 1 1 0 1 2 1 406695 1 142 577 373382 30969 0 161730 0 288405 4 1 0 5 7344 22400 221557 0 12 0 5684 1 144 0 4 1 0 72267 3 1 3 0 8064 4479 173 4 341011 0 0 459706 14 219500 1 6 26 0 6 0 365 0 3 96 36627 1 271 0 389425 0 7 35 1 1 5 1 1 437099 14 1 13 3 0 7 1 411338 3 0 353038 156659 561 1 255245 0 0 483376 15 100321 26 0 0 1 0 215884 0 0 1 1 266881 1 117110 87704 3588 0 0 12 8 0 291743 0 5 3 7 0 1 31054 4 0 4 0 21323 22508 860 183810 221888 324 1 0 2 14 0 1 1 16726 0 481355 8 290051 11 350820 0 1 5484 3 1 4 118271 813 392475 3264 0 7 2";

    [Fact]
    public void Decoder_IsBitExactWithDav1dReference()
    {
        byte[] data = Convert.FromBase64String(DataBase64);
        string[] ops = Script.Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] expectedTokens = Expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ops.Length, expectedTokens.Length);

        // Slots 0..3 mirror the dav1d reference run: uniform CDFs for alphabet sizes 2, 4, 8 and 16.
        ushort[][] cdf =
        [
            Av1Cdf.CreateUniform(2),
            Av1Cdf.CreateUniform(4),
            Av1Cdf.CreateUniform(8),
            Av1Cdf.CreateUniform(16),
        ];

        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < ops.Length; i++)
        {
            string op = ops[i];
            long expected = long.Parse(expectedTokens[i], System.Globalization.CultureInfo.InvariantCulture);
            long actual = op[0] switch
            {
                'E' => decoder.ReadBool(),
                'G' => decoder.ReadGolomb(),
                'L' => decoder.ReadLiteral(int.Parse(op.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture)),
                'S' => decoder.ReadSymbol(cdf[op[1] - '0']),
                _ => throw new InvalidOperationException($"Unknown op '{op}'."),
            };

            Assert.True(expected == actual, $"Op {i} ({op}): expected {expected}, got {actual}.");
        }
    }
}
