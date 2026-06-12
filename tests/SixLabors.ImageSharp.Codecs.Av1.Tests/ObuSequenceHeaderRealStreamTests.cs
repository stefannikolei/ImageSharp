// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the full sequence-header parse against the sequence-header OBU payload taken from a real,
/// dav1d-decodable AV1 stream (a 64x64 all-intra clip produced by aomenc 3.8.2). The expected field
/// values were derived by an independent bit-accurate reference parse of specification section 5.5.
/// </summary>
public class ObuSequenceHeaderRealStreamTests
{
    // Sequence-header OBU payload from the real stream.
    private static readonly byte[] Payload = Convert.FromHexString("00000002afff9b5f3008");

    [Fact]
    public void Parse_RealStream_MatchesReferenceFields()
    {
        ObuSequenceHeader h = ObuSequenceHeader.Parse(Payload);

        Assert.Equal(0, h.SeqProfile);
        Assert.False(h.StillPicture);
        Assert.False(h.ReducedStillPictureHeader);
        Assert.Equal(6, h.FrameWidthBits);
        Assert.Equal(6, h.FrameHeightBits);
        Assert.Equal(64, h.MaxFrameWidth);
        Assert.Equal(64, h.MaxFrameHeight);

        Assert.False(h.Use128x128Superblock);
        Assert.True(h.EnableFilterIntra);
        Assert.True(h.EnableIntraEdgeFilter);
        Assert.False(h.EnableInterIntraCompound);
        Assert.True(h.EnableMaskedCompound);
        Assert.True(h.EnableWarpedMotion);
        Assert.False(h.EnableDualFilter);
        Assert.True(h.EnableOrderHint);
        Assert.Equal(7, h.OrderHintBits);

        Assert.False(h.EnableSuperResolution);
        Assert.True(h.EnableCdef);
        Assert.True(h.EnableRestoration);

        Assert.Equal(8, h.BitDepth);
        Assert.False(h.MonoChrome);
        Assert.Equal(3, h.NumPlanes);
        Assert.Equal(1, h.SubsamplingX);
        Assert.Equal(1, h.SubsamplingY);
        Assert.False(h.ColorRange);
        Assert.False(h.SeparateUvDeltaQ);
        Assert.False(h.FilmGrainParamsPresent);
    }
}
