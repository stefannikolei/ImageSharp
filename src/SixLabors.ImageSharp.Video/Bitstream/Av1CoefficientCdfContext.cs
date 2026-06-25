// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Holds the mutable, adaptive coefficient CDFs for a single tile, initialized from the default
/// tables for the tile's quantizer context. The coefficient reader adapts these CDFs as it decodes;
/// a tile keeps its own copy so adaptation does not leak across tiles.
/// </summary>
/// <remarks>
/// Each CDF is a fresh, mutable array in the inverse-CDF layout used by <see cref="Av1SymbolDecoder"/>
/// (boundaries, a terminal 0 and an adaptation counter). Multi-dimensional groups are stored as
/// jagged arrays flattened in row-major order; the accessors compute the flat index.
/// </remarks>
internal sealed class Av1CoefficientCdfContext
{
    private Av1CoefficientCdfContext()
    {
    }

    /// <summary>Gets the txb_skip CDFs, indexed by <c>[txSize * 13 + ctx]</c>.</summary>
    public ushort[][] Skip { get; private set; } = default!;

    /// <summary>Gets the dc_sign CDFs, indexed by <c>[plane * 3 + ctx]</c>.</summary>
    public ushort[][] DcSign { get; private set; } = default!;

    /// <summary>Gets the eob_hi_bit CDFs, indexed by <c>[(txSize * 2 + plane) * 9 + ctx]</c>.</summary>
    public ushort[][] EobHighBit { get; private set; } = default!;

    /// <summary>Gets the coeff_base CDFs, indexed by <c>[(txSize * 2 + plane) * 41 + ctx]</c>.</summary>
    public ushort[][] BaseToken { get; private set; } = default!;

    /// <summary>Gets the coeff_base_range CDFs, indexed by <c>[(set * 2 + plane) * 21 + ctx]</c>.</summary>
    public ushort[][] BaseRange { get; private set; } = default!;

    /// <summary>Gets the eob_base_tok CDFs, indexed by <c>[(txSize * 2 + plane) * 4 + ctx]</c>.</summary>
    public ushort[][] EobBaseToken { get; private set; } = default!;

    /// <summary>Gets the eob_bin CDFs for transform-size context 0..6, indexed by <c>[plane * 2 + ctx]</c> (or <c>[plane]</c> for 512/1024).</summary>
    public ushort[][][] EobBin { get; private set; } = default!;

    /// <summary>
    /// Creates a coefficient CDF context for the given quantizer context, copying the default tables.
    /// </summary>
    /// <param name="quantizerContext">The quantizer context in the range [0, 3].</param>
    /// <returns>A fresh, mutable coefficient CDF context.</returns>
    public static Av1CoefficientCdfContext CreateDefault(int quantizerContext)
    {
        int q = quantizerContext;
        Av1CoefficientCdfContext context = new()
        {
            Skip = BoundaryGroup(Av1DefaultCoefficientCdf.Skip, q, 5, 13),
            DcSign = BoundaryGroup(Av1DefaultCoefficientCdf.DcSign, q, 2, 3),
            EobHighBit = BoundaryGroup4(Av1DefaultCoefficientCdf.EobHighBit, q, 5, 2, 9),
            BaseToken = VectorGroup5(Av1DefaultCoefficientCdf.BaseToken, q, 5, 2, 41, 5),
            BaseRange = VectorGroup5(Av1DefaultCoefficientCdf.BaseRange, q, 4, 2, 21, 5),
            EobBaseToken = VectorGroup5(Av1DefaultCoefficientCdf.EobBaseToken, q, 5, 2, 4, 4),
            EobBin =
            [
                VectorGroup4(Av1DefaultCoefficientCdf.EobBin16, q, 2, 2, 6),
                VectorGroup4(Av1DefaultCoefficientCdf.EobBin32, q, 2, 2, 7),
                VectorGroup4(Av1DefaultCoefficientCdf.EobBin64, q, 2, 2, 8),
                VectorGroup4(Av1DefaultCoefficientCdf.EobBin128, q, 2, 2, 9),
                VectorGroup4(Av1DefaultCoefficientCdf.EobBin256, q, 2, 2, 10),
                VectorGroup3(Av1DefaultCoefficientCdf.EobBin512, q, 2, 11),
                VectorGroup3(Av1DefaultCoefficientCdf.EobBin1024, q, 2, 12),
            ],
        };

        return context;
    }

