// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A most-significant-bit-first reader over an AV1 bitstream payload.
/// </summary>
/// <remarks>
/// Implements the descriptor reading primitives defined in the AV1 specification, section 4
/// ("Conventions"): <c>f(n)</c> (fixed width literals) and <c>uvlc()</c> (unsigned variable
/// length codes). Bits are consumed from the most significant bit of the first byte onwards.
/// </remarks>
internal ref struct Av1BitStreamReader
{
    private readonly ReadOnlySpan<byte> data;
    private int bitPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BitStreamReader"/> struct.
    /// </summary>
    /// <param name="data">The bitstream payload to read from.</param>
    public Av1BitStreamReader(ReadOnlySpan<byte> data)
    {
        this.data = data;
        this.bitPosition = 0;
    }

    /// <summary>
    /// Gets the number of bits consumed so far.
    /// </summary>
    public readonly int BitPosition => this.bitPosition;

    /// <summary>
    /// Gets the total number of bits available in the payload.
    /// </summary>
    public readonly int BitLength => this.data.Length << 3;

    /// <summary>
    /// Reads a single bit. Corresponds to <c>f(1)</c> in the specification.
    /// </summary>
    /// <returns>The bit value, either 0 or 1.</returns>
    public uint ReadBit()
    {
        int bytePosition = this.bitPosition >> 3;
        if (bytePosition >= this.data.Length)
        {
            throw new InvalidDataException("Attempted to read beyond the end of the AV1 bitstream.");
        }

        int shift = 7 - (this.bitPosition & 7);
        this.bitPosition++;
        return (uint)((this.data[bytePosition] >> shift) & 1);
    }

    /// <summary>
    /// Reads <paramref name="n"/> bits as an unsigned integer, most significant bit first.
    /// Corresponds to <c>f(n)</c> in the specification.
    /// </summary>
    /// <param name="n">The number of bits to read, in the range [0, 32].</param>
    /// <returns>The decoded value.</returns>
    public uint ReadLiteral(int n)
    {
        DebugGuard.MustBeBetweenOrEqualTo(n, 0, 32, nameof(n));

        uint value = 0;
        for (int i = 0; i < n; i++)
        {
            value = (value << 1) | this.ReadBit();
        }

        return value;
    }

    /// <summary>
    /// Reads a single bit as a boolean.
    /// </summary>
    /// <returns><see langword="true"/> if the bit is set; otherwise <see langword="false"/>.</returns>
    public bool ReadBoolean() => this.ReadBit() != 0;

    /// <summary>
    /// Reads an unsigned variable length code. Corresponds to <c>uvlc()</c> in the specification.
    /// </summary>
    /// <returns>The decoded value.</returns>
    public uint ReadUnsignedVariableLength()
    {
        int leadingZeros = 0;
        while (true)
        {
            bool done = this.ReadBoolean();
            if (done)
            {
                break;
            }

            leadingZeros++;
        }

        if (leadingZeros >= 32)
        {
            return uint.MaxValue;
        }

        uint value = this.ReadLiteral(leadingZeros);
        return value + (1u << leadingZeros) - 1u;
    }

    /// <summary>
    /// Reads a two's-complement signed integer of <paramref name="n"/> bits.
    /// Corresponds to <c>su(n)</c> in the specification.
    /// </summary>
    /// <param name="n">The number of bits, including the sign bit.</param>
    /// <returns>The decoded signed value.</returns>
    public int ReadSignedLiteral(int n)
    {
        int value = (int)this.ReadLiteral(n);
        int signMask = 1 << (n - 1);
        if ((value & signMask) != 0)
        {
            value -= 2 * signMask;
        }

        return value;
    }

    /// <summary>
    /// Reads a non-symmetric unsigned integer in the range [0, <paramref name="n"/>).
    /// Corresponds to <c>ns(n)</c> in the specification.
    /// </summary>
    /// <param name="n">The exclusive upper bound (must be at least 1).</param>
    /// <returns>The decoded value.</returns>
    public uint ReadNonSymmetric(uint n)
    {
        if (n <= 1)
        {
            return 0;
        }

        int w = FloorLog2(n) + 1;
        uint m = (uint)((1 << w) - n);
        uint v = this.ReadLiteral(w - 1);
        if (v < m)
        {
            return v;
        }

        uint extra = this.ReadBit();
        return (v << 1) - m + extra;
    }

    /// <summary>
    /// Reads <paramref name="n"/> bytes as a little-endian unsigned integer.
    /// Corresponds to <c>le(n)</c> in the specification; the reader must be byte-aligned.
    /// </summary>
    /// <param name="n">The number of bytes (1 to 4).</param>
    /// <returns>The decoded value.</returns>
    public uint ReadLittleEndian(int n)
    {
        uint t = 0;
        for (int i = 0; i < n; i++)
        {
            uint b = this.ReadLiteral(8);
            t += b << (i * 8);
        }

        return t;
    }

    private static int FloorLog2(uint value) => 31 - System.Numerics.BitOperations.LeadingZeroCount(value);
}
