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
    public void EobBin16_MatchesKnownReferenceValues()
    {
        // dav1d default coef.eob_bin_16 qctx0, plane 0, ctx 0 is CDF4(840, 1039, 1980, 4895).
        Assert.Equal(31928, Av1DefaultCoefficientCdf.EobBin16[0, 0, 0, 0]);
        Assert.Equal(31729, Av1DefaultCoefficientCdf.EobBin16[0, 0, 0, 1]);
        Assert.Equal(30788, Av1DefaultCoefficientCdf.EobBin16[0, 0, 0, 2]);
        Assert.Equal(27873, Av1DefaultCoefficientCdf.EobBin16[0, 0, 0, 3]);
        Assert.Equal(0, Av1DefaultCoefficientCdf.EobBin16[0, 0, 0, 4]); // terminal boundary
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

    [Fact]
    public void MultiSymbolTables_AreWellFormed()
    {
        AssertWellFormed(Av1DefaultCoefficientCdf.BaseToken, 5, 2);    // 4-symbol: 3 boundaries
        AssertWellFormed(Av1DefaultCoefficientCdf.BaseRange, 5, 2);    // 4-symbol: 3 boundaries
        AssertWellFormed(Av1DefaultCoefficientCdf.EobBaseToken, 4, 2); // 3-symbol: 2 boundaries
    }

    private static void AssertWellFormed(ushort[,,,,] table, int innerLength, int countAndTerminal)
    {
        int boundaries = innerLength - countAndTerminal;
        int d0 = table.GetLength(0);
        int d1 = table.GetLength(1);
        int d2 = table.GetLength(2);
        int d3 = table.GetLength(3);
        for (int a = 0; a < d0; a++)
        {
            for (int b = 0; b < d1; b++)
            {
                for (int c = 0; c < d2; c++)
                {
                    for (int d = 0; d < d3; d++)
                    {
                        int previous = 32768;
                        for (int i = 0; i < boundaries; i++)
                        {
                            ushort value = table[a, b, c, d, i];
                            Assert.InRange(value, 1, previous - 1); // strictly decreasing, positive
                            previous = value;
                        }

                        // Terminal 0 and adaptation counter 0.
                        for (int i = boundaries; i < innerLength; i++)
                        {
                            Assert.Equal(0, table[a, b, c, d, i]);
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(2, 3, 1, 20)]
    [InlineData(3, 4, 0, 40)]
    public void BaseToken_DefaultCdf_RoundTrips(int qctx, int tx, int plane, int ctx)
        => AssertMultiSymbolRoundTrip(
            Av1DefaultCoefficientCdf.GetBaseToken(qctx, tx, plane, ctx),
            seed: (qctx * 1000) + (tx * 100) + (plane * 41) + ctx);

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 3, 1, 20)]
    public void BaseRange_DefaultCdf_RoundTrips(int qctx, int set, int plane, int ctx)
        => AssertMultiSymbolRoundTrip(
            Av1DefaultCoefficientCdf.GetBaseRange(qctx, set, plane, ctx),
            seed: (qctx * 2000) + (set * 100) + (plane * 21) + ctx);

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 4, 1, 3)]
    public void EobBaseToken_DefaultCdf_RoundTrips(int qctx, int tx, int plane, int ctx)
        => AssertMultiSymbolRoundTrip(
            Av1DefaultCoefficientCdf.GetEobBaseToken(qctx, tx, plane, ctx),
            seed: (qctx * 3000) + (tx * 100) + (plane * 4) + ctx);

    private static void AssertMultiSymbolRoundTrip(ushort[] defaultCdf, int seed)
    {
        int nsymbs = defaultCdf.Length - 1;
        Random random = new(seed);
        int[] symbols = new int[800];

        ushort[] encoderCdf = (ushort[])defaultCdf.Clone();
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < symbols.Length; i++)
        {
            symbols[i] = random.Next(nsymbs);
            encoder.WriteSymbol(symbols[i], encoderCdf);
        }

        byte[] data = encoder.Finish();

        ushort[] decoderCdf = (ushort[])defaultCdf.Clone();
        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < symbols.Length; i++)
        {
            Assert.Equal(symbols[i], decoder.ReadSymbol(decoderCdf));
        }

        Assert.Equal(encoderCdf, decoderCdf);
    }

    [Fact]
    public void EobBins_AreWellFormed()
    {
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin16, 6);
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin32, 7);
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin64, 8);
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin128, 9);
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin256, 10);
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin512, 11);
        AssertVectorsWellFormed(Av1DefaultCoefficientCdf.EobBin1024, 12);
    }

    [Fact]
    public void EobHighBit_BoundariesAreValid()
    {
        foreach (ushort boundary in Av1DefaultCoefficientCdf.EobHighBit)
        {
            Assert.InRange(boundary, 1, 32767);
        }
    }

    [Fact]
    public void EobBin16_DefaultCdf_RoundTrips()
        => AssertMultiSymbolRoundTrip(Av1DefaultCoefficientCdf.GetEobBin16(0, 0, 0), seed: 11);

    [Fact]
    public void EobBin1024_DefaultCdf_RoundTrips()
        => AssertMultiSymbolRoundTrip(Av1DefaultCoefficientCdf.GetEobBin1024(3, 1), seed: 13);

    [Fact]
    public void EobHighBit_DefaultCdf_RoundTrips()
        => AssertRoundTrip(Av1DefaultCoefficientCdf.EobHighBit[2, 3, 1, 4], seed: 17);

    // Generic well-formedness check for any-rank table whose last dimension is a full CDF
    // (strictly decreasing boundaries, then a terminal 0 and an adaptation counter 0).
    private static void AssertVectorsWellFormed(Array table, int innerLength)
    {
        int rank = table.Rank;
        int boundaries = innerLength - 2;
        int[] dims = new int[rank];
        long outerCount = 1;
        for (int r = 0; r < rank - 1; r++)
        {
            dims[r] = table.GetLength(r);
            outerCount *= dims[r];
        }

        int[] index = new int[rank];
        for (long n = 0; n < outerCount; n++)
        {
            long rem = n;
            for (int r = rank - 2; r >= 0; r--)
            {
                index[r] = (int)(rem % dims[r]);
                rem /= dims[r];
            }

            int previous = 32768;
            for (int i = 0; i < boundaries; i++)
            {
                index[rank - 1] = i;
                ushort value = (ushort)table.GetValue(index);
                Assert.InRange(value, 1, previous - 1);
                previous = value;
            }

            for (int i = boundaries; i < innerLength; i++)
            {
                index[rank - 1] = i;
                Assert.Equal(0, (ushort)table.GetValue(index));
            }
        }
    }
}
