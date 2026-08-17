// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the is-inter flag decoder (<see cref="Av1IsInterReader"/>): round-trip recovery of the
/// flag through the adaptive CDFs, and the neighbour intra-context derivation against the reference
/// algorithm.
/// </summary>
public class Av1IsInterReaderTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 3)]
    [InlineData(false, 2)]
    public void ReadIsInter_RoundTrips(bool isInter, int context)
    {
        Av1SymbolEncoder encoder = new();
        Av1InterModeCdfContext encoderCdf = Av1InterModeCdfContext.CreateDefault();
        encoder.WriteSymbol(isInter ? 1 : 0, encoderCdf.IsInter[context]);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterModeCdfContext decoderCdf = Av1InterModeCdfContext.CreateDefault();
        bool actual = Av1IsInterReader.ReadIsInter(decoder, decoderCdf, context);

        Assert.Equal(isInter, actual);
    }

    [Theory]
    [InlineData(true, 0, 0)]
    [InlineData(false, 1, 0)]
    [InlineData(true, 1, 1)]
    public void ReadSkipMode_RoundTrips(bool skipMode, int left, int top)
    {
        Av1SymbolEncoder encoder = new();
        Av1InterModeCdfContext encoderCdf = Av1InterModeCdfContext.CreateDefault();
        encoder.WriteSymbol(skipMode ? 1 : 0, encoderCdf.SkipMode[left + top]);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterModeCdfContext decoderCdf = Av1InterModeCdfContext.CreateDefault();
        bool actual = Av1IsInterReader.ReadSkipMode(decoder, decoderCdf, left, top);

        Assert.Equal(skipMode, actual);
    }

    [Theory]
    [InlineData(0, 0, false, false, 0)] // no neighbours
    [InlineData(1, 0, false, true, 0)]  // top only, intra=0
    [InlineData(1, 1, false, true, 2)]  // top only, intra=1 -> 2
    [InlineData(1, 0, true, false, 2)]  // left only, intra=1 -> 2
    [InlineData(1, 1, true, true, 3)]   // both intra -> 2 + 1
    [InlineData(1, 0, true, true, 1)]   // one intra -> 1
    [InlineData(0, 0, true, true, 0)]   // neither intra -> 0
    public void GetIntraContext_MatchesReference(int left, int top, bool haveLeft, bool haveTop, int expected)
        => Assert.Equal(expected, Av1IsInterReader.GetIntraContext(left, top, haveLeft, haveTop));
}
