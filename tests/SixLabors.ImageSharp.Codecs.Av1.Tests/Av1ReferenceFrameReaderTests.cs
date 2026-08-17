// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the single-reference frame selection decoder
/// (<see cref="Av1ReferenceFrameReader"/>). A test-only encoder writes the binary-tree reference bits
/// the reference decoder reads, using the same adaptive reference CDFs; the decoder must recover every
/// reference index in the range [0, 6].
/// </summary>
public class Av1ReferenceFrameReaderTests
{
    private static readonly int[] Contexts = [1, 0, 2, 1, 0, 2];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ReadSingleReference_RoundTripsThroughEncoder(int reference)
    {
        Av1SymbolEncoder encoder = new();
        Av1InterModeCdfContext encoderCdf = Av1InterModeCdfContext.CreateDefault();
        EncodeReference(encoder, encoderCdf, reference);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterModeCdfContext decoderCdf = Av1InterModeCdfContext.CreateDefault();
        int actual = Av1ReferenceFrameReader.ReadSingleReference(decoder, decoderCdf, Contexts);

        Assert.Equal(reference, actual);
    }

    private static void EncodeReference(Av1SymbolEncoder encoder, Av1InterModeCdfContext cdf, int reference)
    {
        if (reference >= 4)
        {
            encoder.WriteSymbol(1, cdf.SingleReference[0][Contexts[0]]);
            if (reference == 6)
            {
                encoder.WriteSymbol(1, cdf.SingleReference[1][Contexts[1]]);
            }
            else
            {
                encoder.WriteSymbol(0, cdf.SingleReference[1][Contexts[1]]);
                encoder.WriteSymbol(reference - 4, cdf.SingleReference[5][Contexts[5]]);
            }
        }
        else
        {
            encoder.WriteSymbol(0, cdf.SingleReference[0][Contexts[0]]);
            if (reference >= 2)
            {
                encoder.WriteSymbol(1, cdf.SingleReference[2][Contexts[2]]);
                encoder.WriteSymbol(reference - 2, cdf.SingleReference[4][Contexts[4]]);
            }
            else
            {
                encoder.WriteSymbol(0, cdf.SingleReference[2][Contexts[2]]);
                encoder.WriteSymbol(reference, cdf.SingleReference[3][Contexts[3]]);
            }
        }
    }
}
