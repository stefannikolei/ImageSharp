// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the separable Wiener loop-restoration kernels against dav1d 1.4.1's
/// <c>wiener_filter_h</c> / <c>wiener_filter_v</c> for 8-bit samples. The horizontal pass is checked
/// with and without the left/right edges available (the right-available case reads three padding
/// samples past the row, as in dav1d's restoration-unit buffer); the vertical pass is checked over
/// seven intermediate rows.
/// </summary>
public class Av1WienerFilterTests
{
    [Fact]
    public void Horizontal_BothEdges_MatchesDav1d()
    {
        byte[] left = [74, 235, 232, 89];
        byte[] src = [201, 179, 221, 56, 62, 41, 233, 134, 24, 104, 171];
        short[] fh = [3, -7, 15, 106, 15, -7, 3];
        ushort[] expected = [8143, 8058, 8191, 4187, 3764, 3726, 8191, 6410];

        ushort[] dst = new ushort[8];
        Av1WienerFilter.FilterHorizontal(dst, src, 0, left, fh, 8, Av1WienerFilter.EdgeFlags.Left | Av1WienerFilter.EdgeFlags.Right);

        Assert.Equal(expected, dst);
    }

    [Fact]
    public void Horizontal_NoEdges_MatchesDav1d()
    {
        byte[] src = [109, 113, 147, 248, 108, 168, 224, 36, 130, 98, 82];
        short[] fh = [-5, 0, 0, 138, 0, 0, -5];
        ushort[] expected = [5449, 5670, 6763, 8191, 5546, 7520, 8191, 3155];

        ushort[] dst = new ushort[8];
        Av1WienerFilter.FilterHorizontal(dst, src, 0, ReadOnlySpan<byte>.Empty, fh, 8, 0);

        Assert.Equal(expected, dst);
    }

    [Fact]
    public void Vertical_MatchesDav1d()
    {
        ushort[][] rows =
        [
            [1213, 2934, 1390, 1494, 3148, 2398, 3515, 354],
            [1518, 3089, 3616, 1858, 1048, 695, 433, 1505],
            [3618, 1818, 1190, 4020, 3375, 3921, 561, 2018],
            [753, 2727, 3092, 1593, 3501, 3531, 3263, 1093],
            [1368, 2977, 1698, 588, 1943, 2415, 3545, 1046],
            [3609, 2188, 2588, 3851, 3662, 1698, 3200, 2001],
            [1679, 1330, 3028, 396, 3263, 977, 3882, 1409],
        ];
        short[] fv = [1, -3, 12, 108, 12, -3, 1];
        byte[] expected = [0, 38, 45, 0, 84, 93, 66, 0];

        byte[] dst = new byte[8];
        Av1WienerFilter.FilterVertical(dst, 0, rows, fv, 8);

        Assert.Equal(expected, dst);
    }
}
