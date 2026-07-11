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
        Av1ReferenceFrameStore referenceStore = new();
        PendingFrame? pending = null;
        int offset = 0;
        while (ObuReader.TryRead(frame, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
        {
            EnsureBaseLayer(header);
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

                return DecodeFrame(payload, sequenceHeader, referenceStore, out _);
            }
            else if (header.Type == ObuType.FrameHeader && haveSequenceHeader
                && !TryPeekShowExistingSlot(payload, sequenceHeader, out _))
            {
                pending = ParseFrameHeader(payload, sequenceHeader, referenceStore);
            }
            else if (header.Type == ObuType.TileGroup)
            {
                if (pending is null)
                {
                    throw new InvalidDataException("Encountered a tile group OBU without a preceding frame header.");
                }

                AddTileGroup(pending, payload);
                if (pending.IsComplete)
                {
                    return FinishFrame(pending, sequenceHeader, referenceStore);
                }
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
            PendingFrame? pending = null;
            while (ObuReader.TryRead(frame, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
            {
                EnsureBaseLayer(header);
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
                else if (header.Type == ObuType.FrameHeader && haveSequenceHeader
                    && !TryPeekShowExistingSlot(payload, sequenceHeader, out _))
                {
                    pending = ParseFrameHeader(payload, sequenceHeader, referenceStore);
                }
                else if (header.Type == ObuType.TileGroup)
                {
                    if (pending is null)
                    {
                        throw new InvalidDataException("Encountered a tile group OBU without a preceding frame header.");
                    }

                    if (AddTileGroupAndTryFinish(pending, payload, sequenceHeader, referenceStore) is { } tileDecoder)
                    {
                        frames.Add(tileDecoder);
                        pending = null;
                    }
                }
            }

            EnsureNoPendingFrame(pending);
        }

        if (frames.Count == 0)
        {
            throw new InvalidDataException("The AV1 stream contains no decodable frame OBU.");
        }

        return frames;
    }

    // Decodes a frame OBU: the frame header followed by a single tile group holding every tile of the
    // frame (the header-parse and tile-decode halves are shared with the separate FRAME_HEADER +
    // TILE_GROUP OBU layout).
    internal static Av1TileDecoder DecodeFrame(ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader, Av1ReferenceFrameStore referenceStore, out ObuFrameHeader frameHeader)
    {
        PendingFrame pending = ParseFrameHeader(payload, sequenceHeader, referenceStore);
        frameHeader = pending.Header;

        int tileGroupStart = (frameHeader.EndBitPosition + 7) >> 3;
        AddTileGroup(pending, payload[tileGroupStart..]);
        if (!pending.IsComplete)
        {
            throw new InvalidDataException("A frame OBU must contain every tile of the frame.");
        }

        return FinishFrame(pending, sequenceHeader, referenceStore);
    }

    // Parses a frame header (dispatching between the intra and inter parsers) and prepares the tile
    // decoder that will reconstruct the frame: inter frames resolve their references from the store and
    // start from the primary reference's saved CDF state when they have one.
    internal static PendingFrame ParseFrameHeader(ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader, Av1ReferenceFrameStore referenceStore)
    {
        Av1FrameType frameType = PeekFrameType(payload, sequenceHeader);
        ObuFrameHeader frameHeader;
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

        return new PendingFrame(frameHeader, tileDecoder, frameEndCdfs);
    }

    // Collects the tiles of one tile group; the frame is complete once a group ends with the frame's
    // last tile.
    internal static void AddTileGroup(PendingFrame pending, ReadOnlySpan<byte> payload)
    {
        ObuFrameHeader frameHeader = pending.Header;
        ObuTileGroup tileGroup = ObuTileGroup.Parse(payload, frameHeader);
        if (tileGroup.FirstTile != pending.Tiles.Count)
        {
            throw new InvalidDataException($"Tile group starts at tile {tileGroup.FirstTile}, expected {pending.Tiles.Count}.");
        }

        for (int i = 0; i < tileGroup.Count; i++)
        {
            (int tileOffset, int tileLength) = tileGroup.GetTile(i);
            pending.Tiles.Add(payload.Slice(tileOffset, tileLength).ToArray());
        }

        int numTiles = ((frameHeader.TileColumnStarts?.Length ?? 2) - 1) * ((frameHeader.TileRowStarts?.Length ?? 2) - 1);
        if (tileGroup.LastTile == numTiles - 1)
        {
            pending.IsComplete = true;
        }
    }

    // Collects one tile group and, once the frame's last tile arrived, decodes and publishes the frame.
    internal static Av1TileDecoder? AddTileGroupAndTryFinish(PendingFrame pending, ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader, Av1ReferenceFrameStore referenceStore)
    {
        AddTileGroup(pending, payload);
        return pending.IsComplete ? FinishFrame(pending, sequenceHeader, referenceStore) : null;
    }

    // A coded frame's header and all of its tile groups must lie within one temporal unit.
    internal static void EnsureNoPendingFrame(PendingFrame? pending)
    {
        if (pending is not null)
        {
            throw new InvalidDataException("The temporal unit ended with an incomplete frame (missing tile groups).");
        }
    }

    // Decodes the collected tiles and publishes the reconstructed frame with its frame-end context
    // (CDFs, header state, motion field) into the reference slots it refreshes.
    internal static Av1TileDecoder FinishFrame(PendingFrame pending, in ObuSequenceHeader sequenceHeader, Av1ReferenceFrameStore referenceStore)
    {
        ObuFrameHeader frameHeader = pending.Header;
        Av1TileDecoder tileDecoder = pending.Decoder;
        tileDecoder.DecodeTiles(pending.Tiles);

        // The save zeroes every CDF's adaptation counter (dav1d's cdf_thread_update); a frame inheriting
        // the state keeps the adapted probabilities but restarts adaptation at the initial rate.
        Av1FrameCdfSet frameEndCdfs = pending.FrameEndCdfs;
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
            new ObuPrimaryReferenceState(
                lf.RefDeltas,
                lf.ModeDeltas,
                frameHeader.GlobalMotionParams,
                frameHeader.SegmentationParams,
                frameHeader.FilmGrain,
                frameHeader.UpscaledWidth,
                frameHeader.FrameHeight,
                frameHeader.RenderWidth,
                frameHeader.RenderHeight),
            temporalMvs,
            referenceOrderHints,
            frameHeader.FrameType == Av1FrameType.Key);
        referenceStore.Update(decoded, frameHeader.RefreshFrameFlags);
        return tileDecoder;
    }

    /// <summary>
    /// A coded frame whose header OBU has been parsed but whose tiles are still being collected: the
    /// FRAME_HEADER + TILE_GROUP OBU layout splits one frame across several OBUs, with the tiles
    /// possibly spread over multiple tile groups.
    /// </summary>
    internal sealed class PendingFrame
    {
        public PendingFrame(in ObuFrameHeader header, Av1TileDecoder decoder, Av1FrameCdfSet frameEndCdfs)
        {
            this.Header = header;
            this.Decoder = decoder;
            this.FrameEndCdfs = frameEndCdfs;
        }

        /// <summary>Gets the parsed frame header.</summary>
        public ObuFrameHeader Header { get; }

        /// <summary>Gets the tile decoder prepared for this frame.</summary>
        public Av1TileDecoder Decoder { get; }

        /// <summary>Gets the CDF set to publish at the frame end (a pre-decode snapshot when the frame
        /// disables the frame-end CDF update).</summary>
        public Av1FrameCdfSet FrameEndCdfs { get; }

        /// <summary>Gets the tiles collected so far, in tile order.</summary>
        public List<ReadOnlyMemory<byte>> Tiles { get; } = [];

        /// <summary>Gets or sets a value indicating whether every tile of the frame has been collected.</summary>
        public bool IsComplete { get; set; }
    }

    // Film grain synthesises into displayed output only: the reconstruction (and thus every
    // reference) stays grain-free, and a re-shown frame applies its own stored parameters.
    internal static Av1DisplayFrame CreateDisplayFrame(Av1Plane luma, Av1Plane chromaU, Av1Plane chromaV, ObuFilmGrainParams? grain, int bitDepth)
    {
        if (grain is null)
        {
            return new Av1DisplayFrame(luma, chromaU, chromaV);
        }

        (Av1Plane grainLuma, Av1Plane grainU, Av1Plane grainV) = Prediction.Av1FilmGrain.Apply(grain, luma, chromaU, chromaV, bitDepth);
        return new Av1DisplayFrame(grainLuma, grainU, grainV);
    }

    // Multi-layer (scalable) streams carry OBUs for enhancement layers; decoding them into the same
    // reference store would corrupt the base layer, so they are rejected until operating points are
    // supported.
    internal static void EnsureBaseLayer(in ObuHeader header)
    {
        if (header.HasExtension && (header.TemporalId != 0 || header.SpatialId != 0))
        {
            throw new NotSupportedException("Scalable (multi-layer) streams are not supported yet.");
        }
    }

    /// <summary>
    /// Decodes an IVF-wrapped AV1 stream into its displayed frames, in display order: hidden
    /// (no-show) frames are decoded into the reference store without being emitted, and
    /// <c>show_existing_frame</c> headers emit the referenced, previously decoded frame.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the IVF container.</param>
    /// <returns>The displayed frames, in display order.</returns>
    /// <exception cref="InvalidDataException">The stream is not a valid AV1/IVF bitstream.</exception>
    public static List<Av1DisplayFrame> DecodeDisplayFrames(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));

        IvfFileHeader fileHeader = IvfReader.ReadFileHeader(stream);
        if (!fileHeader.IsAv1)
        {
            throw new InvalidDataException($"Unsupported IVF codec FourCC '{fileHeader.FourCc}', expected AV1.");
        }

        List<Av1DisplayFrame> frames = [];
        bool haveSequenceHeader = false;
        ObuSequenceHeader sequenceHeader = default;
        Av1ReferenceFrameStore referenceStore = new();

        while (IvfReader.TryReadFrame(stream, out _, out byte[] temporalUnit))
        {
            int offset = 0;
            PendingFrame? pending = null;
            while (ObuReader.TryRead(temporalUnit, ref offset, out ObuHeader header, out ReadOnlySpan<byte> payload))
            {
                EnsureBaseLayer(header);
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

                    Av1TileDecoder tileDecoder = DecodeFrame(payload, sequenceHeader, referenceStore, out ObuFrameHeader frameHeader);
                    if (frameHeader.ShowFrame)
                    {
                        frames.Add(CreateDisplayFrame(tileDecoder.Luma, tileDecoder.ChromaU, tileDecoder.ChromaV, frameHeader.FilmGrain, sequenceHeader.BitDepth));
                    }
                }
                else if (header.Type == ObuType.FrameHeader && haveSequenceHeader)
                {
                    if (TryPeekShowExistingSlot(payload, sequenceHeader, out int slot))
                    {
                        Av1ReferenceFrame shown = referenceStore[slot]
                            ?? throw new InvalidDataException($"show_existing_frame references the empty slot {slot}.");
                        if (shown.IsKeyFrame)
                        {
                            throw new NotSupportedException("show_existing_frame of a key frame (forward key frames) is not supported yet.");
                        }

                        frames.Add(CreateDisplayFrame(shown.Luma, shown.ChromaU!, shown.ChromaV!, shown.HeaderState?.FilmGrain, sequenceHeader.BitDepth));
                    }
                    else
                    {
                        pending = ParseFrameHeader(payload, sequenceHeader, referenceStore);
                    }
                }
                else if (header.Type == ObuType.TileGroup)
                {
                    if (pending is null)
                    {
                        throw new InvalidDataException("Encountered a tile group OBU without a preceding frame header.");
                    }

                    if (AddTileGroupAndTryFinish(pending, payload, sequenceHeader, referenceStore) is { } tileDecoder)
                    {
                        if (pending.Header.ShowFrame)
                        {
                            frames.Add(CreateDisplayFrame(tileDecoder.Luma, tileDecoder.ChromaU, tileDecoder.ChromaV, pending.Header.FilmGrain, sequenceHeader.BitDepth));
                        }

                        pending = null;
                    }
                }
            }

            EnsureNoPendingFrame(pending);
        }

        if (frames.Count == 0)
        {
            throw new InvalidDataException("The AV1 stream contains no displayable frame.");
        }

        return frames;
    }

    // Reads a frame-header OBU only far enough to detect show_existing_frame and its reference slot.
    // (Showing an existing KEY frame additionally reloads decoder state; such streams use forward key
    // frames, which aomenc does not emit by default, and are not handled here.)
    internal static bool TryPeekShowExistingSlot(ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader, out int slot)
    {
        slot = 0;
        if (sequenceHeader.ReducedStillPictureHeader)
        {
            return false;
        }

        Av1BitStreamReader reader = new(payload);
        if (!reader.ReadBoolean())
        {
            return false;
        }

        slot = (int)reader.ReadLiteral(3);
        return true;
    }

    // Reads a frame OBU header only far enough to determine whether the frame is shown directly.
    internal static bool PeekShowFrame(ReadOnlySpan<byte> payload, in ObuSequenceHeader sequenceHeader)
    {
        if (sequenceHeader.ReducedStillPictureHeader)
        {
            return true;
        }

        Av1BitStreamReader reader = new(payload);
        if (reader.ReadBoolean())
        {
            throw new NotSupportedException("show_existing_frame inside a frame OBU is not supported.");
        }

        reader.ReadLiteral(2); // frame_type
        return reader.ReadBoolean();
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
