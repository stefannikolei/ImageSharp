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

    // Display index -> the temporal-unit index that produces it, plus its position among that unit's
    // displayed outputs (a temporal unit can show a coded frame, hide it, or re-show a stored one).
    private readonly List<(int Unit, int Output)> displayMap = [];
    private ObuSequenceHeader sequenceHeader;
    private Av1ReferenceFrameStore referenceStore = new();
    private int lastDecodedUnit = -1;
    private bool isDisposed;

    public Av1VideoFrameSource(Stream stream)
    {
        this.stream = stream;
        IvfFileHeader fileHeader = this.BuildIndex();
        this.Metadata = new VideoMetadata
        {
            Size = new Size(fileHeader.Width, fileHeader.Height),
            FrameCount = this.displayMap.Count,
            FrameRateNumerator = (int)fileHeader.FrameRateNumerator,
            FrameRateDenominator = (int)fileHeader.FrameRateDenominator,
        };
    }

    /// <inheritdoc/>
    public Size Size => this.Metadata.Size;

    /// <inheritdoc/>
    public int FrameCount => this.displayMap.Count;

    /// <inheritdoc/>
    public VideoMetadata Metadata { get; }

    /// <inheritdoc/>
    public Image<TPixel> DecodeFrame<TPixel>(int frameIndex, Configuration configuration)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Av1DisplayFrame decoded = this.DecodeUpTo(frameIndex);
        return Av1FrameConverter.ToImage<TPixel>(decoded.Luma, decoded.ChromaU, decoded.ChromaV, configuration, this.sequenceHeader.BitDepth);
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
            bool isKeyFrame = false;
            int displayed = 0;
            while (ObuReader.TryRead(temporalUnit, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
            {
                Av1DecoderCore.EnsureBaseLayer(header);
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
                    isKeyFrame |= frameType is Av1FrameType.Key or Av1FrameType.IntraOnly;
                    if (Av1DecoderCore.PeekShowFrame(payload, this.sequenceHeader))
                    {
                        displayed++;
                    }
                }
                else if (header.Type == ObuType.FrameHeader && haveSequenceHeader)
                {
                    if (Av1DecoderCore.TryPeekShowExistingSlot(payload, this.sequenceHeader, out _))
                    {
                        displayed++;
                    }
                    else
                    {
                        // A standalone frame-header OBU announces a coded frame whose tiles follow in
                        // tile-group OBUs.
                        Av1FrameType frameType = Av1DecoderCore.PeekFrameType(payload, this.sequenceHeader);
                        isKeyFrame |= frameType is Av1FrameType.Key or Av1FrameType.IntraOnly;
                        if (Av1DecoderCore.PeekShowFrame(payload, this.sequenceHeader))
                        {
                            displayed++;
                        }
                    }
                }
            }

            int unit = this.index.Count;
            this.index.Add(new FrameEntry(payloadOffset, (int)frameSize, isKeyFrame));
            for (int d = 0; d < displayed; d++)
            {
                this.displayMap.Add((unit, d));
            }
        }

        return fileHeader;
    }

    private Av1DisplayFrame DecodeUpTo(int displayTarget)
    {
        (int targetUnit, int targetOutput) = this.displayMap[displayTarget];
        int keyframe = this.NearestKeyframeAtOrBefore(targetUnit);

        int startFrom;
        if (targetUnit > this.lastDecodedUnit && this.lastDecodedUnit >= keyframe)
        {
            // The decoder is already positioned within this GOP; continue forward.
            startFrom = this.lastDecodedUnit + 1;
        }
        else
        {
            // Seek back: reset the reference store and decode from the keyframe.
            this.referenceStore = new Av1ReferenceFrameStore();
            startFrom = keyframe;
        }

        List<Av1DisplayFrame> outputs = [];
        for (int i = startFrom; i <= targetUnit; i++)
        {
            outputs = this.DecodeUnitAt(i);
            this.lastDecodedUnit = i;
        }

        return outputs[targetOutput];
    }

    // Decodes every frame OBU of one temporal unit (updating the reference store) and returns the
    // frames the unit displays: shown coded frames plus show_existing_frame re-emissions.
    private List<Av1DisplayFrame> DecodeUnitAt(int unitIndex)
    {
        FrameEntry entry = this.index[unitIndex];
        this.stream.Position = entry.Offset;
        byte[] temporalUnit = new byte[entry.Length];
        this.stream.ReadExactly(temporalUnit);

        List<Av1DisplayFrame> outputs = [];
        int offset = 0;
        Av1DecoderCore.PendingFrame? pending = null;
        while (ObuReader.TryRead(temporalUnit, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
        {
            if (header.Type == ObuType.Frame)
            {
                // DecodeFrame also publishes the frame (with its frame-end CDF and header state) into
                // the reference slots it refreshes.
                Av1TileDecoder decoded = Av1DecoderCore.DecodeFrame(payload, this.sequenceHeader, this.referenceStore, out ObuFrameHeader frameHeader);
                if (frameHeader.ShowFrame)
                {
                    outputs.Add(Av1DecoderCore.CreateDisplayFrame(decoded.Luma, decoded.ChromaU, decoded.ChromaV, frameHeader.FilmGrain, this.sequenceHeader.BitDepth));
                }
            }
            else if (header.Type == ObuType.FrameHeader)
            {
                if (Av1DecoderCore.TryPeekShowExistingSlot(payload, this.sequenceHeader, out int slot))
                {
                    Av1ReferenceFrame shown = this.referenceStore[slot]
                        ?? throw new InvalidDataException($"show_existing_frame references the empty slot {slot}.");
                    if (shown.IsKeyFrame)
                    {
                        throw new NotSupportedException("show_existing_frame of a key frame (forward key frames) is not supported yet.");
                    }

                    outputs.Add(Av1DecoderCore.CreateDisplayFrame(shown.Luma, shown.ChromaU!, shown.ChromaV!, shown.HeaderState?.FilmGrain, this.sequenceHeader.BitDepth));
                }
                else
                {
                    pending = Av1DecoderCore.ParseFrameHeader(payload, this.sequenceHeader, this.referenceStore);
                }
            }
            else if (header.Type == ObuType.TileGroup)
            {
                if (pending is null)
                {
                    throw new InvalidDataException("Encountered a tile group OBU without a preceding frame header.");
                }

                if (Av1DecoderCore.AddTileGroupAndTryFinish(pending, payload, this.sequenceHeader, this.referenceStore) is { } decoded)
                {
                    if (pending.Header.ShowFrame)
                    {
                        outputs.Add(Av1DecoderCore.CreateDisplayFrame(decoded.Luma, decoded.ChromaU, decoded.ChromaV, pending.Header.FilmGrain, this.sequenceHeader.BitDepth));
                    }

                    pending = null;
                }
            }
        }

        Av1DecoderCore.EnsureNoPendingFrame(pending);
        return outputs;
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
