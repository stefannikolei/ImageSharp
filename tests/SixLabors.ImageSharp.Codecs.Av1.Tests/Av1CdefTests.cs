// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the CDEF edge-direction search against dav1d 1.4.1's cdef_find_dir for random 8x8 blocks.
/// </summary>
public class Av1CdefTests
{
    [Fact]
    public void FindDirection_Case0_MatchesDav1d()
    {
        byte[] img = [247, 128, 213, 154, 26, 101, 177, 255, 15, 113, 154, 59, 81, 163, 20, 163, 187, 116, 146, 23, 51, 241, 145, 143, 239, 192, 78, 34, 169, 168, 94, 37, 145, 208, 42, 49, 93, 222, 254, 177, 214, 226, 255, 2, 78, 219, 144, 249, 94, 149, 62, 151, 154, 54, 205, 5, 238, 70, 170, 90, 23, 142, 85, 102];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(8917, variance);
    }
    [Fact]
    public void FindDirection_Case1_MatchesDav1d()
    {
        byte[] img = [18, 117, 169, 128, 120, 227, 70, 221, 117, 224, 64, 81, 241, 143, 65, 20, 47, 85, 195, 20, 105, 199, 138, 91, 3, 36, 67, 23, 51, 164, 154, 72, 140, 152, 239, 216, 49, 15, 128, 123, 79, 97, 33, 186, 245, 124, 19, 94, 4, 150, 42, 194, 203, 65, 252, 75, 234, 78, 125, 84, 160, 46, 169, 209];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(41962, variance);
    }
    [Fact]
    public void FindDirection_Case2_MatchesDav1d()
    {
        byte[] img = [69, 248, 196, 88, 160, 104, 226, 32, 212, 72, 197, 8, 165, 208, 102, 135, 16, 228, 18, 157, 121, 216, 125, 74, 155, 164, 238, 226, 146, 44, 219, 32, 184, 79, 116, 252, 183, 187, 190, 156, 180, 10, 237, 131, 124, 244, 165, 25, 232, 245, 67, 187, 27, 12, 88, 109, 123, 122, 188, 252, 121, 0, 141, 168];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(2774, variance);
    }
    [Fact]
    public void FindDirection_Case3_MatchesDav1d()
    {
        byte[] img = [108, 164, 80, 86, 92, 105, 208, 85, 37, 118, 232, 110, 234, 250, 205, 167, 254, 213, 249, 156, 232, 31, 5, 171, 246, 7, 128, 60, 254, 194, 34, 179, 125, 69, 22, 216, 163, 79, 189, 127, 37, 237, 149, 164, 35, 234, 130, 129, 172, 21, 253, 164, 6, 240, 191, 77, 159, 164, 95, 85, 232, 147, 87, 99];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(7, dir);
        Assert.Equal(42061, variance);
    }
    [Fact]
    public void FindDirection_Case4_MatchesDav1d()
    {
        byte[] img = [83, 53, 241, 164, 227, 245, 3, 174, 23, 122, 118, 251, 137, 96, 136, 12, 132, 255, 144, 233, 196, 126, 173, 140, 134, 200, 57, 255, 240, 104, 150, 146, 150, 50, 81, 238, 248, 222, 157, 183, 17, 78, 140, 139, 122, 196, 195, 62, 27, 82, 208, 233, 2, 110, 158, 213, 135, 100, 30, 26, 187, 4, 190, 175];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(29155, variance);
    }
}
