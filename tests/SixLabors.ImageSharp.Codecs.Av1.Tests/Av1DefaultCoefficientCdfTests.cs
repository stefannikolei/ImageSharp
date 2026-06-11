// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1DefaultCoefficientCdfTests
{
    [Fact]
    public void DcSign_MatchesKnownReferenceValue()
    {
        // dav1d default dc_sign qctx0, plane 0, ctx 0 is CDF1(16000) => 32768 - 16000.
        Assert.Equal(16768, Av1DefaultCoefficientCdf.DcSign[0, 0, 0]);
    }

    [Fact]
    public void Skip_MatchesKnownReferenceValue()
    {
        // dav1d default coef.skip qctx0, tx 0, ctx 0 is CDF1(31849) => 32768 - 31849.
        Assert.Equal(919, Av1DefaultCoefficientCdf.Skip[0, 0, 0]);
    }

    [Fact]
    public void AllBoundaries_AreValidTwoSymbolCdfs()
    {
        foreach (ushort boundary in Av1DefaultCoefficientCdf.DcSign)
        {
            Assert.InRange(boundary, 1, 32767);
        }

        foreach (ushort boundary in Av1DefaultCoefficientCdf.Skip)
        {
            Assert.InRange(boundary, 1, 32767);
        }
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 0, 1)]
    public void DcSign_DefaultCdf_RoundTrips(int qctx, int plane, int ctx)
        => AssertRoundTrip(Av1DefaultCoefficientCdf.DcSign[qctx, plane, ctx], seed: (qctx * 10) + (plane * 3) + ctx);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 5)]
    [InlineData(3, 4, 9)]
    public void Skip_DefaultCdf_RoundTrips(int qctx, int tx, int ctx)
        => AssertRoundTrip(Av1DefaultCoefficientCdf.Skip[qctx, tx, ctx], seed: (qctx * 100) + (tx * 13) + ctx);

    private static void AssertRoundTrip(ushort boundary, int seed)
    {
        Random random = new(seed);
        int[] symbols = new int[600];
        ushort[] encoderCdf = Av1DefaultCoefficientCdf.CreateTwoSymbol(boundary);
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < symbols.Length; i++)
        {
            symbols[i] = random.Next(2);
            encoder.WriteSymbol(symbols[i], encoderCdf);
        }

        byte[] data = encoder.Finish();

        ushort[] decoderCdf = Av1DefaultCoefficientCdf.CreateTwoSymbol(boundary);
        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < symbols.Length; i++)
        {
            Assert.Equal(symbols[i], decoder.ReadSymbol(decoderCdf));
        }

        Assert.Equal(encoderCdf, decoderCdf);
    }
}
