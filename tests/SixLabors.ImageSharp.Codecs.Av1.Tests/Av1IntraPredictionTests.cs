// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1IntraPredictionTests
{
    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(16, 16)]
    [InlineData(32, 32)]
    [InlineData(4, 8)]
    [InlineData(8, 4)]
    [InlineData(4, 16)]
    [InlineData(16, 4)]
    [InlineData(8, 16)]
    public void Dc_MatchesAverageReference(int width, int height)
    {
        Random random = new((width * 100) + height);
        ushort[] above = RandomSamples(random, width);
        ushort[] left = RandomSamples(random, height);

        ushort[] block = new ushort[width * height];
        Av1IntraPrediction.DcPredict(block, width, width, height, above, left);

        int sum = 0;
        foreach (ushort b in above)
        {
            sum += b;
        }

        foreach (ushort b in left)
        {
            sum += b;
        }

        int expected = (sum + ((width + height) / 2)) / (width + height);
        foreach (ushort value in block)
        {
            Assert.Equal(expected, value);
        }
    }

    [Fact]
    public void DcTop_And_DcLeft_AverageOneSide()
    {
        ushort[] above = [10, 20, 30, 40];
        ushort[] left = [100, 100, 100, 100];
        ushort[] block = new ushort[16];

        Av1IntraPrediction.DcTopPredict(block, 4, 4, 4, above);
        Assert.All(block, v => Assert.Equal(25, v)); // (10+20+30+40+2)/4 = 25

        Av1IntraPrediction.DcLeftPredict(block, 4, 4, 4, left);
        Assert.All(block, v => Assert.Equal(100, v));
    }

    [Fact]
    public void Dc128_FillsMidGrey()
    {
        ushort[] block = new ushort[16];
        Av1IntraPrediction.Dc128Predict(block, 4, 4, 4, 8);
        Assert.All(block, v => Assert.Equal(128, v));
    }

    [Fact]
    public void Vertical_CopiesAboveRow()
    {
        ushort[] above = [1, 2, 3, 4];
        ushort[] block = new ushort[16];
        Av1IntraPrediction.VerticalPredict(block, 4, 4, 4, above);

        for (int y = 0; y < 4; y++)
        {
            Assert.Equal(above, block.AsSpan(y * 4, 4).ToArray());
        }
    }

    [Fact]
    public void Horizontal_FillsRowsWithLeft()
    {
        ushort[] left = [5, 6, 7, 8];
        ushort[] block = new ushort[16];
        Av1IntraPrediction.HorizontalPredict(block, 4, 4, 4, left);

        for (int y = 0; y < 4; y++)
        {
            Assert.All(block.AsSpan(y * 4, 4).ToArray(), v => Assert.Equal(left[y], v));
        }
    }

    [Fact]
    public void Paeth_PicksNearestPredictor()
    {
        // base = left + top - topLeft = 20 + 10 - 5 = 25; left is closest.
        ushort[] above = [10];
        ushort[] left = [20];
        ushort[] block = new ushort[1];
        Av1IntraPrediction.PaethPredict(block, 1, 1, 1, above, left, 5);
        Assert.Equal(20, block[0]);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(8, 16)]
    public void Paeth_MatchesReference(int width, int height)
    {
        Random random = new(width + height);
        ushort[] above = RandomSamples(random, width);
        ushort[] left = RandomSamples(random, height);
        ushort topLeft = (ushort)random.Next(256);

        ushort[] block = new ushort[width * height];
        Av1IntraPrediction.PaethPredict(block, width, width, height, above, left, topLeft);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int b = left[y] + above[x] - topLeft;
                int ld = Math.Abs(left[y] - b);
                int td = Math.Abs(above[x] - b);
                int tld = Math.Abs(topLeft - b);
                int expected = ld <= td && ld <= tld ? left[y] : (td <= tld ? above[x] : topLeft);
                Assert.Equal(expected, block[(y * width) + x]);
            }
        }
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(16, 16)]
    public void Smooth_UniformNeighbours_ReproducesValue(int width, int height)
    {
        // When every neighbour equals V, all SMOOTH blends collapse to V.
        const ushort v = 137;
        ushort[] above = CreateFilled(width, v);
        ushort[] left = CreateFilled(height, v);
        ushort[] block = new ushort[width * height];

        Av1IntraPrediction.SmoothPredict(block, width, width, height, above, left);
        Assert.All(block, value => Assert.Equal(v, value));

        Av1IntraPrediction.SmoothVerticalPredict(block, width, width, height, above, left);
        Assert.All(block, value => Assert.Equal(v, value));

        Av1IntraPrediction.SmoothHorizontalPredict(block, width, width, height, above, left);
        Assert.All(block, value => Assert.Equal(v, value));
    }

    [Fact]
    public void Predict_RespectsStride()
    {
        ushort[] above = [1, 2, 3, 4];
        const int stride = 7;
        ushort[] block = new ushort[stride * 4];
        block.AsSpan().Fill(99);

        Av1IntraPrediction.VerticalPredict(block, stride, 4, 4, above);

        for (int y = 0; y < 4; y++)
        {
            Assert.Equal(above, block.AsSpan(y * stride, 4).ToArray());
            Assert.Equal(99, block[(y * stride) + 4]); // padding untouched
        }
    }

    private static ushort[] RandomSamples(Random random, int count)
    {
        byte[] bytes = new byte[count];
        random.NextBytes(bytes);
        return Av1TestData.Widen(bytes);
    }

    private static ushort[] CreateFilled(int count, ushort value)
    {
        ushort[] data = new ushort[count];
        Array.Fill(data, value);
        return data;
    }
}
