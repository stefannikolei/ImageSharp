// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the CDEF parameter parse against the real 128x128 clip's frame header. The expected
/// values were taken from dav1d 1.4.1 (damping 4, 1 preset, y_strength 24 => primary 6 / secondary 0).
/// </summary>
public class ObuCdefParamsTests
{
    private static readonly byte[] SequencePayload = Convert.FromHexString("1819bfffec02");

    private static readonly byte[] FramePayload = Convert.FromHexString(
        "1780041000048c0000e2462ad9b42655895c4775463e2cfc148b923ad111cce78d7a0795b1c5e3c1564f450436048573cb0af86e8d1365ff68a2cb015eb68049b32ff6ded56537414eb83a6e7ecddb111fd8e0edd37a3c2d9280");

    [Fact]
    public void ParseIntra_CdefParameters_MatchDav1d()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);
        Av1BitStreamReader reader = new(FramePayload);
        ObuFrameHeader frameHeader = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);

        ObuFrameHeader.Cdef cdef = frameHeader.CdefParameters;
        Assert.Equal(4, cdef.Damping);
        Assert.Equal(0, cdef.Bits);
        Assert.Single(cdef.YPrimary);
        Assert.Equal(6, cdef.YPrimary[0]);
        Assert.Equal(0, cdef.YSecondary[0]);
        Assert.Equal(0, cdef.UvPrimary[0]);
        Assert.Equal(0, cdef.UvSecondary[0]);
    }
}
