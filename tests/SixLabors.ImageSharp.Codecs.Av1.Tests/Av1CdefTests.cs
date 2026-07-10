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
        ushort[] img = [247, 128, 213, 154, 26, 101, 177, 255, 15, 113, 154, 59, 81, 163, 20, 163, 187, 116, 146, 23, 51, 241, 145, 143, 239, 192, 78, 34, 169, 168, 94, 37, 145, 208, 42, 49, 93, 222, 254, 177, 214, 226, 255, 2, 78, 219, 144, 249, 94, 149, 62, 151, 154, 54, 205, 5, 238, 70, 170, 90, 23, 142, 85, 102];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(8917, variance);
    }
    [Fact]
    public void FindDirection_Case1_MatchesDav1d()
    {
        ushort[] img = [18, 117, 169, 128, 120, 227, 70, 221, 117, 224, 64, 81, 241, 143, 65, 20, 47, 85, 195, 20, 105, 199, 138, 91, 3, 36, 67, 23, 51, 164, 154, 72, 140, 152, 239, 216, 49, 15, 128, 123, 79, 97, 33, 186, 245, 124, 19, 94, 4, 150, 42, 194, 203, 65, 252, 75, 234, 78, 125, 84, 160, 46, 169, 209];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(41962, variance);
    }
    [Fact]
    public void FindDirection_Case2_MatchesDav1d()
    {
        ushort[] img = [69, 248, 196, 88, 160, 104, 226, 32, 212, 72, 197, 8, 165, 208, 102, 135, 16, 228, 18, 157, 121, 216, 125, 74, 155, 164, 238, 226, 146, 44, 219, 32, 184, 79, 116, 252, 183, 187, 190, 156, 180, 10, 237, 131, 124, 244, 165, 25, 232, 245, 67, 187, 27, 12, 88, 109, 123, 122, 188, 252, 121, 0, 141, 168];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(2774, variance);
    }
    [Fact]
    public void FindDirection_Case3_MatchesDav1d()
    {
        ushort[] img = [108, 164, 80, 86, 92, 105, 208, 85, 37, 118, 232, 110, 234, 250, 205, 167, 254, 213, 249, 156, 232, 31, 5, 171, 246, 7, 128, 60, 254, 194, 34, 179, 125, 69, 22, 216, 163, 79, 189, 127, 37, 237, 149, 164, 35, 234, 130, 129, 172, 21, 253, 164, 6, 240, 191, 77, 159, 164, 95, 85, 232, 147, 87, 99];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(7, dir);
        Assert.Equal(42061, variance);
    }
    [Fact]
    public void FindDirection_Case4_MatchesDav1d()
    {
        ushort[] img = [83, 53, 241, 164, 227, 245, 3, 174, 23, 122, 118, 251, 137, 96, 136, 12, 132, 255, 144, 233, 196, 126, 173, 140, 134, 200, 57, 255, 240, 104, 150, 146, 150, 50, 81, 238, 248, 222, 157, 183, 17, 78, 140, 139, 122, 196, 195, 62, 27, 82, 208, 233, 2, 110, 158, 213, 135, 100, 30, 26, 187, 4, 190, 175];
        int dir = Av1Cdef.FindDirection(img, 0, 8, out int variance);
        Assert.Equal(4, dir);
        Assert.Equal(29155, variance);
    }

    [Fact]
    public void FilterBlock_Case0_W8H8Pri6Sec0_MatchesDav1d()
    {
        int w = 8, h = 8, stride = 10;
        ushort[] dst = [101, 249, 115, 183, 124, 131, 47, 233, 253, 204, 98, 12, 171, 221, 218, 83, 143, 219, 8, 177, 79, 96, 124, 16, 204, 67, 62, 61, 215, 129, 146, 250, 123, 255, 62, 194, 17, 17, 105, 152, 70, 246, 95, 97, 205, 248, 162, 31, 206, 109, 55, 24, 47, 154, 214, 134, 187, 141, 79, 106, 46, 110, 18, 166, 77, 24, 209, 153, 87, 91, 90, 83, 148, 179, 61, 184, 213, 192, 59, 210];
        ushort[] left = [159, 235, 8, 110, 134, 249, 122, 159, 83, 172, 61, 91, 113, 106, 217, 51];
        ushort[] top = [106, 180, 105, 101, 237, 160, 93, 255, 47, 146, 226, 238, 251, 187, 193, 6, 106, 23, 37, 141, 92, 60, 233, 155];
        ushort[] bottom = [138, 91, 251, 154, 43, 201, 36, 150, 13, 30, 70, 149, 208, 151, 145, 115, 217, 65, 221, 190, 44, 117, 152, 81];
        ushort[] reference = [102, 249, 115, 182, 125, 131, 48, 234, 99, 13, 172, 221, 218, 83, 143, 219, 80, 96, 124, 16, 204, 66, 62, 61, 146, 250, 123, 255, 63, 195, 16, 17, 70, 246, 95, 97, 204, 248, 162, 30, 55, 24, 47, 154, 214, 134, 187, 142, 47, 109, 18, 165, 77, 25, 209, 153, 91, 83, 149, 179, 61, 184, 214, 192];
        Av1Cdef.FilterBlock(dst, 0, stride, left, top, bottom, 6, 0, 0, 4, w, h,
            Av1Cdef.EdgeFlags.Left | Av1Cdef.EdgeFlags.Right | Av1Cdef.EdgeFlags.Top | Av1Cdef.EdgeFlags.Bottom);
        ushort[] result = new ushort[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                result[(y * w) + x] = dst[(y * stride) + x];
            }
        }

        Assert.True(result.AsSpan().SequenceEqual(reference), "CDEF filter output differs from dav1d.");
    }
    [Fact]
    public void FilterBlock_Case1_W8H8Pri0Sec2_MatchesDav1d()
    {
        int w = 8, h = 8, stride = 10;
        ushort[] dst = [8, 123, 149, 147, 107, 106, 90, 159, 184, 178, 212, 8, 42, 116, 198, 101, 109, 162, 234, 221, 79, 189, 128, 44, 192, 129, 21, 152, 2, 206, 116, 212, 234, 99, 178, 15, 176, 162, 14, 242, 139, 132, 217, 19, 124, 126, 183, 82, 243, 145, 86, 15, 27, 92, 249, 195, 174, 63, 12, 222, 95, 5, 115, 172, 41, 114, 236, 246, 29, 151, 129, 193, 56, 226, 174, 18, 218, 199, 214, 154];
        ushort[] left = [132, 119, 170, 119, 39, 97, 134, 105, 111, 212, 112, 231, 166, 56, 22, 160];
        ushort[] top = [248, 30, 186, 22, 53, 222, 175, 125, 162, 147, 61, 211, 20, 129, 3, 93, 110, 147, 177, 23, 44, 88, 101, 31];
        ushort[] bottom = [122, 108, 234, 57, 153, 133, 250, 195, 189, 227, 34, 37, 161, 84, 199, 125, 75, 22, 78, 195, 137, 76, 42, 172];
        ushort[] reference = [8, 123, 149, 147, 107, 106, 90, 159, 212, 8, 42, 116, 198, 101, 109, 162, 79, 189, 128, 44, 192, 129, 21, 152, 116, 212, 234, 99, 178, 15, 176, 162, 139, 132, 217, 19, 124, 126, 183, 82, 86, 15, 27, 92, 249, 195, 174, 63, 95, 5, 115, 172, 41, 114, 236, 246, 129, 193, 56, 226, 174, 18, 218, 199];
        Av1Cdef.FilterBlock(dst, 0, stride, left, top, bottom, 0, 2, 0, 4, w, h,
            Av1Cdef.EdgeFlags.Left | Av1Cdef.EdgeFlags.Right | Av1Cdef.EdgeFlags.Top | Av1Cdef.EdgeFlags.Bottom);
        ushort[] result = new ushort[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                result[(y * w) + x] = dst[(y * stride) + x];
            }
        }

        Assert.True(result.AsSpan().SequenceEqual(reference), "CDEF filter output differs from dav1d.");
    }
    [Fact]
    public void FilterBlock_Case2_W8H8Pri6Sec2_MatchesDav1d()
    {
        int w = 8, h = 8, stride = 10;
        ushort[] dst = [87, 113, 108, 173, 193, 107, 245, 191, 243, 130, 72, 6, 164, 205, 33, 81, 147, 232, 226, 101, 31, 109, 252, 25, 17, 67, 207, 121, 97, 214, 27, 26, 13, 98, 143, 88, 113, 6, 221, 132, 102, 175, 38, 74, 100, 109, 171, 4, 54, 43, 21, 49, 20, 251, 198, 185, 6, 199, 251, 246, 43, 113, 217, 46, 66, 4, 15, 174, 110, 9, 121, 81, 70, 147, 177, 175, 87, 172, 168, 95];
        ushort[] left = [37, 147, 134, 189, 28, 150, 79, 116, 170, 30, 10, 121, 180, 155, 67, 137];
        ushort[] top = [212, 86, 10, 145, 108, 124, 209, 237, 42, 134, 202, 68, 241, 145, 229, 86, 149, 131, 194, 34, 174, 7, 213, 161];
        ushort[] bottom = [140, 194, 119, 208, 31, 80, 122, 220, 55, 17, 34, 101, 219, 213, 178, 177, 20, 38, 45, 32, 234, 124, 25, 45];
        ushort[] reference = [87, 113, 109, 172, 194, 107, 245, 191, 73, 6, 165, 204, 32, 81, 146, 233, 31, 109, 252, 25, 17, 68, 207, 120, 27, 26, 15, 98, 144, 89, 113, 6, 103, 175, 40, 73, 99, 110, 172, 5, 20, 47, 20, 251, 198, 184, 5, 199, 44, 114, 217, 46, 66, 5, 15, 174, 120, 81, 70, 147, 177, 175, 87, 172];
        Av1Cdef.FilterBlock(dst, 0, stride, left, top, bottom, 6, 2, 0, 4, w, h,
            Av1Cdef.EdgeFlags.Left | Av1Cdef.EdgeFlags.Right | Av1Cdef.EdgeFlags.Top | Av1Cdef.EdgeFlags.Bottom);
        ushort[] result = new ushort[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                result[(y * w) + x] = dst[(y * stride) + x];
            }
        }

        Assert.True(result.AsSpan().SequenceEqual(reference), "CDEF filter output differs from dav1d.");
    }
    [Fact]
    public void FilterBlock_Case3_W4H4Pri6Sec2_MatchesDav1d()
    {
        int w = 4, h = 4, stride = 6;
        ushort[] dst = [128, 11, 32, 225, 198, 42, 11, 145, 192, 107, 253, 171, 83, 17, 251, 232, 145, 162, 231, 153, 208, 175, 14, 255];
        ushort[] left = [184, 139, 30, 128, 46, 81, 62, 190];
        ushort[] top = [227, 69, 67, 69, 1, 190, 202, 7, 52, 137, 195, 242, 75, 54, 133, 27];
        ushort[] bottom = [42, 243, 91, 40, 66, 53, 227, 56, 141, 180, 46, 6, 163, 71, 26, 123];
        ushort[] reference = [128, 11, 32, 225, 11, 145, 192, 107, 83, 17, 251, 232, 232, 153, 208, 174];
        Av1Cdef.FilterBlock(dst, 0, stride, left, top, bottom, 6, 2, 0, 4, w, h,
            Av1Cdef.EdgeFlags.Left | Av1Cdef.EdgeFlags.Right | Av1Cdef.EdgeFlags.Top | Av1Cdef.EdgeFlags.Bottom);
        ushort[] result = new ushort[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                result[(y * w) + x] = dst[(y * stride) + x];
            }
        }

        Assert.True(result.AsSpan().SequenceEqual(reference), "CDEF filter output differs from dav1d.");
    }
}
