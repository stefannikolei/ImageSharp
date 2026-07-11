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
/// For transforms with a 64-sample dimension only the lowest 32 coefficients along that dimension
/// are coded; the higher coefficients are treated as zero, matching the AV1 specification.
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
    /// The dequantized coefficients in row-major order with a stride equal to the transform width
    /// (index <c>y * width + x</c>). Coefficients outside the coded 32x32 region must be zero.
    /// </param>
    /// <param name="residual">
    /// The destination residual buffer in row-major order; must be at least <c>width * height</c>.
    /// </param>
    /// <param name="bitDepth">The stream bit depth (8, 10 or 12).</param>
    public static void Reconstruct(
        Av1TransformType transformType,
        Av1TransformSize transformSize,
        ReadOnlySpan<int> coefficients,
        Span<int> residual,
        int bitDepth)
    {
        int w = transformSize.GetWidth();
        int h = transformSize.GetHeight();
        int sw = Math.Min(w, 32);
        int sh = Math.Min(h, 32);

        // The lossless Walsh-Hadamard transform (dav1d inv_txfm_add_wht_wht_4x4): the dequantized
        // coefficients carry a factor of four that the >> 2 removes, and the 1D passes are exact.
        if (transformType == Av1TransformType.WhtWht)
        {
            Span<int> wht = stackalloc int[16];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    wht[(y * 4) + x] = coefficients[(y * 4) + x] >> 2;
                }

                InverseWht4(wht.Slice(y * 4, 4), 1);
            }

            for (int x = 0; x < 4; x++)
            {
                InverseWht4(wht[x..], 4);
            }

            wht.CopyTo(residual);
            return;
        }

        int shift = RowShift[(int)transformSize];
        int round = (1 << shift) >> 1;
        bool isRect2 = (w * 2 == h) || (h * 2 == w);

        GetClampRange(bitDepth, out int rowClipMin, out int rowClipMax, out int colClipMin, out int colClipMax);

        Av1Transform1dType rowType = transformType.GetHorizontal();
        Av1Transform1dType columnType = transformType.GetVertical();

        int[] buffer = new int[w * h];
        Span<int> tmp = buffer;

        // Row (horizontal) pass over the coded rows; rows beyond sh remain zero.
        for (int y = 0; y < sh; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < sw; x++)
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
    // dav1d inv_wht4_1d.
    private static void InverseWht4(Span<int> c, int stride)
    {
        int in0 = c[0];
        int in1 = c[stride];
        int in2 = c[2 * stride];
        int in3 = c[3 * stride];

        int t0 = in0 + in1;
        int t2 = in2 - in3;
        int t4 = (t0 - t2) >> 1;
        int t3 = t4 - in3;
        int t1 = t4 - in1;

        c[0] = t0 - t3;
        c[stride] = t3;
        c[2 * stride] = t1;
        c[3 * stride] = t2 + t1;
    }

}
