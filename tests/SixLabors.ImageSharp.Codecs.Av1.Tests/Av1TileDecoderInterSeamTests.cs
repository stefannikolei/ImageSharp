// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the inter-reuse seam of <see cref="Av1TileDecoder"/>: a derived decoder can substitute its
/// own prediction (as an inter decoder does with motion compensation) by overriding <c>Predict</c>, and
/// the shared <c>Reconstruct</c> adds the residual on top of it unchanged. This is the mechanism that
/// lets an inter tile decoder reuse the bit-exact residual pipeline without duplicating it.
/// </summary>
public class Av1TileDecoderInterSeamTests
{
    private static readonly byte[] SequencePayload = Convert.FromHexString("00000002afff9b5f3008");
    private static readonly byte[] FramePayload = Convert.FromHexString("1000d00000028800001ff8195e23effcafeea34da6");

    private static SeamDecoder CreateDecoder(byte predictionFill)
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);
        Av1BitStreamReader reader = new(FramePayload);
        ObuFrameHeader frameHeader = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);
        return new SeamDecoder(sequenceHeader, frameHeader, predictionFill);
    }

    [Fact]
    public void Reconstruct_UsesOverriddenPrediction_WhenNoResidual()
    {
        SeamDecoder decoder = CreateDecoder(predictionFill: 100);
        int[] levels = new int[4 * 4];

        decoder.RunReconstruct(0, 0, levels, Av1CoefficientReader.AllZero);

        // With no residual, the reconstructed 4x4 equals the substituted prediction exactly.
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(100, decoder.Luma[x, y]);
            }
        }
    }

    [Fact]
    public void Reconstruct_AddsResidualOnTopOfOverriddenPrediction()
    {
        SeamDecoder decoder = CreateDecoder(predictionFill: 0);
        int[] levels = new int[4 * 4];
        levels[0] = 6; // a positive DC level -> a uniform positive residual.

        decoder.RunReconstruct(0, 0, levels, eob: 1);

        // The residual is added on top of the zero prediction, so every sample is uniform and positive.
        ushort first = decoder.Luma[0, 0];
        Assert.True(first > 0, "Expected a positive DC residual on top of the zero prediction.");
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(first, decoder.Luma[x, y]);
            }
        }
    }

    private sealed class SeamDecoder : Av1TileDecoder
    {
        private readonly byte predictionFill;

        public SeamDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, byte predictionFill)
            : base(sequenceHeader, frameHeader)
            => this.predictionFill = predictionFill;

        public void RunReconstruct(int x, int y, int[] levels, int eob)
            => this.Reconstruct(this.Luma, x, y, Av1TransformSize.Size4x4, Av1TransformType.DctDct, levels, eob, 0, 0, -1, 0);

        private protected override void Predict(
            Av1Plane plane, int x, int y, int width, int height, int intraMode, int angleDelta, int filterIntraMode, int cflAlpha, ushort[] prediction)
            => Array.Fill(prediction, this.predictionFill);
    }
}
