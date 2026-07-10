// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// Chroma-from-luma (CfL) prediction (specification section 7.11.5), a port of dav1d's <c>cfl_ac</c> and
/// <c>cfl_pred</c> for 8-bit samples. The reconstructed luma block is subsampled to the chroma
/// resolution and reduced to a zero-mean AC contribution, which is then scaled by a signed alpha and
/// added to the chroma DC prediction.
/// </summary>
internal static class Av1ChromaFromLuma
{
    /// <summary>
    /// Computes the zero-mean luma AC contribution at chroma resolution (a port of dav1d's
    /// <c>cfl_ac</c>) for a fully-populated block (no edge padding).
    /// </summary>
    /// <param name="luma">The reconstructed luma plane.</param>
    /// <param name="lumaOffset">The offset of the block's top-left luma sample.</param>
    /// <param name="lumaStride">The luma plane stride.</param>
    /// <param name="chromaWidth">The chroma block width.</param>
    /// <param name="chromaHeight">The chroma block height.</param>
    /// <param name="subsamplingX">1 when the horizontal dimension is subsampled.</param>
    /// <param name="subsamplingY">1 when the vertical dimension is subsampled.</param>
    /// <param name="ac">Receives the AC contribution (chromaWidth*chromaHeight, row-major).</param>
    public static void ComputeAc(
        ReadOnlySpan<ushort> luma,
        int lumaOffset,
        int lumaStride,
        int chromaWidth,
        int chromaHeight,
        int subsamplingX,
        int subsamplingY,
        Span<int> ac)
    {
        int shift = 1 + (subsamplingY == 0 ? 1 : 0) + (subsamplingX == 0 ? 1 : 0);

        int row = lumaOffset;
        for (int y = 0; y < chromaHeight; y++)
        {
            for (int x = 0; x < chromaWidth; x++)
            {
                int sum = luma[row + (x << subsamplingX)];
                if (subsamplingX != 0)
                {
                    sum += luma[row + (x * 2) + 1];
                }

                if (subsamplingY != 0)
                {
                    sum += luma[row + (x << subsamplingX) + lumaStride];
                    if (subsamplingX != 0)
                    {
                        sum += luma[row + (x * 2) + 1 + lumaStride];
                    }
                }

                ac[(y * chromaWidth) + x] = sum << shift;
            }

            row += lumaStride << subsamplingY;
        }

        int log2Size = Log2(chromaWidth) + Log2(chromaHeight);
        long total = (1L << log2Size) >> 1;
        for (int i = 0; i < chromaWidth * chromaHeight; i++)
        {
            total += ac[i];
        }

        int mean = (int)(total >> log2Size);
        for (int i = 0; i < chromaWidth * chromaHeight; i++)
        {
            ac[i] -= mean;
        }
    }

    /// <summary>
    /// Predicts a chroma block from the DC prediction and the luma AC contribution (a port of dav1d's
    /// <c>cfl_pred</c>).
    /// </summary>
    /// <param name="dc">The chroma DC prediction value.</param>
    /// <param name="alpha">The signed CfL alpha.</param>
    /// <param name="ac">The luma AC contribution (width*height, row-major).</param>
    /// <param name="width">The chroma block width.</param>
    /// <param name="height">The chroma block height.</param>
    /// <param name="destination">The prediction output buffer (width*height, row-major).</param>
    public static void Predict(int dc, int alpha, ReadOnlySpan<int> ac, int width, int height, Span<ushort> destination)
    {
        for (int i = 0; i < width * height; i++)
        {
            int diff = alpha * ac[i];
            int magnitude = (Math.Abs(diff) + 32) >> 6;
            int signed = diff < 0 ? -magnitude : magnitude;
            destination[i] = (ushort)Math.Clamp(dc + signed, 0, 255);
        }
    }

    private static int Log2(int value) => System.Numerics.BitOperations.Log2((uint)value);
}
