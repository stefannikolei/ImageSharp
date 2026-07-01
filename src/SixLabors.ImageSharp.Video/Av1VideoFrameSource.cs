// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;
using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Video;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// A stateful, seekable source of decoded AV1 frames over an IVF stream. Construction builds a cheap
/// frame index (offsets, sizes and keyframe flags) by scanning the container without decoding pixels.
/// Each frame is decoded on demand: requesting an arbitrary frame seeks to the nearest preceding
/// keyframe and decodes forward, maintaining the reference-frame store so inter frames can be predicted.
/// </summary>
internal sealed class Av1VideoFrameSource : IVideoFrameSource
{
    private readonly Stream stream;
    private readonly List<FrameEntry> index = [];
    private ObuSequenceHeader sequenceHeader;
    private Av1ReferenceFrameStore referenceStore = new();
    private int lastDecodedIndex = -1;
    private bool isDisposed;

    public Av1VideoFrameSource(Stream stream)
    {
        this.stream = stream;
        IvfFileHeader fileHeader = this.BuildIndex();
        this.Metadata = new VideoMetadata
        {
            Size = new Size(fileHeader.Width, fileHeader.Height),
            FrameCount = this.index.Count,
            FrameRateNumerator = (int)fileHeader.FrameRateNumerator,
            FrameRateDenominator = (int)fileHeader.FrameRateDenominator,
        };
    }

    /// <inheritdoc/>
    public Size Size => this.Metadata.Size;

    /// <inheritdoc/>
    public int FrameCount => this.index.Count;

    /// <inheritdoc/>
    public VideoMetadata Metadata { get; }

    /// <inheritdoc/>
    public Image<TPixel> DecodeFrame<TPixel>(int frameIndex, Configuration configuration)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Av1TileDecoder decoded = this.DecodeUpTo(frameIndex);
        return Av1FrameConverter.ToImage<TPixel>(decoded, configuration);
    }

    /// <inheritdoc/>
    public void Dispose() => this.isDisposed = true;

    private IvfFileHeader BuildIndex()
    {
        this.stream.Position = 0;
        IvfFileHeader fileHeader = IvfReader.ReadFileHeader(this.stream);
        bool haveSequenceHeader = false;

        Span<byte> frameHeader = stackalloc byte[IvfReader.FrameHeaderSize];
        while (true)
        {
            int read = ReadFully(this.stream, frameHeader);
            if (read == 0)
            {
                break;
            }

            if (read < IvfReader.FrameHeaderSize)
            {
                throw new InvalidDataException("Truncated IVF frame header.");
            }

            uint frameSize = BinaryPrimitives.ReadUInt32LittleEndian(frameHeader[..4]);
            long payloadOffset = this.stream.Position;
            byte[] temporalUnit = new byte[frameSize];
            this.stream.ReadExactly(temporalUnit);

            int offset = 0;
            while (ObuReader.TryRead(temporalUnit, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
            {
                if (header.Type == ObuType.SequenceHeader)
                {
                    this.sequenceHeader = ObuSequenceHeader.Parse(payload);
                    haveSequenceHeader = true;
                }
                else if (header.Type == ObuType.Frame)
                {
                    if (!haveSequenceHeader)
                    {
                        throw new InvalidDataException("Encountered a frame OBU before any sequence header.");
                    }

                    Av1FrameType frameType = Av1DecoderCore.PeekFrameType(payload, this.sequenceHeader);
                    bool isKeyFrame = frameType is Av1FrameType.Key or Av1FrameType.IntraOnly;
                    this.index.Add(new FrameEntry(payloadOffset, (int)frameSize, isKeyFrame));
                }
            }
        }

        return fileHeader;
    }

    private Av1TileDecoder DecodeUpTo(int target)
    {
        int keyframe = this.NearestKeyframeAtOrBefore(target);

        int startFrom;
        if (target > this.lastDecodedIndex && this.lastDecodedIndex >= keyframe)
        {
            // The decoder is already positioned within this GOP; continue forward.
            startFrom = this.lastDecodedIndex + 1;
        }
        else
        {
            // Seek back: reset the reference store and decode from the keyframe.
            this.referenceStore = new Av1ReferenceFrameStore();
            startFrom = keyframe;
        }

        Av1TileDecoder? decoded = null;
        for (int i = startFrom; i <= target; i++)
        {
            decoded = this.DecodeFrameAt(i);
            this.lastDecodedIndex = i;
        }

        return decoded!;
    }

    private Av1TileDecoder DecodeFrameAt(int frameIndex)
    {
        FrameEntry entry = this.index[frameIndex];
        this.stream.Position = entry.Offset;
        byte[] temporalUnit = new byte[entry.Length];
        this.stream.ReadExactly(temporalUnit);

        int offset = 0;
        while (ObuReader.TryRead(temporalUnit, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
        {
            if (header.Type == ObuType.Frame)
            {
                // DecodeFrame also publishes the frame (with its frame-end CDF and header state) into
                // the reference slots it refreshes.
                return Av1DecoderCore.DecodeFrame(payload, this.sequenceHeader, this.referenceStore, out _);
            }
        }

        throw new InvalidDataException("Temporal unit contained no frame OBU.");
    }

    private int NearestKeyframeAtOrBefore(int target)
    {
        for (int i = target; i >= 0; i--)
        {
            if (this.index[i].IsKeyFrame)
            {
                return i;
            }
        }

        // The first frame of a valid stream is always a key frame.
        throw new InvalidDataException("No key frame found at or before the requested frame.");
    }

    private static int ReadFully(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private readonly record struct FrameEntry(long Offset, int Length, bool IsKeyFrame);
}
