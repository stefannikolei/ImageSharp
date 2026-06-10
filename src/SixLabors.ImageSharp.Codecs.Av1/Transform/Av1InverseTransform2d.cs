// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// The separable 2D inverse transform that turns a block of dequantized coefficients into spatial
/// residuals (specification section 7.13.3). The row (horizontal) transform is applied first,
/// followed by an intermediate rounding shift, then the column (vertical) transform and a final
/// rounding shift of 4.
/// </summary>
/// <remarks>
/// Transform sizes with a 64-sample dimension are not yet supported (they require the
/// 32-coefficient zeroing path) and will be added alongside the coefficient reader.
/// </remarks>
internal static class Av1InverseTransform2d
{
    // Intermediate (row) rounding shift per transform size, indexed by Av1TransformSize.
    private static readonly int[] RowShift =
        [0, 1, 2, 2, 2, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2];

    private const int FinalShift = 4;

    /// <summary>
    /// Reconstructs the spatial residuals for a transform block.
    /// </summary>
    /// <param name="transformType">The 2D transform type.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="coefficients">
    /// The dequantized coefficients in row-major order (index <c>y * width + x</c>).
    /// </param>
    /// <param name="residual">
    /// The destination residual buffer in row-major order; must be at least <c>width * height</c>.
    /// </param>
    /// <param name="bitDepth">The stream bit depth (8, 10 or 12).</param>
    /// <exception cref="NotSupportedException">A 64-sample transform dimension was requested.</exception>
    public static void Reconstruct(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        ReadOnlySpan<int> coefficients,
        Span<int> residual,
        int bitDepth)
    {
        int w = transformSize.GetWidth();
        int h = transformSize.GetHeight();
        if (w == 64 || h == 64)
        {
            throw new NotSupportedException("64-sample transform dimensions are not yet supported.");
        }

        int shift = RowShift[(int)transformSize];
        int round = (1 << shift) >> 1;
        bool isRect2 = (w * 2 == h) || (h * 2 == w);

        GetClampRange(bitDepth, out int rowClipMin, out int rowClipMax, out int colClipMin, out int colClipMax);

        Av1Transform1dType rowType = transformType.GetHorizontal();
        Av1Transform1dType columnType = transformType.GetVertical();

        Span<int> tmp = w * h <= 1024 ? stackalloc int[w * h] : new int[w * h];

        // Row (horizontal) pass.
        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                int coefficient = coefficients[rowOffset + x];
                tmp[rowOffset + x] = isRect2 ? (((coefficient * 181) + 128) >> 8) : coefficient;
            }

            Av1InverseTransform1d.Apply(rowType, w, tmp, rowOffset, 1, rowClipMin, rowClipMax);
        }

        // Intermediate rounding and clamping.
        for (int i = 0; i < w * h; i++)
        {
            tmp[i] = Clamp((tmp[i] + round) >> shift, colClipMin, colClipMax);
        }

        // Column (vertical) pass.
        for (int x = 0; x < w; x++)
        {
            Av1InverseTransform1d.Apply(columnType, h, tmp, x, w, colClipMin, colClipMax);
        }

        // Final rounding shift.
        for (int i = 0; i < w * h; i++)
        {
            residual[i] = (tmp[i] + 8) >> FinalShift;
        }
    }

    private static void GetClampRange(int bitDepth, out int rowMin, out int rowMax, out int colMin, out int colMax)
    {
        if (bitDepth == 8)
        {
            rowMin = short.MinValue;
            colMin = short.MinValue;
        }
        else
        {
            int bitDepthMax = (1 << bitDepth) - 1;
            rowMin = (~bitDepthMax) << 7;
            colMin = (~bitDepthMax) << 5;
        }

        rowMax = ~rowMin;
        colMax = ~colMin;
    }

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}
