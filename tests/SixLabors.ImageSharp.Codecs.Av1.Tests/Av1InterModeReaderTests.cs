// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the single-reference inter-mode decoder (<see cref="Av1InterModeReader"/>).
/// A test-only encoder writes the exact new/global/ref-mv flag cascade and DRL bits the reference
/// decoder reads, using the same adaptive inter-mode CDFs; the decoder must recover the original mode
/// and dynamic-reference-list index.
/// </summary>
public class Av1InterModeReaderTests
{
    private static readonly Av1MotionVectorCandidate[] Candidates =
    [
        new(new Av1MotionVector(0, 32), 800),
        new(new Av1MotionVector(0, 16), 700),
        new(new Av1MotionVector(8, 0), 600),
        new(new Av1MotionVector(4, 4), 100),
    ];

    public static TheoryData<int, int, int, int> Cases { get; } = new()
    {
        // context, candidateCount, mode, drlIndex
        { 0, 1, (int)Av1InterPredictionMode.NewMv, 0 },
        { 5, 3, (int)Av1InterPredictionMode.NewMv, 2 },
        { 0x15, 4, (int)Av1InterPredictionMode.NewMv, 1 },
        { 3, 1, (int)Av1InterPredictionMode.NearestMv, 0 },
        { 0x2A, 4, (int)Av1InterPredictionMode.NearMv, 1 },
        { 0x2A, 4, (int)Av1InterPredictionMode.NearMv, 2 },
        { 0x2A, 4, (int)Av1InterPredictionMode.NearMv, 3 },
        { 4, 2, (int)Av1InterPredictionMode.GlobalMv, 0 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReadMode_RoundTripsThroughEncoder(int context, int candidateCount, int mode, int drlIndex)
    {
        Av1InterPredictionMode expectedMode = (Av1InterPredictionMode)mode;

        Av1SymbolEncoder encoder = new();
        Av1InterModeCdfContext encoderCdf = Av1InterModeCdfContext.CreateDefault();
        EncodeMode(encoder, encoderCdf, context, candidateCount, expectedMode, drlIndex);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterModeCdfContext decoderCdf = Av1InterModeCdfContext.CreateDefault();
        (Av1InterPredictionMode actualMode, int actualDrl) = Av1InterModeReader.ReadMode(
            decoder, decoderCdf, context, candidateCount, Candidates, forceGlobalMv: false);

        Assert.Equal(expectedMode, actualMode);
        Assert.Equal(drlIndex, actualDrl);
    }

    private static void EncodeMode(
        Av1SymbolEncoder encoder,
        Av1InterModeCdfContext cdf,
        int context,
        int candidateCount,
        Av1InterPredictionMode mode,
        int drlIndex)
    {
        if (mode != Av1InterPredictionMode.NewMv)
        {
            encoder.WriteSymbol(1, cdf.NewMv[context & 7]);
            if (mode == Av1InterPredictionMode.GlobalMv)
            {
                encoder.WriteSymbol(0, cdf.GlobalMv[(context >> 3) & 1]);
                return;
            }

            encoder.WriteSymbol(1, cdf.GlobalMv[(context >> 3) & 1]);
            if (mode == Av1InterPredictionMode.NearMv)
            {
                encoder.WriteSymbol(1, cdf.RefMv[(context >> 4) & 15]);
                int drl = 1;
                if (candidateCount > 2)
                {
                    int bit = drlIndex > 1 ? 1 : 0;
                    encoder.WriteSymbol(bit, cdf.DrlBit[Av1InterModeReader.GetDrlContext(Candidates, 1)]);
                    drl += bit;
                    if (drl == 2 && candidateCount > 3)
                    {
                        int bit2 = drlIndex > 2 ? 1 : 0;
                        encoder.WriteSymbol(bit2, cdf.DrlBit[Av1InterModeReader.GetDrlContext(Candidates, 2)]);
                    }
                }
            }
            else
            {
                encoder.WriteSymbol(0, cdf.RefMv[(context >> 4) & 15]);
            }
        }
        else
        {
            encoder.WriteSymbol(0, cdf.NewMv[context & 7]);
            int drl = 0;
            if (candidateCount > 1)
            {
                int bit = drlIndex > 0 ? 1 : 0;
                encoder.WriteSymbol(bit, cdf.DrlBit[Av1InterModeReader.GetDrlContext(Candidates, 0)]);
                drl += bit;
                if (drl == 1 && candidateCount > 2)
                {
                    int bit2 = drlIndex > 1 ? 1 : 0;
                    encoder.WriteSymbol(bit2, cdf.DrlBit[Av1InterModeReader.GetDrlContext(Candidates, 1)]);
                }
            }
        }
    }
}
