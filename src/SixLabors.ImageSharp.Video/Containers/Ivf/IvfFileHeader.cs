// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;

/// <summary>
/// The 32-byte file header of an IVF container.
/// </summary>
/// <remarks>
/// IVF is a minimal container commonly used to carry raw AV1 (and VP8/VP9) bitstreams for testing.
/// The layout is little-endian: signature "DKIF", version (u16), header length (u16), codec FourCC,
/// width (u16), height (u16), frame-rate numerator (u32), frame-rate denominator (u32), frame count
/// (u32), and 4 reserved bytes.
/// </remarks>
internal readonly struct IvfFileHeader
{
    /// <summary>
    /// The size of the IVF file header in bytes.
    /// </summary>
    public const int Size = 32;

    private IvfFileHeader(string fourCc, int width, int height, uint frameRateNumerator, uint frameRateDenominator, uint frameCount)
    {
        this.FourCc = fourCc;
        this.Width = width;
        this.Height = height;
        this.FrameRateNumerator = frameRateNumerator;
        this.FrameRateDenominator = frameRateDenominator;
        this.FrameCount = frameCount;
    }

    /// <summary>
    /// Gets the "DKIF" signature identifying an IVF container.
    /// </summary>
    public static ReadOnlySpan<byte> Signature => "DKIF"u8;

    /// <summary>
    /// Gets the "AV01" FourCC identifying an AV1 bitstream.
    /// </summary>
    public static ReadOnlySpan<byte> Av1FourCc => "AV01"u8;

    /// <summary>
    /// Gets the codec FourCC, e.g. "AV01".
    /// </summary>
    public string FourCc { get; }

    /// <summary>
    /// Gets the frame width in pixels declared by the container.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the frame height in pixels declared by the container.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the frame-rate numerator (time base denominator).
    /// </summary>
    public uint FrameRateNumerator { get; }

    /// <summary>
    /// Gets the frame-rate denominator (time base numerator).
    /// </summary>
    public uint FrameRateDenominator { get; }

    /// <summary>
    /// Gets the number of frames declared by the container.
    /// </summary>
    public uint FrameCount { get; }

    /// <summary>
    /// Gets a value indicating whether the codec FourCC denotes AV1.
    /// </summary>
    public bool IsAv1 => string.Equals(this.FourCc, "AV01", StringComparison.Ordinal);

    /// <summary>
    /// Parses an IVF file header from a 32-byte span.
    /// </summary>
    /// <param name="header">The header bytes; must be at least <see cref="Size"/> bytes long.</param>
    /// <returns>The parsed <see cref="IvfFileHeader"/>.</returns>
    /// <exception cref="InvalidDataException">The signature is not "DKIF".</exception>
    public static IvfFileHeader Parse(ReadOnlySpan<byte> header)
    {
        if (header.Length < Size || !header[..4].SequenceEqual(Signature))
        {
            throw new InvalidDataException("The stream does not start with a valid IVF (DKIF) signature.");
        }

        string fourCc = Encoding.ASCII.GetString(header.Slice(8, 4));
        int width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(12, 2));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(14, 2));
        uint rateNumerator = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4));
        uint rateDenominator = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4));
        uint frameCount = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24, 4));

        return new IvfFileHeader(fourCc, width, height, rateNumerator, rateDenominator, frameCount);
    }
}
