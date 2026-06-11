// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1ScanOrderTests
{
    [Theory]
    [InlineData((int)Av1TransformSize.Size4x4)]
    [InlineData((int)Av1TransformSize.Size8x8)]
    [InlineData((int)Av1TransformSize.Size16x16)]
    [InlineData((int)Av1TransformSize.Size32x32)]
    [InlineData((int)Av1TransformSize.Size64x64)]
    [InlineData((int)Av1TransformSize.Size4x8)]
    [InlineData((int)Av1TransformSize.Size8x4)]
    [InlineData((int)Av1TransformSize.Size8x16)]
    [InlineData((int)Av1TransformSize.Size16x8)]
    [InlineData((int)Av1TransformSize.Size16x32)]
    [InlineData((int)Av1TransformSize.Size32x16)]
    [InlineData((int)Av1TransformSize.Size32x64)]
    [InlineData((int)Av1TransformSize.Size64x32)]
    [InlineData((int)Av1TransformSize.Size4x16)]
    [InlineData((int)Av1TransformSize.Size16x4)]
    [InlineData((int)Av1TransformSize.Size8x32)]
    [InlineData((int)Av1TransformSize.Size32x8)]
    [InlineData((int)Av1TransformSize.Size16x64)]
    [InlineData((int)Av1TransformSize.Size64x16)]
    public void GetScan_IsPermutationOfCodedRegion(int sizeValue)
    {
        Av1TransformSize size = (Av1TransformSize)sizeValue;
        int codedWidth = Math.Min(size.GetWidth(), 32);
        int codedHeight = Math.Min(size.GetHeight(), 32);
        int expectedLength = codedWidth * codedHeight;

        ReadOnlySpan<ushort> scan = Av1ScanOrder.GetScan(size);

        Assert.Equal(expectedLength, scan.Length);
        Assert.Equal(0, scan[0]); // DC is always first.

        bool[] seen = new bool[expectedLength];
        foreach (ushort position in scan)
        {
            Assert.InRange((int)position, 0, expectedLength - 1);
            Assert.False(seen[position], $"Position {position} appears twice.");
            seen[position] = true;
        }
    }

    [Fact]
    public void GetScan_64Sizes_ReuseCodedRegionScan()
    {
        // 64-sample dimensions reuse the 32-wide/32-tall scan of the coded 32x32 region.
        Assert.True(Av1ScanOrder.GetScan(Av1TransformSize.Size64x64).SequenceEqual(Av1ScanOrder.GetScan(Av1TransformSize.Size32x32)));
        Assert.True(Av1ScanOrder.GetScan(Av1TransformSize.Size32x64).SequenceEqual(Av1ScanOrder.GetScan(Av1TransformSize.Size32x32)));
        Assert.True(Av1ScanOrder.GetScan(Av1TransformSize.Size16x64).SequenceEqual(Av1ScanOrder.GetScan(Av1TransformSize.Size16x32)));
        Assert.True(Av1ScanOrder.GetScan(Av1TransformSize.Size64x16).SequenceEqual(Av1ScanOrder.GetScan(Av1TransformSize.Size32x16)));
    }

    [Fact]
    public void GetScan_4x4_MatchesReference()
    {
        ushort[] expected = [0, 4, 1, 2, 5, 8, 12, 9, 6, 3, 7, 10, 13, 14, 11, 15];
        Assert.True(Av1ScanOrder.GetScan(Av1TransformSize.Size4x4).SequenceEqual(expected));
    }

    [Fact]
    public void GetScan_4x4_DecodesAsUpRightDiagonal()
    {
        // Scan values are column-major (row + col * height); the 4x4 scan follows the up-right
        // diagonal: (0,0) -> (0,1) -> (1,0) -> ...
        ReadOnlySpan<ushort> scan = Av1ScanOrder.GetScan(Av1TransformSize.Size4x4);
        const int height = 4;

        (int Row, int Col) Decode(int rc) => (rc % height, rc / height);

        Assert.Equal((0, 0), Decode(scan[0]));
        Assert.Equal((0, 1), Decode(scan[1]));
        Assert.Equal((1, 0), Decode(scan[2]));
    }
}
