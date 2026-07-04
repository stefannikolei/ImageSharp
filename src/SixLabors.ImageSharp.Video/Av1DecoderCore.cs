// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// Shared parsing logic for the AV1 decoder. At this stage it covers container demuxing and
/// sequence-header parsing only; full frame reconstruction is tracked in docs/av1-codec-roadmap.md.
/// </summary>
internal static class Av1DecoderCore
{
    /// <summary>
    /// Reads the coded frame dimensions from an IVF-wrapped AV1 stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the IVF container.</param>
    /// <returns>The frame <see cref="Size"/> in pixels.</returns>
    /// <exception cref="InvalidDataException">The stream is not a valid AV1/IVF bitstream.</exception>
    public static Size ReadDimensions(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));

        IvfFileHeader fileHeader = IvfReader.ReadFileHeader(stream);
        if (!fileHeader.IsAv1)
        {
            throw new InvalidDataException($"Unsupported IVF codec FourCC '{fileHeader.FourCc}', expected AV1.");
        }

        // Prefer the authoritative dimensions coded in the AV1 sequence header, falling back to the
        // values declared by the container if no sequence header is found in the first frame.
        if (TryReadSequenceHeaderDimensions(stream, out Size size))
        {
            return size;
        }

        return new Size(fileHeader.Width, fileHeader.Height);
    }

    /// <summary>
    /// Decodes the first key frame of an IVF-wrapped AV1 stream into reconstructed planes. Supports the
    /// intra feature subset implemented by <see cref="Av1TileDecoder"/>.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the IVF container.</param>
    /// <returns>The decoder holding the reconstructed luma and chroma planes.</returns>
    /// <exception cref="InvalidDataException">The stream is not a valid AV1/IVF bitstream.</exception>
    public static Av1TileDecoder DecodeFirstFrame(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));

        IvfFileHeader fileHeader = IvfReader.ReadFileHeader(stream);
        if (!fileHeader.IsAv1)
        {
            throw new InvalidDataException($"Unsupported IVF codec FourCC '{fileHeader.FourCc}', expected AV1.");
        }

        if (!IvfReader.TryReadFrame(stream, out _, out byte[] frame))
        {
            throw new InvalidDataException("The AV1 stream contains no coded frames.");
        }

        bool haveSequenceHeader = false;
        ObuSequenceHeader sequenceHeader = default;
        int offset = 0;
        while (ObuReader.TryRead(frame, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
        {
            if (header.Type == ObuType.SequenceHeader)
            {
                sequenceHeader = ObuSequenceHeader.Parse(payload);
                haveSequenceHeader = true;
            }
            else if (header.Type == ObuType.Frame)
            {
                if (!haveSequenceHeader)
                {
                    throw new InvalidDataException("Encountered a frame OBU before any sequence header.");
                }

                Av1BitStreamReader reader = new(payload);
                ObuFrameHeader frameHeader = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);

                int tileGroupStart = (frameHeader.EndBitPosition + 7) >> 3;
                ObuTileGroup tileGroup = ObuTileGroup.Parse(payload[tileGroupStart..], frameHeader);

                Av1TileDecoder tileDecoder = new(sequenceHeader, frameHeader);
                tileDecoder.DecodeTiles(SliceTiles(payload, tileGroupStart, tileGroup));
                return tileDecoder;
            }
        }

        throw new InvalidDataException("The AV1 stream contains no decodable frame OBU.");
    }

    /// <summary>
    /// Decodes every coded frame of an IVF-wrapped AV1 stream into reconstructed planes, maintaining the
    /// reference-frame store so inter frames can be predicted from earlier frames. Key frames and the
    /// implemented single-reference inter subset are supported; unsupported syntax raises
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the IVF container.</param>
    /// <returns>The decoders holding each reconstructed frame, in decode order.</returns>
    /// <exception cref="InvalidDataException">The stream is not a valid AV1/IVF bitstream.</exception>
    public static List<Av1TileDecoder> DecodeAllFrames(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));

        IvfFileHeader fileHeader = IvfReader.ReadFileHeader(stream);
        if (!fileHeader.IsAv1)
        {
            throw new InvalidDataException($"Unsupported IVF codec FourCC '{fileHeader.FourCc}', expected AV1.");
        }

        List<Av1TileDecoder> frames = [];
        bool haveSequenceHeader = false;
        ObuSequenceHeader sequenceHeader = default;
        Av1ReferenceFrameStore referenceStore = new();

        while (IvfReader.TryReadFrame(stream, out _, out byte[] frame))
        {
            int offset = 0;
            while (ObuReader.TryRead(frame, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
            {
                if (header.Type == ObuType.SequenceHeader)
                {
                    sequenceHeader = ObuSequenceHeader.Parse(payload);
                    haveSequenceHeader = true;
                }
                else if (header.Type == ObuType.Frame)
                {
                    if (!haveSequenceHeader)
                    {
                        throw new InvalidDataException("Encountered a frame OBU before any sequence header.");
                    }

                    Av1TileDecoder tileDecoder = DecodeFrame(payload, sequenceHeader, referenceStore, out _);
                    frames.Add(tileDecoder);
                }
            }
        }

        if (frames.Count == 0)
        {
            throw new InvalidDataException("The AV1 stream contains no decodable frame OBU.");
        }

        return frames;
    }

    // Parses a frame OBU header (dispatching between the intra and inter parsers), decodes its single
    // tile (predicting inter frames from the reference store, loading the primary reference's CDF and
    // header state when the frame has one) and publishes the reconstructed frame with its frame-end
    // context into the reference slots it refreshes.
    internal static Av1TileDecoder DecodeFrame(ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader, Av1ReferenceFrameStore referenceStore, out ObuFrameHeader frameHeader)
    {
        Av1FrameType frameType = PeekFrameType(payload, sequenceHeader);
        Av1TileDecoder tileDecoder;
        if (frameType is Av1FrameType.Key or Av1FrameType.IntraOnly)
        {
            Av1BitStreamReader reader = new(payload);
            frameHeader = ObuFrameHeader.ParseIntra(ref reader, sequenceHeader);
            tileDecoder = new Av1TileDecoder(sequenceHeader, frameHeader);
        }
        else
        {
            Av1BitStreamReader reader = new(payload);
            frameHeader = ObuFrameHeader.ParseInter(ref reader, sequenceHeader, referenceStore.GetOrderHints(), referenceStore.GetHeaderStates());

            // Resolve each reference name (LAST .. ALTREF) to its frame via the header's slot mapping.
            Av1ReferenceFrame?[] references = new Av1ReferenceFrame?[7];
            for (int i = 0; i < references.Length; i++)
            {
                references[i] = referenceStore[frameHeader.ReferenceFrameIndices[i]];
            }

            // A frame with a primary reference starts from a copy of that reference's saved CDF state
            // instead of the defaults.
            Av1FrameCdfSet cdfs = frameHeader.PrimaryRefFrame != ObuFrameHeader.PrimaryReferenceNone
                && references[frameHeader.PrimaryRefFrame]?.Cdfs is { } saved
                ? saved.Clone()
                : Av1FrameCdfSet.CreateDefault(frameHeader.BaseQIndex);

            tileDecoder = new Av1InterTileDecoder(sequenceHeader, frameHeader, references, cdfs);
        }


        // With disable_frame_end_update_cdf the state saved at the frame end is the initial state, not
        // the adaptation the decode performs; snapshot it before decoding.
        Av1FrameCdfSet frameEndCdfs = frameHeader.DisableFrameEndUpdateCdf ? tileDecoder.Cdfs.Clone() : tileDecoder.Cdfs;

        int tileGroupStart = (frameHeader.EndBitPosition + 7) >> 3;
        ObuTileGroup tileGroup = ObuTileGroup.Parse(payload[tileGroupStart..], frameHeader);
        tileDecoder.DecodeTiles(SliceTiles(payload, tileGroupStart, tileGroup));

        // The save zeroes every CDF's adaptation counter (dav1d's cdf_thread_update); a frame inheriting
        // the state keeps the adapted probabilities but restarts adaptation at the initial rate.
        frameEndCdfs.ResetCounters();

        // An inter frame saves its motion field (save_tmvs) and its own reference order hints so a later
        // frame's temporal motion-vector prediction can project them.
        Av1TemporalMvs? temporalMvs = null;
        int[]? referenceOrderHints = null;
        if (tileDecoder is Av1InterTileDecoder interDecoder)
        {
            int[] storeHints = referenceStore.GetOrderHints();
            referenceOrderHints = new int[7];
            for (int i = 0; i < 7; i++)
            {
                referenceOrderHints[i] = storeHints[frameHeader.ReferenceFrameIndices[i]];
            }

            if (sequenceHeader.EnableReferenceFrameMotionVectors && sequenceHeader.OrderHintBits > 0)
            {
                temporalMvs = Av1TemporalMvs.Save(interDecoder.MotionVectorGrid, sequenceHeader.OrderHintBits, frameHeader.OrderHint, referenceOrderHints);
            }
        }

        ObuFrameHeader.LoopFilter lf = frameHeader.LoopFilterParameters;
        Av1ReferenceFrame decoded = new(
            frameHeader.OrderHint,
            tileDecoder.Luma,
            tileDecoder.ChromaU,
            tileDecoder.ChromaV,
            frameEndCdfs,
            new ObuPrimaryReferenceState(lf.RefDeltas, lf.ModeDeltas, frameHeader.GlobalMotionParams),
            temporalMvs,
            referenceOrderHints);
        referenceStore.Update(decoded, frameHeader.RefreshFrameFlags);
        return tileDecoder;
    }

    // Copies each tile's compressed bytes out of the tile group (the tile decoder consumes them in
    // tile raster order).
    private static ReadOnlyMemory<byte>[] SliceTiles(ReadOnlySpan<byte> payload, int tileGroupStart, in ObuTileGroup tileGroup)
    {
        ReadOnlyMemory<byte>[] tiles = new ReadOnlyMemory<byte>[tileGroup.Count];
        for (int i = 0; i < tiles.Length; i++)
        {
            (int tileOffset, int tileLength) = tileGroup.GetTile(i);
            tiles[i] = payload.Slice(tileGroupStart + tileOffset, tileLength).ToArray();
        }

        return tiles;
    }

    // Reads only enough of a frame OBU header to determine the frame type (used to choose the parser).
    internal static Av1FrameType PeekFrameType(ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader)
    {
        if (sequenceHeader.ReducedStillPictureHeader)
        {
            return Av1FrameType.Key;
        }

        Av1BitStreamReader reader = new(payload);
        bool showExistingFrame = reader.ReadBoolean();
        if (showExistingFrame)
        {
            throw new NotSupportedException("show_existing_frame is not supported yet.");
        }

        return (Av1FrameType)reader.ReadLiteral(2);
    }

    private static bool TryReadSequenceHeaderDimensions(Stream stream, out Size size)
    {
        size = default;
        if (!IvfReader.TryReadFrame(stream, out _, out byte[] frame))
        {
            return false;
        }

        int offset = 0;
        while (ObuReader.TryRead(frame, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
        {
            if (header.Type == ObuType.SequenceHeader)
            {
                ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(payload);
                size = new Size(sequenceHeader.MaxFrameWidth, sequenceHeader.MaxFrameHeight);
                return true;
            }
        }

        return false;
    }
}
