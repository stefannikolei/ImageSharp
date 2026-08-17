// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the motion-vector residual decoder (<see cref="Av1MotionVectorReader"/>).
/// A test-only encoder writes the exact joint/sign/class/fp/hp symbol sequence the reference decoder
/// reads, using the same adaptive MV CDFs; the decoder must recover the original motion vectors.
/// </summary>
public class Av1MotionVectorReaderTests
{
    public static TheoryData<int, int, int, int, int> Vectors { get; } = new()
    {
        // predictorY, predictorX, mvY, mvX, precision
        { 0, 0, 0, 0, 1 },
        { 0, 0, 0, 32, 1 },
        { 0, 0, 8, -3, 1 },
        { 0, 0, 16, -100, 1 },
        { 0, 0, -1, 1, 1 },
        { 4, -7, 132, -260, 1 },

        // Integer-subpel precision forces the high-precision bit to 1, so the magnitude must be even.
        { 0, 0, 6, -10, 0 },

        // Force-integer precision forces fp=3 and hp=1, so the diffs must be whole pels (multiples of 8).
        { 0, 0, 8, -16, -1 },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ReadResidual_RoundTripsThroughEncoder(int predictorY, int predictorX, int mvY, int mvX, int precision)
    {
        Av1MotionVector predictor = new(predictorY, predictorX);
        Av1MotionVector expected = new(mvY, mvX);

        Av1SymbolEncoder encoder = new();
        Av1MotionVectorCdfContext encoderCdf = Av1MotionVectorCdfContext.CreateDefault();
        EncodeResidual(encoder, encoderCdf, predictor, expected, precision);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1MotionVectorCdfContext decoderCdf = Av1MotionVectorCdfContext.CreateDefault();
        Av1MotionVector actual = Av1MotionVectorReader.ReadResidual(decoder, decoderCdf, predictor, precision);

        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.X, actual.X);
    }

    [Fact]
    public void ReadResidual_AdaptsAcrossMultipleVectors()
    {
        (int Y, int X)[] sequence = [(0, 32), (8, -3), (-16, 100), (1, 1)];

        Av1SymbolEncoder encoder = new();
        Av1MotionVectorCdfContext encoderCdf = Av1MotionVectorCdfContext.CreateDefault();
        foreach ((int y, int x) in sequence)
        {
            EncodeResidual(encoder, encoderCdf, default, new Av1MotionVector(y, x), 1);
        }

        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1MotionVectorCdfContext decoderCdf = Av1MotionVectorCdfContext.CreateDefault();
        foreach ((int y, int x) in sequence)
        {
            Av1MotionVector actual = Av1MotionVectorReader.ReadResidual(decoder, decoderCdf, default, 1);
            Assert.Equal(y, actual.Y);
            Assert.Equal(x, actual.X);
        }
    }

    private static void EncodeResidual(
        Av1SymbolEncoder encoder,
        Av1MotionVectorCdfContext cdf,
        Av1MotionVector predictor,
        Av1MotionVector mv,
        int precision)
    {
        int diffY = mv.Y - predictor.Y;
        int diffX = mv.X - predictor.X;
        int joint = 0;
        if (diffY != 0)
        {
            joint |= 2;
        }

        if (diffX != 0)
        {
            joint |= 1;
        }

        encoder.WriteSymbol(joint, cdf.Joint);
        if ((joint & 2) != 0)
        {
            EncodeComponentDiff(encoder, cdf.Components[0], diffY, precision);
        }

        if ((joint & 1) != 0)
        {
            EncodeComponentDiff(encoder, cdf.Components[1], diffX, precision);
        }
    }

    private static void EncodeComponentDiff(
        Av1SymbolEncoder encoder,
        Av1MotionVectorCdfContext.Component component,
        int diff,
        int precision)
    {
        int sign = diff < 0 ? 1 : 0;
        int magnitude = Math.Abs(diff) - 1;
        int hp = magnitude & 1;
        int fp = (magnitude >> 1) & 3;
        int up = magnitude >> 3;
        int classIndex = up < 2 ? 0 : (31 - BitOperations.LeadingZeroCount((uint)up));

        encoder.WriteSymbol(sign, component.Sign);
        encoder.WriteSymbol(classIndex, component.Classes);
        if (classIndex == 0)
        {
            encoder.WriteSymbol(up, component.Class0);
            if (precision >= 0)
            {
                encoder.WriteSymbol(fp, component.Class0Fp[up]);
                if (precision > 0)
                {
                    encoder.WriteSymbol(hp, component.Class0Hp);
                }
            }
        }
        else
        {
            for (int n = 0; n < classIndex; n++)
            {
                encoder.WriteSymbol((up >> n) & 1, component.ClassN[n]);
            }

            if (precision >= 0)
            {
                encoder.WriteSymbol(fp, component.ClassNFp);
                if (precision > 0)
                {
                    encoder.WriteSymbol(hp, component.ClassNHp);
                }
            }
        }
    }
}
