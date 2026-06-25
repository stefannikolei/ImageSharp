// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Helpers for the cumulative distribution function (CDF) tables used by the AV1 symbol coder.
/// </summary>
/// <remarks>
/// CDFs are stored in the "inverse" (icdf) form used by the AV1 reference implementation: for a
/// symbol with <c>n</c> values the array has <c>n + 1</c> entries, where entries <c>0..n-1</c> are
/// <c>32768 - cumulativeProbability</c> (a strictly decreasing sequence ending in 0 at index
/// <c>n-1</c>) and entry <c>n</c> is the adaptation counter.
/// </remarks>
internal static class Av1Cdf
{
    /// <summary>
    /// The total probability scale used by the coder (Q15).
    /// </summary>
    public const int ProbabilityTop = 1 << 15;

    // Adaptation speed per symbol count: Min(FloorLog2(nsymbs), 2). Index by nsymbs (max 16).
    private static readonly int[] NsymbsToSpeed =
        [0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2];

    /// <summary>
    /// Creates a uniform icdf table for a symbol with <paramref name="nsymbs"/> values.
    /// </summary>
    /// <param name="nsymbs">The number of symbol values (in the range [2, 16]).</param>
    /// <returns>An icdf array of length <paramref name="nsymbs"/> + 1.</returns>
    public static ushort[] CreateUniform(int nsymbs)
    {
        DebugGuard.MustBeBetweenOrEqualTo(nsymbs, 2, 16, nameof(nsymbs));

        ushort[] cdf = new ushort[nsymbs + 1];
        for (int i = 0; i < nsymbs - 1; i++)
        {
            int cumulative = (int)(((long)(i + 1) * ProbabilityTop) / nsymbs);
            cdf[i] = (ushort)(ProbabilityTop - cumulative);
        }

        // cdf[nsymbs - 1] (terminal boundary) and cdf[nsymbs] (counter) are left at 0.
        return cdf;
    }

    /// <summary>
    /// Adapts a CDF towards the most recently decoded symbol, matching the AV1 reference
    /// implementation (specification section 8.3.2).
    /// </summary>
    /// <param name="cdf">The icdf array to update.</param>
    /// <param name="symbol">The decoded symbol value.</param>
    /// <param name="nsymbs">The number of symbol values.</param>
    public static void Update(Span<ushort> cdf, int symbol, int nsymbs)
    {
        ushort count = cdf[nsymbs];
        int rate = 3 + (count > 15 ? 1 : 0) + (count > 31 ? 1 : 0) + NsymbsToSpeed[nsymbs];

        int tmp = ProbabilityTop;
        for (int i = 0; i < nsymbs - 1; i++)
        {
            tmp = (i == symbol) ? 0 : tmp;
            if (tmp < cdf[i])
            {
                cdf[i] -= (ushort)((cdf[i] - tmp) >> rate);
            }
            else
            {
                cdf[i] += (ushort)((tmp - cdf[i]) >> rate);
            }
        }

        cdf[nsymbs] = (ushort)(count + (count < 32 ? 1 : 0));
    }
}
