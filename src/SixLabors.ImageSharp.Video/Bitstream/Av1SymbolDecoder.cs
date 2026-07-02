// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The AV1 multi-symbol arithmetic (range) decoder, as defined in the AV1 specification section 8.2
/// ("Symbol decoding process"). This is a port of the reference Daala entropy decoder used by AV1.
/// </summary>
internal sealed class Av1SymbolDecoder
{
    private const int WindowSize = 32;
    private const int ProbabilityShift = 6; // EC_PROB_SHIFT
    private const int MinProbability = 4;   // EC_MIN_PROB
    private const int LotsOfBits = 0x4000;

    private readonly ReadOnlyMemory<byte> data;
    private int position;
    private uint difference;
    private uint range;
    private int count;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolDecoder"/> class.
    /// </summary>
    /// <param name="data">The tile/partition payload to decode symbols from.</param>
    public Av1SymbolDecoder(ReadOnlyMemory<byte> data)
    {
        this.data = data;
        this.position = 0;
        this.difference = (1u << (WindowSize - 1)) - 1;
        this.range = 0x8000;
        this.count = -15;
        this.Refill();
    }

    /// <summary>
    /// Gets the current arithmetic-decoder range, matching dav1d's <c>msac.rng</c>. Exposed to allow
    /// bit-exact validation of the decode against a reference trace.
    /// </summary>
    public uint Range => this.range;

    /// <summary>
    /// Decodes a symbol using the supplied adaptive CDF and updates the CDF.
    /// </summary>
    /// <param name="cdf">The icdf array of length <c>nsymbs + 1</c>.</param>
    /// <returns>The decoded symbol value.</returns>
    public int ReadSymbol(Span<ushort> cdf)
    {
        int nsymbs = cdf.Length - 1;
        int symbol = this.DecodeCdf(cdf, nsymbs);
        Av1Cdf.Update(cdf, symbol, nsymbs);
        return symbol;
    }

    /// <summary>
    /// Decodes a symbol using the supplied CDF without adapting it.
    /// </summary>
    /// <param name="cdf">The icdf array of length <c>nsymbs + 1</c>.</param>
    /// <returns>The decoded symbol value.</returns>
    public int ReadSymbolNoUpdate(ReadOnlySpan<ushort> cdf) => this.DecodeCdf(cdf, cdf.Length - 1);

    /// <summary>
    /// Decodes a single equiprobable bit.
    /// </summary>
    /// <returns>The decoded bit, either 0 or 1.</returns>
    public int ReadBool()
    {
        ReadOnlySpan<ushort> cdf = [1 << 14, 0, 0];
        return this.ReadSymbolNoUpdate(cdf);
    }

    /// <summary>
    /// Decodes a boolean with the given fixed Q15 probability without adaptation, matching
    /// <c>dav1d_msac_decode_bool</c> (a two-symbol decode over an inverse CDF is arithmetically
    /// identical).
    /// </summary>
    /// <param name="probability">The Q15 probability of decoding one.</param>
    /// <returns>The decoded bit.</returns>
    public int ReadBool(uint probability)
    {
        ReadOnlySpan<ushort> cdf = [(ushort)probability, 0, 0];
        return this.ReadSymbolNoUpdate(cdf);
    }

    /// <summary>
    /// Decodes <paramref name="numBits"/> equiprobable bits, most significant bit first.
    /// </summary>
    /// <param name="numBits">The number of bits to read.</param>
    /// <returns>The decoded value.</returns>
    public uint ReadLiteral(int numBits)
    {
        uint value = 0;
        for (int i = 0; i < numBits; i++)
        {
            value = (value << 1) | (uint)this.ReadBool();
        }

        return value;
    }

    /// <summary>
    /// Reads an Exp-Golomb coded value, as used by the coefficient base-range syntax
    /// (specification <c>read_golomb</c>).
    /// </summary>
    /// <returns>The decoded value.</returns>
    public uint ReadGolomb()
    {
        int leadingZeros = 0;
        while (this.ReadBool() == 0)
        {
            leadingZeros++;
        }

        uint value = 1;
        for (int i = 0; i < leadingZeros; i++)
        {
            value = (value << 1) | (uint)this.ReadBool();
        }

        return value - 1;
    }

    private int DecodeCdf(ReadOnlySpan<ushort> icdf, int nsymbs)
    {
        uint r = this.range;
        int n = nsymbs - 1;
        uint c = this.difference >> (WindowSize - 16);
        uint v = r;
        uint u;
        int symbol = -1;
        do
        {
            u = v;
            symbol++;
            v = ((r >> 8) * (uint)(icdf[symbol] >> ProbabilityShift)) >> (7 - ProbabilityShift);
            v += (uint)(MinProbability * (n - symbol));
        }
        while (c < v);

        this.Normalize(this.difference - (v << (WindowSize - 16)), u - v);
        return symbol;
    }

    private void Normalize(uint newDifference, uint newRange)
    {
        int d = 16 - IntLog2NonZero(newRange);
        this.count -= d;
        this.difference = ((newDifference + 1) << d) - 1;
        this.range = newRange << d;
        if (this.count < 0)
        {
            this.Refill();
        }
    }

    private void Refill()
    {
        ReadOnlySpan<byte> buffer = this.data.Span;
        int end = buffer.Length;
        int s = WindowSize - 9 - (this.count + 15);
        for (; s >= 0 && this.position < end; s -= 8, this.position++)
        {
            this.difference ^= (uint)buffer[this.position] << s;
            this.count += 8;
        }

        if (this.position >= end)
        {
            // The bitstream is exhausted; remaining reads behave as if padded with set bits.
            this.count = LotsOfBits;
        }
    }

    // Returns FloorLog2(value) + 1 for value > 0.
    private static int IntLog2NonZero(uint value) => WindowSize - BitOperations.LeadingZeroCount(value);
}
