// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// AV1 intra prediction modes for 8-bit samples (specification section 7.11.2). Predictors take the
/// reconstructed neighbour samples — the row above, the column to the left and the top-left corner —
/// and fill a width × height block in row-major order.
/// </summary>
internal static class Av1IntraPrediction
{
    /// <summary>DC prediction using both the above row and the left column.</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    /// <param name="left">The column of samples left of the block.</param>
    public static void DcPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> left)
    {
        uint sum = (uint)((width + height) >> 1);
        for (int x = 0; x < width; x++)
        {
            sum += above[x];
        }

        for (int y = 0; y < height; y++)
        {
            sum += left[y];
        }

        uint dc = sum >> BitOperations.TrailingZeroCount(width + height);
        if (width != height)
        {
            // Replace the division by (width + height) with a fixed-point multiply (8-bit constants).
            uint multiplier = (width > (height * 2)) || (height > (width * 2)) ? 0x3334u : 0x5556u;
            dc = (dc * multiplier) >> 16;
        }

        Splat(destination, stride, width, height, (ushort)dc);
    }

    /// <summary>DC prediction using only the above row.</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    public static void DcTopPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above)
    {
        uint sum = (uint)(width >> 1);
        for (int x = 0; x < width; x++)
        {
            sum += above[x];
        }

        Splat(destination, stride, width, height, (ushort)(sum >> BitOperations.TrailingZeroCount(width)));
    }

    /// <summary>DC prediction using only the left column.</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="left">The column of samples left of the block.</param>
    public static void DcLeftPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> left)
    {
        uint sum = (uint)(height >> 1);
        for (int y = 0; y < height; y++)
        {
            sum += left[y];
        }

        Splat(destination, stride, width, height, (ushort)(sum >> BitOperations.TrailingZeroCount(height)));
    }

    /// <summary>DC prediction with no available neighbours (mid-grey).</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void Dc128Predict(Span<ushort> destination, int stride, int width, int height, int bitDepth)
        => Splat(destination, stride, width, height, (ushort)(1 << (bitDepth - 1)));

    /// <summary>Vertical prediction: each row is a copy of the above row.</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    public static void VerticalPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above)
    {
        for (int y = 0; y < height; y++)
        {
            above[..width].CopyTo(destination.Slice(y * stride, width));
        }
    }

    /// <summary>Horizontal prediction: each row is filled with the corresponding left sample.</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="left">The column of samples left of the block.</param>
    public static void HorizontalPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> left)
    {
        for (int y = 0; y < height; y++)
        {
            destination.Slice(y * stride, width).Fill(left[y]);
        }
    }

    /// <summary>Paeth prediction.</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    /// <param name="left">The column of samples left of the block.</param>
    /// <param name="topLeft">The top-left corner sample.</param>
    public static void PaethPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> left, ushort topLeft)
    {
        for (int y = 0; y < height; y++)
        {
            int leftValue = left[y];
            Span<ushort> row = destination.Slice(y * stride, width);
            for (int x = 0; x < width; x++)
            {
                int top = above[x];
                int @base = leftValue + top - topLeft;
                int leftDiff = Math.Abs(leftValue - @base);
                int topDiff = Math.Abs(top - @base);
                int topLeftDiff = Math.Abs(topLeft - @base);

                row[x] = leftDiff <= topDiff && leftDiff <= topLeftDiff
                    ? (ushort)leftValue
                    : topDiff <= topLeftDiff ? (ushort)top : topLeft;
            }
        }
    }

    /// <summary>SMOOTH prediction (blends in both directions).</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    /// <param name="left">The column of samples left of the block.</param>
    public static void SmoothPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> left)
    {
        ReadOnlySpan<byte> weightsHorizontal = Av1SmoothWeights.Get(width);
        ReadOnlySpan<byte> weightsVertical = Av1SmoothWeights.Get(height);
        int right = above[width - 1];
        int bottom = left[height - 1];

        for (int y = 0; y < height; y++)
        {
            int wv = weightsVertical[y];
            Span<ushort> row = destination.Slice(y * stride, width);
            for (int x = 0; x < width; x++)
            {
                int wh = weightsHorizontal[x];
                int pred = (wv * above[x]) + ((256 - wv) * bottom) + (wh * left[y]) + ((256 - wh) * right);
                row[x] = (ushort)((pred + 256) >> 9);
            }
        }
    }

    /// <summary>SMOOTH_V prediction (blends vertically only).</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    /// <param name="left">The column of samples left of the block.</param>
    public static void SmoothVerticalPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> left)
    {
        ReadOnlySpan<byte> weightsVertical = Av1SmoothWeights.Get(height);
        int bottom = left[height - 1];

        for (int y = 0; y < height; y++)
        {
            int wv = weightsVertical[y];
            Span<ushort> row = destination.Slice(y * stride, width);
            for (int x = 0; x < width; x++)
            {
                int pred = (wv * above[x]) + ((256 - wv) * bottom);
                row[x] = (ushort)((pred + 128) >> 8);
            }
        }
    }

    /// <summary>SMOOTH_H prediction (blends horizontally only).</summary>
    /// <param name="destination">The destination block buffer.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="above">The row of samples above the block.</param>
    /// <param name="left">The column of samples left of the block.</param>
    public static void SmoothHorizontalPredict(Span<ushort> destination, int stride, int width, int height, ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> left)
    {
        ReadOnlySpan<byte> weightsHorizontal = Av1SmoothWeights.Get(width);
        int right = above[width - 1];

        for (int y = 0; y < height; y++)
        {
            Span<ushort> row = destination.Slice(y * stride, width);
            for (int x = 0; x < width; x++)
            {
                int wh = weightsHorizontal[x];
                int pred = (wh * left[y]) + ((256 - wh) * right);
                row[x] = (ushort)((pred + 128) >> 8);
            }
        }
    }

    private static void Splat(Span<ushort> destination, int stride, int width, int height, ushort value)
    {
        for (int y = 0; y < height; y++)
        {
            destination.Slice(y * stride, width).Fill(value);
        }
    }
}
