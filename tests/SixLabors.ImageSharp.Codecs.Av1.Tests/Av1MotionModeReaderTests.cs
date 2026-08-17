// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the motion-mode decoder (<see cref="Av1MotionModeReader"/>): the three-way
/// SIMPLE / OBMC / WARP symbol when warped motion is allowed, and the binary OBMC flag otherwise.
/// </summary>
public class Av1MotionModeReaderTests
{
    [Theory]
    [InlineData((int)Av1BlockSize.Block16x16, (int)Av1MotionMode.Translation)]
    [InlineData((int)Av1BlockSize.Block16x16, (int)Av1MotionMode.Obmc)]
    [InlineData((int)Av1BlockSize.Block16x16, (int)Av1MotionMode.Warp)]
    [InlineData((int)Av1BlockSize.Block8x8, (int)Av1MotionMode.Warp)]
    [InlineData((int)Av1BlockSize.Block64x64, (int)Av1MotionMode.Obmc)]
    public void ReadMotionMode_WarpAllowedRoundTrips(int blockSize, int mode)
    {
        Av1SymbolEncoder encoder = new();
        Av1MotionModeCdfContext encoderCdf = Av1MotionModeCdfContext.CreateDefault();
        encoder.WriteSymbol(mode, encoderCdf.MotionMode[blockSize]!);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1MotionModeCdfContext decoderCdf = Av1MotionModeCdfContext.CreateDefault();
        Av1MotionMode actual = Av1MotionModeReader.ReadMotionMode(decoder, decoderCdf, (Av1BlockSize)blockSize, allowWarp: true);

        Assert.Equal((Av1MotionMode)mode, actual);
    }

    [Theory]
    [InlineData((int)Av1BlockSize.Block16x16, (int)Av1MotionMode.Translation)]
    [InlineData((int)Av1BlockSize.Block16x16, (int)Av1MotionMode.Obmc)]
    [InlineData((int)Av1BlockSize.Block32x16, (int)Av1MotionMode.Obmc)]
    public void ReadMotionMode_WarpDisallowedRoundTrips(int blockSize, int mode)
    {
        Av1SymbolEncoder encoder = new();
        Av1MotionModeCdfContext encoderCdf = Av1MotionModeCdfContext.CreateDefault();
        encoder.WriteSymbol(mode == (int)Av1MotionMode.Obmc ? 1 : 0, encoderCdf.Obmc[blockSize]!);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1MotionModeCdfContext decoderCdf = Av1MotionModeCdfContext.CreateDefault();
        Av1MotionMode actual = Av1MotionModeReader.ReadMotionMode(decoder, decoderCdf, (Av1BlockSize)blockSize, allowWarp: false);

        Assert.Equal((Av1MotionMode)mode, actual);
    }
}
