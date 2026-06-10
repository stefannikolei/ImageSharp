// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// A test-only AV1 multi-symbol arithmetic encoder. It is the exact inverse of
/// <see cref="Av1SymbolDecoder"/> (a port of the reference Daala entropy encoder) and is used to
/// produce bitstreams for round-trip validation of the decoder.
/// </summary>
internal sealed class Av1SymbolEncoder
{
    private const int WindowSize = 32;
    private const int ProbabilityShift = 6;
    private const int MinProbability = 4;
    private const int ProbabilityTop = 1 << 15;

    private readonly List<ushort> precarry = [];
    private uint low;
    private uint range;
    private int count;

    public Av1SymbolEncoder()
    {
        this.low = 0;
        this.range = 0x8000;
        this.count = -9;
    }

    public void WriteSymbol(int symbol, Span<ushort> cdf)
    {
        int nsymbs = cdf.Length - 1;
        this.Encode(symbol > 0 ? cdf[symbol - 1] : ProbabilityTop, cdf[symbol], symbol, nsymbs);
        Av1Cdf.Update(cdf, symbol, nsymbs);
    }

    public void WriteSymbolNoUpdate(int symbol, ReadOnlySpan<ushort> cdf)
    {
        int nsymbs = cdf.Length - 1;
        this.Encode(symbol > 0 ? cdf[symbol - 1] : ProbabilityTop, cdf[symbol], symbol, nsymbs);
    }

    public void WriteBool(int bit)
    {
        ReadOnlySpan<ushort> cdf = [1 << 14, 0, 0];
        this.WriteSymbolNoUpdate(bit, cdf);
    }

    public void WriteLiteral(uint value, int numBits)
    {
        for (int i = numBits - 1; i >= 0; i--)
        {
            this.WriteBool((int)((value >> i) & 1));
        }
    }

    public void WriteGolomb(uint value)
    {
        uint valuePlusOne = value + 1;
        int numBits = 31 - System.Numerics.BitOperations.LeadingZeroCount(valuePlusOne);
        for (int i = 0; i < numBits; i++)
        {
            this.WriteBool(0);
        }

        this.WriteBool(1);
        for (int i = numBits - 1; i >= 0; i--)
        {
            this.WriteBool((int)((valuePlusOne >> i) & 1));
        }
    }

    public byte[] Finish()
    {
        uint l = this.low;
        int c = this.count;
        int s = 10;
        uint m = 0x3FFF;
        uint e = ((l + m) & ~m) | (m + 1);
        s += c;
        if (s > 0)
        {
            uint n = (1u << (c + 16)) - 1;
            do
            {
                this.precarry.Add((ushort)(e >> (c + 16)));
                e &= n;
                s -= 8;
                c -= 8;
                n >>= 8;
            }
            while (s > 0);
        }

        int offs = this.precarry.Count;
        byte[] output = new byte[offs];
        uint carry = 0;
        for (int i = offs - 1; i >= 0; i--)
        {
            uint value = this.precarry[i] + carry;
            output[i] = (byte)value;
            carry = value >> 8;
        }

        return output;
    }

    private void Encode(int fl, int fh, int symbol, int nsymbs)
    {
        uint r = this.range;
        int n = nsymbs - 1;
        uint u;
        uint v = (((r >> 8) * (uint)(fh >> ProbabilityShift)) >> (7 - ProbabilityShift)) + (uint)(MinProbability * (n - symbol));
        if (fl < ProbabilityTop)
        {
            u = (((r >> 8) * (uint)(fl >> ProbabilityShift)) >> (7 - ProbabilityShift)) + (uint)(MinProbability * (n - (symbol - 1)));
        }
        else
        {
            u = r;
        }

        this.low += r - u;
        this.Normalize(this.low, u - v);
    }

    private void Normalize(uint lowIn, uint rng)
    {
        int c = this.count;
        int d = 16 - IntLog2NonZero(rng);
        int s = c + d;
        uint low = lowIn;
        if (s >= 0)
        {
            c += 16;
            uint m = (1u << c) - 1;
            if (s >= 8)
            {
                this.precarry.Add((ushort)(low >> c));
                low &= m;
                c -= 8;
                m = (1u << c) - 1;
            }

            this.precarry.Add((ushort)(low >> c));
            s = c + d - 24;
            low &= m;
        }

        this.low = low << d;
        this.range = rng << d;
        this.count = s;
    }

    private static int IntLog2NonZero(uint value) => WindowSize - BitOperations.LeadingZeroCount(value);
}
