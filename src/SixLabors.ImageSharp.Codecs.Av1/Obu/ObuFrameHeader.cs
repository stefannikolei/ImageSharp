// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// Represents the AV1 uncompressed frame header (specification section 5.9.2,
/// <c>uncompressed_header</c>). Parsing currently covers the intra (key-frame and intra-only) path,
/// which is sufficient to locate the tile group data and drive intra reconstruction.
/// </summary>
internal readonly struct ObuFrameHeader
{
    /// <summary>The <c>PRIMARY_REF_NONE</c> sentinel.</summary>
    public const int PrimaryReferenceNone = 7;

    /// <summary>Gets the frame type.</summary>
    public Av1FrameType FrameType { get; init; }

    /// <summary>Gets a value indicating whether the frame is intra-coded (key or intra-only).</summary>
    public bool FrameIsIntra => this.FrameType is Av1FrameType.Key or Av1FrameType.IntraOnly;

    /// <summary>Gets a value indicating whether the frame is shown directly.</summary>
    public bool ShowFrame { get; init; }

    /// <summary>Gets a value indicating whether CDF updates are disabled for this frame.</summary>
    public bool DisableCdfUpdate { get; init; }

    /// <summary>Gets a value indicating whether screen-content tools are allowed.</summary>
    public bool AllowScreenContentTools { get; init; }

    /// <summary>Gets a value indicating whether intra block copy is allowed.</summary>
    public bool AllowIntraBlockCopy { get; init; }

    /// <summary>Gets the coded frame width in pixels.</summary>
    public int FrameWidth { get; init; }

    /// <summary>Gets the coded frame height in pixels.</summary>
    public int FrameHeight { get; init; }

    /// <summary>Gets the render width in pixels.</summary>
    public int RenderWidth { get; init; }

    /// <summary>Gets the render height in pixels.</summary>
    public int RenderHeight { get; init; }

    /// <summary>Gets the number of 4x4 mode-info columns.</summary>
    public int ModeInfoColumns { get; init; }

    /// <summary>Gets the number of 4x4 mode-info rows.</summary>
    public int ModeInfoRows { get; init; }

    /// <summary>Gets the base-2 logarithm of the number of tile columns.</summary>
    public int TileColumnsLog2 { get; init; }

    /// <summary>Gets the base-2 logarithm of the number of tile rows.</summary>
    public int TileRowsLog2 { get; init; }

    /// <summary>Gets the number of bytes used to encode tile sizes.</summary>
    public int TileSizeBytes { get; init; }

    /// <summary>Gets the base quantizer index (<c>base_q_idx</c>).</summary>
    public int BaseQIndex { get; init; }

    /// <summary>Gets the luma DC quantizer delta.</summary>
    public int DeltaQYDc { get; init; }

    /// <summary>Gets the chroma U DC quantizer delta.</summary>
    public int DeltaQUDc { get; init; }

    /// <summary>Gets the chroma U AC quantizer delta.</summary>
    public int DeltaQUAc { get; init; }

    /// <summary>Gets the chroma V DC quantizer delta.</summary>
    public int DeltaQVDc { get; init; }

    /// <summary>Gets the chroma V AC quantizer delta.</summary>
    public int DeltaQVAc { get; init; }

    /// <summary>Gets a value indicating whether a quantizer matrix is used.</summary>
    public bool UsingQMatrix { get; init; }

    /// <summary>Gets a value indicating whether segmentation is enabled.</summary>
    public bool SegmentationEnabled { get; init; }

    /// <summary>Gets a value indicating whether per-block quantizer deltas are coded.</summary>
    public bool DeltaQPresent { get; init; }

    /// <summary>Gets the quantizer delta resolution.</summary>
    public int DeltaQResolution { get; init; }

    /// <summary>Gets a value indicating whether per-block loop-filter deltas are coded.</summary>
    public bool DeltaLfPresent { get; init; }

    /// <summary>Gets a value indicating whether every coefficient is coded losslessly.</summary>
    public bool CodedLossless { get; init; }

    /// <summary>Gets the transform mode (<c>TxMode</c>: 0 = ONLY_4X4, 1 = LARGEST, 2 = SELECT).</summary>
    public int TxMode { get; init; }

    /// <summary>Gets a value indicating whether the reduced transform-type set is used.</summary>
    public bool ReducedTxSet { get; init; }

    /// <summary>Gets the number of bits used to code each block's CDEF index.</summary>
    public int CdefBits { get; init; }

    /// <summary>Gets the CDEF strength parameters.</summary>
    public Cdef CdefParameters { get; init; }

    /// <summary>Gets the deblocking loop-filter parameters.</summary>
    public LoopFilter LoopFilterParameters { get; init; }

    /// <summary>Gets the bit position immediately after the uncompressed header (before byte alignment).</summary>
    public int EndBitPosition { get; init; }

    /// <summary>
    /// Parses an uncompressed frame header for the intra path (specification section 5.9.2).
    /// </summary>
    /// <param name="reader">The bit-stream reader positioned at the start of the header.</param>
    /// <param name="sequenceHeader">The active sequence header.</param>
    /// <returns>The parsed <see cref="ObuFrameHeader"/>.</returns>
    public static ObuFrameHeader ParseIntra(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader)
    {
        bool showExistingFrame = !sequenceHeader.ReducedStillPictureHeader && reader.ReadBoolean();
        if (showExistingFrame)
        {
            throw new NotSupportedException("show_existing_frame is not supported yet.");
        }

        Av1FrameType frameType = sequenceHeader.ReducedStillPictureHeader
            ? Av1FrameType.Key
            : (Av1FrameType)reader.ReadLiteral(2);
        bool frameIsIntra = frameType is Av1FrameType.Key or Av1FrameType.IntraOnly;
        if (!frameIsIntra)
        {
            throw new NotSupportedException("Inter frames are not supported yet.");
        }

        bool showFrame = sequenceHeader.ReducedStillPictureHeader || reader.ReadBoolean();
        if (!showFrame)
        {
            reader.ReadBoolean(); // showable_frame
        }

        bool errorResilientMode;
        if (frameType == Av1FrameType.Switch || (frameType == Av1FrameType.Key && showFrame))
        {
            errorResilientMode = true;
        }
        else
        {
            errorResilientMode = reader.ReadBoolean();
        }

        bool disableCdfUpdate = reader.ReadBoolean();

        bool allowScreenContentTools = sequenceHeader.ForceScreenContentTools == ObuSequenceHeader.Select
            ? reader.ReadBoolean()
            : sequenceHeader.ForceScreenContentTools != 0;

        if (allowScreenContentTools && sequenceHeader.ForceIntegerMotionVector == ObuSequenceHeader.Select)
        {
            reader.ReadBoolean(); // force_integer_mv (intra frames force it anyway)
        }

        if (sequenceHeader.FrameIdNumbersPresent)
        {
            reader.ReadLiteral(sequenceHeader.FrameIdLength); // current_frame_id
        }

        bool frameSizeOverride = frameType == Av1FrameType.Switch
            || (!sequenceHeader.ReducedStillPictureHeader && reader.ReadBoolean());

        reader.ReadLiteral(sequenceHeader.OrderHintBits); // order_hint

        // primary_ref_frame is PRIMARY_REF_NONE for intra/error-resilient frames.
        if (frameType == Av1FrameType.Key && showFrame)
        {
            // refresh_frame_flags is implicitly all-ones.
        }
        else
        {
            reader.ReadLiteral(8); // refresh_frame_flags
        }

        // frame_size().
        int frameWidth;
        int frameHeight;
        if (frameSizeOverride)
        {
            frameWidth = (int)reader.ReadLiteral(sequenceHeader.FrameWidthBits) + 1;
            frameHeight = (int)reader.ReadLiteral(sequenceHeader.FrameHeightBits) + 1;
        }
        else
        {
            frameWidth = sequenceHeader.MaxFrameWidth;
            frameHeight = sequenceHeader.MaxFrameHeight;
        }

        // superres_params(): the upscaled width equals the frame width unless super-res is used.
        if (sequenceHeader.EnableSuperResolution && reader.ReadBoolean())
        {
            reader.ReadLiteral(3); // coded_denom
        }

        // render_size().
        int renderWidth = frameWidth;
        int renderHeight = frameHeight;
        if (reader.ReadBoolean())
        {
            renderWidth = (int)reader.ReadLiteral(16) + 1;
            renderHeight = (int)reader.ReadLiteral(16) + 1;
        }

        bool allowIntraBlockCopy = false;
        if (allowScreenContentTools)
        {
            // UpscaledWidth == FrameWidth here (super-res handled above), so allow_intrabc is coded.
            allowIntraBlockCopy = reader.ReadBoolean();
        }

        // disable_frame_end_update_cdf.
        if (!sequenceHeader.ReducedStillPictureHeader && !disableCdfUpdate)
        {
            reader.ReadBoolean();
        }

        int modeInfoColumns = 2 * ((frameWidth + 7) >> 3);
        int modeInfoRows = 2 * ((frameHeight + 7) >> 3);

        TileInfo tile = ReadTileInfo(ref reader, sequenceHeader, modeInfoColumns, modeInfoRows);

        Quantization q = ReadQuantizationParams(ref reader, sequenceHeader);

        bool segmentationEnabled = ReadSegmentationParams(ref reader);

        // delta_q_params().
        bool deltaQPresent = false;
        int deltaQResolution = 0;
        if (q.BaseQIndex > 0)
        {
            deltaQPresent = reader.ReadBoolean();
        }

        if (deltaQPresent)
        {
            deltaQResolution = (int)reader.ReadLiteral(2);
        }

        // delta_lf_params().
        bool deltaLfPresent = false;
        if (deltaQPresent && !allowIntraBlockCopy)
        {
            deltaLfPresent = reader.ReadBoolean();
            if (deltaLfPresent)
            {
                reader.ReadLiteral(2); // delta_lf_res
                reader.ReadBoolean();  // delta_lf_multi
            }
        }

        // CodedLossless: with no segmentation, every block uses base_q_idx and the frame-level deltas.
        bool codedLossless = q.BaseQIndex == 0 && q.DeltaQYDc == 0 &&
            q.DeltaQUDc == 0 && q.DeltaQUAc == 0 && q.DeltaQVDc == 0 && q.DeltaQVAc == 0;

        LoopFilter loopFilter = ReadLoopFilterParams(ref reader, sequenceHeader, codedLossless, allowIntraBlockCopy);
        Cdef cdef = ReadCdefParams(ref reader, sequenceHeader, codedLossless, allowIntraBlockCopy);
        ReadLoopRestorationParams(ref reader, sequenceHeader, codedLossless, allowIntraBlockCopy);

        // read_tx_mode().
        int txMode = codedLossless ? 0 : (reader.ReadBoolean() ? 2 : 1);

        // frame_reference_mode() and skip_mode_params() contribute no bits for intra frames.
        bool reducedTxSet = reader.ReadBoolean();

        // global_motion_params() is empty for intra frames; film grain is gated by the sequence header.
        if (sequenceHeader.FilmGrainParamsPresent)
        {
            throw new NotSupportedException("Film grain parameters are not supported yet.");
        }

        return new ObuFrameHeader
        {
            FrameType = frameType,
            ShowFrame = showFrame,
            DisableCdfUpdate = disableCdfUpdate,
            AllowScreenContentTools = allowScreenContentTools,
            AllowIntraBlockCopy = allowIntraBlockCopy,
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            ModeInfoColumns = modeInfoColumns,
            ModeInfoRows = modeInfoRows,
            TileColumnsLog2 = tile.ColumnsLog2,
            TileRowsLog2 = tile.RowsLog2,
            TileSizeBytes = tile.SizeBytes,
            BaseQIndex = q.BaseQIndex,
            DeltaQYDc = q.DeltaQYDc,
            DeltaQUDc = q.DeltaQUDc,
            DeltaQUAc = q.DeltaQUAc,
            DeltaQVDc = q.DeltaQVDc,
            DeltaQVAc = q.DeltaQVAc,
            UsingQMatrix = q.UsingQMatrix,
            SegmentationEnabled = segmentationEnabled,
            DeltaQPresent = deltaQPresent,
            DeltaQResolution = deltaQResolution,
            DeltaLfPresent = deltaLfPresent,
            CodedLossless = codedLossless,
            TxMode = txMode,
            ReducedTxSet = reducedTxSet,
            CdefBits = cdef.Bits,
            CdefParameters = cdef,
            LoopFilterParameters = loopFilter,
            EndBitPosition = reader.BitPosition,
        };
    }

    private static TileInfo ReadTileInfo(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, int miCols, int miRows)
    {
        int sbShift = sequenceHeader.Use128x128Superblock ? 5 : 4;
        int sbCols = (miCols + (1 << sbShift) - 1) >> sbShift;
        int sbRows = (miRows + (1 << sbShift) - 1) >> sbShift;
        int sbSize = sbShift + 2;

        const int maxTileWidthSbConst = 4096;
        const int maxTileAreaConst = 4096 * 2304;
        int maxTileWidthSb = maxTileWidthSbConst >> sbSize;
        int maxTileAreaSb = maxTileAreaConst >> (2 * sbSize);
        int minLog2TileCols = TileLog2(maxTileWidthSb, sbCols);
        int maxLog2TileCols = TileLog2(1, Math.Min(sbCols, 64));
        int maxLog2TileRows = TileLog2(1, Math.Min(sbRows, 64));
        int minLog2Tiles = Math.Max(minLog2TileCols, TileLog2(maxTileAreaSb, sbRows * sbCols));

        int tileColumnsLog2;
        int tileRowsLog2;
        bool uniformTileSpacing = reader.ReadBoolean();
        if (uniformTileSpacing)
        {
            tileColumnsLog2 = minLog2TileCols;
            while (tileColumnsLog2 < maxLog2TileCols && reader.ReadBoolean())
            {
                tileColumnsLog2++;
            }

            int minLog2TileRows = Math.Max(minLog2Tiles - tileColumnsLog2, 0);
            tileRowsLog2 = minLog2TileRows;
            while (tileRowsLog2 < maxLog2TileRows && reader.ReadBoolean())
            {
                tileRowsLog2++;
            }
        }
        else
        {
            int widestTileSb = 0;
            int startSb = 0;
            int i = 0;
            for (; startSb < sbCols; i++)
            {
                int maxWidth = Math.Min(sbCols - startSb, maxTileWidthSb);
                int width = (int)reader.ReadNonSymmetric((uint)maxWidth) + 1;
                widestTileSb = Math.Max(width, widestTileSb);
                startSb += width;
            }

            tileColumnsLog2 = TileLog2(1, i);

            int maxTileAreaSb2 = widestTileSb > 0 ? Math.Max(maxTileAreaSb / widestTileSb, 1) : maxTileAreaSb;
            startSb = 0;
            int j = 0;
            for (; startSb < sbRows; j++)
            {
                int maxHeight = Math.Min(sbRows - startSb, maxTileAreaSb2);
                int height = (int)reader.ReadNonSymmetric((uint)maxHeight) + 1;
                startSb += height;
            }

            tileRowsLog2 = TileLog2(1, j);
        }

        int tileSizeBytes = 1;
        if (tileColumnsLog2 > 0 || tileRowsLog2 > 0)
        {
            reader.ReadLiteral(tileRowsLog2 + tileColumnsLog2); // context_update_tile_id
            tileSizeBytes = (int)reader.ReadLiteral(2) + 1;
        }

        return new TileInfo
        {
            ColumnsLog2 = tileColumnsLog2,
            RowsLog2 = tileRowsLog2,
            SizeBytes = tileSizeBytes,
        };
    }

    private static Quantization ReadQuantizationParams(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader)
    {
        int baseQIndex = (int)reader.ReadLiteral(8);
        int deltaQYDc = ReadDeltaQ(ref reader);

        int deltaQUDc = 0;
        int deltaQUAc = 0;
        int deltaQVDc = 0;
        int deltaQVAc = 0;
        if (sequenceHeader.NumPlanes > 1)
        {
            bool diffUvDelta = sequenceHeader.SeparateUvDeltaQ && reader.ReadBoolean();
            deltaQUDc = ReadDeltaQ(ref reader);
            deltaQUAc = ReadDeltaQ(ref reader);
            if (diffUvDelta)
            {
                deltaQVDc = ReadDeltaQ(ref reader);
                deltaQVAc = ReadDeltaQ(ref reader);
            }
            else
            {
                deltaQVDc = deltaQUDc;
                deltaQVAc = deltaQUAc;
            }
        }

        bool usingQMatrix = reader.ReadBoolean();
        if (usingQMatrix)
        {
            reader.ReadLiteral(4); // qm_y
            reader.ReadLiteral(4); // qm_u
            if (sequenceHeader.SeparateUvDeltaQ)
            {
                reader.ReadLiteral(4); // qm_v
            }
        }

        return new Quantization
        {
            BaseQIndex = baseQIndex,
            DeltaQYDc = deltaQYDc,
            DeltaQUDc = deltaQUDc,
            DeltaQUAc = deltaQUAc,
            DeltaQVDc = deltaQVDc,
            DeltaQVAc = deltaQVAc,
            UsingQMatrix = usingQMatrix,
        };
    }

    private static int ReadDeltaQ(ref Av1BitStreamReader reader)
        => reader.ReadBoolean() ? reader.ReadSignedLiteral(7) : 0;

    private static bool ReadSegmentationParams(ref Av1BitStreamReader reader)
    {
        // Feature bit widths and signedness (specification tables Segmentation_Feature_Bits/Signed).
        ReadOnlySpan<int> featureBits = [8, 6, 6, 6, 6, 3, 0, 0];
        ReadOnlySpan<int> featureSigned = [1, 1, 1, 1, 1, 0, 0, 0];

        bool segmentationEnabled = reader.ReadBoolean();
        if (segmentationEnabled)
        {
            // primary_ref_frame is PRIMARY_REF_NONE for intra frames, so the map and data are updated.
            const int segmentCount = 8;
            const int featureCount = 8;
            for (int i = 0; i < segmentCount; i++)
            {
                for (int j = 0; j < featureCount; j++)
                {
                    bool featureEnabled = reader.ReadBoolean();
                    if (featureEnabled)
                    {
                        int bits = featureBits[j];
                        if (bits > 0)
                        {
                            if (featureSigned[j] == 1)
                            {
                                reader.ReadSignedLiteral(bits + 1);
                            }
                            else
                            {
                                reader.ReadLiteral(bits);
                            }
                        }
                    }
                }
            }
        }

        return segmentationEnabled;
    }

    private static LoopFilter ReadLoopFilterParams(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, bool codedLossless, bool allowIntraBlockCopy)
    {
        // Spec defaults established by setup_past_independence for a key frame.
        int[] refDeltas = [1, 0, 0, 0, -1, 0, -1, -1];
        int[] modeDeltas = [0, 0];

        if (codedLossless || allowIntraBlockCopy)
        {
            return new LoopFilter { Levels = [0, 0, 0, 0], Sharpness = 0, DeltaEnabled = false, RefDeltas = refDeltas, ModeDeltas = modeDeltas };
        }

        int level0 = (int)reader.ReadLiteral(6);
        int level1 = (int)reader.ReadLiteral(6);
        int level2 = 0;
        int level3 = 0;
        if (sequenceHeader.NumPlanes > 1 && (level0 != 0 || level1 != 0))
        {
            level2 = (int)reader.ReadLiteral(6);
            level3 = (int)reader.ReadLiteral(6);
        }

        int sharpness = (int)reader.ReadLiteral(3);

        bool deltaEnabled = reader.ReadBoolean();
        bool deltaUpdate = deltaEnabled && reader.ReadBoolean();
        if (deltaUpdate)
        {
            for (int i = 0; i < 8; i++)
            {
                if (reader.ReadBoolean())
                {
                    refDeltas[i] = reader.ReadSignedLiteral(7);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                if (reader.ReadBoolean())
                {
                    modeDeltas[i] = reader.ReadSignedLiteral(7);
                }
            }
        }

        return new LoopFilter
        {
            Levels = [level0, level1, level2, level3],
            Sharpness = sharpness,
            DeltaEnabled = deltaEnabled,
            RefDeltas = refDeltas,
            ModeDeltas = modeDeltas,
        };
    }

    private static Cdef ReadCdefParams(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, bool codedLossless, bool allowIntraBlockCopy)
    {
        if (codedLossless || allowIntraBlockCopy || !sequenceHeader.EnableCdef)
        {
            // A single all-zero preset: CDEF performs no filtering.
            return new Cdef { Damping = 3, Bits = 0, YPrimary = [0], YSecondary = [0], UvPrimary = [0], UvSecondary = [0] };
        }

        int damping = (int)reader.ReadLiteral(2) + 3;
        int cdefBits = (int)reader.ReadLiteral(2);
        int count = 1 << cdefBits;
        int[] yPri = new int[count];
        int[] ySec = new int[count];
        int[] uvPri = new int[count];
        int[] uvSec = new int[count];
        for (int i = 0; i < count; i++)
        {
            yPri[i] = (int)reader.ReadLiteral(4);
            ySec[i] = (int)reader.ReadLiteral(2);
            if (ySec[i] == 3)
            {
                ySec[i]++;
            }

            if (sequenceHeader.NumPlanes > 1)
            {
                uvPri[i] = (int)reader.ReadLiteral(4);
                uvSec[i] = (int)reader.ReadLiteral(2);
                if (uvSec[i] == 3)
                {
                    uvSec[i]++;
                }
            }
        }

        return new Cdef { Damping = damping, Bits = cdefBits, YPrimary = yPri, YSecondary = ySec, UvPrimary = uvPri, UvSecondary = uvSec };
    }

    private static void ReadLoopRestorationParams(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, bool codedLossless, bool allowIntraBlockCopy)
    {
        if (codedLossless || allowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            return;
        }

        bool usesLr = false;
        bool usesChromaLr = false;
        for (int i = 0; i < sequenceHeader.NumPlanes; i++)
        {
            int lrType = (int)reader.ReadLiteral(2);
            if (lrType != 0)
            {
                usesLr = true;
                if (i > 0)
                {
                    usesChromaLr = true;
                }
            }
        }

        if (usesLr)
        {
            if (sequenceHeader.Use128x128Superblock)
            {
                reader.ReadLiteral(1); // lr_unit_shift
            }
            else
            {
                bool lrUnitShift = reader.ReadBoolean();
                if (lrUnitShift)
                {
                    reader.ReadLiteral(1); // lr_unit_extra_shift
                }
            }

            if (sequenceHeader.SubsamplingX == 1 && sequenceHeader.SubsamplingY == 1 && usesChromaLr)
            {
                reader.ReadLiteral(1); // lr_uv_shift
            }
        }
    }

    private static int TileLog2(int blockSize, int target)
    {
        int k = 0;
        while ((blockSize << k) < target)
        {
            k++;
        }

        return k;
    }

    private readonly struct TileInfo
    {
        public int ColumnsLog2 { get; init; }

        public int RowsLog2 { get; init; }

        public int SizeBytes { get; init; }
    }

    /// <summary>
    /// The CDEF (constrained directional enhancement filter) parameters from the frame header
    /// (specification section 5.9.19). Strengths are indexed by the per-block CDEF preset.
    /// </summary>
    public readonly struct Cdef
    {
        /// <summary>Gets the CDEF damping value (<c>cdef_damping_minus_3 + 3</c>).</summary>
        public int Damping { get; init; }

        /// <summary>Gets the number of bits used to select the per-block CDEF preset.</summary>
        public int Bits { get; init; }

        /// <summary>Gets the luma primary strengths per preset.</summary>
        public int[] YPrimary { get; init; }

        /// <summary>Gets the luma secondary strengths per preset.</summary>
        public int[] YSecondary { get; init; }

        /// <summary>Gets the chroma primary strengths per preset.</summary>
        public int[] UvPrimary { get; init; }

        /// <summary>Gets the chroma secondary strengths per preset.</summary>
        public int[] UvSecondary { get; init; }
    }

    /// <summary>
    /// The deblocking loop-filter parameters from the frame header (specification section 5.9.11).
    /// </summary>
    public readonly struct LoopFilter
    {
        /// <summary>Gets the filter levels [Y vertical, Y horizontal, U, V].</summary>
        public int[] Levels { get; init; }

        /// <summary>Gets the loop-filter sharpness.</summary>
        public int Sharpness { get; init; }

        /// <summary>Gets a value indicating whether reference/mode delta adjustment is enabled.</summary>
        public bool DeltaEnabled { get; init; }

        /// <summary>Gets the per-reference-frame filter-level deltas.</summary>
        public int[] RefDeltas { get; init; }

        /// <summary>Gets the per-mode filter-level deltas.</summary>
        public int[] ModeDeltas { get; init; }
    }

    private readonly struct Quantization
    {
        public int BaseQIndex { get; init; }

        public int DeltaQYDc { get; init; }

        public int DeltaQUDc { get; init; }

        public int DeltaQUAc { get; init; }

        public int DeltaQVDc { get; init; }

        public int DeltaQVAc { get; init; }

        public bool UsingQMatrix { get; init; }
    }
}