    /// <summary>
    /// Creates an independent deep copy of this context.
    /// </summary>
    /// <returns>The cloned context.</returns>
    public Av1CoefficientCdfContext Clone() => new()
    {
        Skip = CloneGroup(this.Skip),
        DcSign = CloneGroup(this.DcSign),
        EobHighBit = CloneGroup(this.EobHighBit),
        BaseToken = CloneGroup(this.BaseToken),
        BaseRange = CloneGroup(this.BaseRange),
        EobBaseToken = CloneGroup(this.EobBaseToken),
        EobBin = [.. this.EobBin.Select(CloneGroup)],
    };

    private static ushort[][] BoundaryGroup(ushort[,,] table, int q, int d1, int d2)
    {
        ushort[][] result = new ushort[d1 * d2][];
        for (int a = 0; a < d1; a++)
        {
            for (int b = 0; b < d2; b++)
            {
                result[(a * d2) + b] = [table[q, a, b], 0, 0];
            }
        }

        return result;
    }

    private static ushort[][] BoundaryGroup4(ushort[,,,] table, int q, int d1, int d2, int d3)
    {
        ushort[][] result = new ushort[d1 * d2 * d3][];
        for (int a = 0; a < d1; a++)
        {
            for (int b = 0; b < d2; b++)
            {
                for (int c = 0; c < d3; c++)
                {
                    result[((a * d2 * d3) + (b * d3)) + c] = [table[q, a, b, c], 0, 0];
                }
            }
        }

        return result;
    }

    private static ushort[][] VectorGroup5(ushort[,,,,] table, int q, int d1, int d2, int d3, int inner)
    {
        ushort[][] result = new ushort[d1 * d2 * d3][];
        for (int a = 0; a < d1; a++)
        {
            for (int b = 0; b < d2; b++)
            {
                for (int c = 0; c < d3; c++)
                {
                    ushort[] cdf = new ushort[inner];
                    for (int i = 0; i < inner; i++)
                    {
                        cdf[i] = table[q, a, b, c, i];
                    }

                    result[(((a * d2) + b) * d3) + c] = cdf;
                }
            }
        }

        return result;
    }

    private static ushort[][] VectorGroup4(ushort[,,,] table, int q, int d1, int d2, int inner)
    {
        ushort[][] result = new ushort[d1 * d2][];
        for (int a = 0; a < d1; a++)
        {
            for (int b = 0; b < d2; b++)
            {
                ushort[] cdf = new ushort[inner];
                for (int i = 0; i < inner; i++)
                {
                    cdf[i] = table[q, a, b, i];
                }

                result[(a * d2) + b] = cdf;
            }
        }

        return result;
    }

    private static ushort[][] VectorGroup3(ushort[,,] table, int q, int d1, int inner)
    {
        ushort[][] result = new ushort[d1][];
        for (int a = 0; a < d1; a++)
        {
            ushort[] cdf = new ushort[inner];
            for (int i = 0; i < inner; i++)
            {
                cdf[i] = table[q, a, i];
            }

            result[a] = cdf;
        }

        return result;
    }

    private static ushort[][] CloneGroup(ushort[][] group)
    {
        ushort[][] result = new ushort[group.Length][];
        for (int i = 0; i < group.Length; i++)
        {
            result[i] = (ushort[])group[i].Clone();
        }

        return result;
    }
}
