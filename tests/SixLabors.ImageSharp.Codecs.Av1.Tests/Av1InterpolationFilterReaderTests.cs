// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the switchable interpolation-filter decoder
/// (<see cref="Av1InterpolationFilterReader"/>). A test-only encoder writes the filter symbols the
/// reference decoder reads, using the same adaptive filter CDFs; the decoder must recover the filters.
/// </summary>
public class Av1InterpolationFilterReaderTests
{
    [Theory]
    [InlineData(0, 0, true, 0, 0)]
    [InlineData(2, 1, true, 3, 5)]
    [InlineData(1, 1, false, 7, 2)]
    public void ReadFilters_DualRoundTrips(int horizontal, int vertical, bool dualFilter, int hctx, int vctx)
    {
        Av1SymbolEncoder encoder = new();
        Av1InterpolationFilterCdfContext encoderCdf = Av1InterpolationFilterCdfContext.CreateDefault();
        encoder.WriteSymbol(horizontal, encoderCdf.Filter[0][hctx]);
        if (dualFilter)
        {
            encoder.WriteSymbol(vertical, encoderCdf.Filter[1][vctx]);
        }

        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterpolationFilterCdfContext decoderCdf = Av1InterpolationFilterCdfContext.CreateDefault();
        (int actualH, int actualV) = Av1InterpolationFilterReader.ReadFilters(
            decoder, decoderCdf, hasSubpelFilter: true, dualFilter, hctx, vctx);

        Assert.Equal(horizontal, actualH);
        Assert.Equal(dualFilter ? vertical : horizontal, actualV);
    }

    [Fact]
    public void ReadFilters_WithoutSubpelFilterReturnsRegular()
    {
        Av1SymbolEncoder encoder = new();
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterpolationFilterCdfContext cdf = Av1InterpolationFilterCdfContext.CreateDefault();
        (int h, int v) = Av1InterpolationFilterReader.ReadFilters(
            decoder, cdf, hasSubpelFilter: false, dualFilter: true, 0, 0);

        Assert.Equal(Av1InterpolationFilterReader.Regular, h);
        Assert.Equal(Av1InterpolationFilterReader.Regular, v);
    }
}
