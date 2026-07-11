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

    /// <summary>
    /// Gets a value indicating whether the frame-end CDF state reverts to the frame's initial state
    /// instead of keeping the in-frame adaptation (<c>disable_frame_end_update_cdf</c>).
    /// </summary>
    public bool DisableFrameEndUpdateCdf { get; init; }

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

    /// <summary>Gets the tile column boundaries in 4x4 units: one start per tile column plus the frame
    /// end, so column <c>i</c> spans <c>[TileColumnStarts[i], TileColumnStarts[i + 1])</c>.</summary>
    public int[]? TileColumnStarts { get; init; }

    /// <summary>Gets the tile row boundaries in 4x4 units (same layout as
    /// <see cref="TileColumnStarts"/>).</summary>
    public int[]? TileRowStarts { get; init; }

    /// <summary>Gets the tile whose frame-end CDF state a later frame inherits
    /// (<c>context_update_tile_id</c>).</summary>
    public int ContextUpdateTileId { get; init; }

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

    /// <summary>Gets the luma quantizer-matrix level (15 codes flat matrices).</summary>
    public int QmY { get; init; }

    /// <summary>Gets the U-plane quantizer-matrix level.</summary>
    public int QmU { get; init; }

    /// <summary>Gets the V-plane quantizer-matrix level.</summary>
    public int QmV { get; init; }

    /// <summary>Gets a value indicating whether segmentation is enabled.</summary>
    public bool SegmentationEnabled { get; init; }

    /// <summary>Gets the segmentation parameters.</summary>
    public ObuSegmentationParams SegmentationParams { get; init; }

    /// <summary>Gets the upscaled (output/reference) frame width; equals <see cref="FrameWidth"/>
    /// unless super-resolution is used.</summary>
    public int UpscaledWidth { get; init; }

    /// <summary>Gets the super-resolution denominator (8 when super-resolution is off).</summary>
    public int SuperresDenominator { get; init; }

    /// <summary>Gets a value indicating whether the frame codes at a reduced super-resolution width.</summary>
    public bool UseSuperres => this.SuperresDenominator > 8;

    /// <summary>Gets the film-grain parameters, or <see langword="null"/> when no grain applies.</summary>
    public ObuFilmGrainParams? FilmGrain { get; init; }

    /// <summary>Gets the per-block loop-filter delta resolution (log2).</summary>
    public int DeltaLfResolution { get; init; }

    /// <summary>Gets a value indicating whether per-block loop-filter deltas are coded per
    /// component (multi) instead of a single shared delta.</summary>
    public bool DeltaLfMulti { get; init; }

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

    /// <summary>Gets the loop-restoration parameters.</summary>
    public LoopRestoration LoopRestorationParameters { get; init; }

    /// <summary>Gets the bit position immediately after the uncompressed header (before byte alignment).</summary>
    public int EndBitPosition { get; init; }

    /// <summary>Gets the order hint of this frame.</summary>
    public int OrderHint { get; init; }

    /// <summary>Gets the eight-bit mask of reference slots this frame refreshes.</summary>
    public int RefreshFrameFlags { get; init; }

    /// <summary>Gets the primary reference frame index (7 = none).</summary>
    public int PrimaryRefFrame { get; init; }

    /// <summary>Gets the seven reference-frame slot indices used by this inter frame.</summary>
    public int[] ReferenceFrameIndices { get; init; }

    /// <summary>Gets a value indicating whether motion vectors use eighth-pel (high) precision.</summary>
    public bool AllowHighPrecisionMv { get; init; }

    /// <summary>Gets a value indicating whether motion vectors are forced to whole pels.</summary>
    public bool ForceIntegerMv { get; init; }

    /// <summary>Gets the interpolation filter selection (0-2 fixed, 4 = switchable).</summary>
    public int InterpolationFilter { get; init; }

    /// <summary>Gets a value indicating whether the motion mode is switchable per block.</summary>
    public bool IsMotionModeSwitchable { get; init; }

    /// <summary>Gets a value indicating whether temporal (reference-frame) motion vectors are used.</summary>
    public bool UseReferenceFrameMotionVectors { get; init; }

    /// <summary>Gets a value indicating whether the compound reference mode is switchable.</summary>
    public bool ReferenceSelect { get; init; }

    /// <summary>Gets a value indicating whether skip-mode is enabled for this frame.</summary>
    public bool SkipModeEnabled { get; init; }

    /// <summary>Gets the derived zero-based skip-mode reference pair (valid when skip-mode is
    /// enabled).</summary>
    public int[]? SkipModeReferences { get; init; }

    /// <summary>Gets a value indicating whether warped motion is allowed.</summary>
    public bool AllowWarpedMotion { get; init; }

    /// <summary>Gets the seven per-reference global-motion models (identity for intra frames).</summary>
    public Av1WarpedMotionParams[] GlobalMotionParams { get; init; }

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
        bool showableFrame = showFrame ? frameType != Av1FrameType.Key : reader.ReadBoolean();

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

        int orderHintIntra = (int)reader.ReadLiteral(sequenceHeader.OrderHintBits); // order_hint

        // primary_ref_frame is PRIMARY_REF_NONE for intra/error-resilient frames.
        int refreshFrameFlagsIntra;
        if (frameType == Av1FrameType.Key && showFrame)
        {
            refreshFrameFlagsIntra = 0xFF; // refresh_frame_flags is implicitly all-ones.
        }
        else
        {
            refreshFrameFlagsIntra = (int)reader.ReadLiteral(8); // refresh_frame_flags
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

        // superres_params(): the frame codes at a reduced width and is upscaled for output/reference.
        int upscaledWidthIntra = frameWidth;
        int superresDenomIntra = 8;
        if (sequenceHeader.EnableSuperResolution && reader.ReadBoolean())
        {
            superresDenomIntra = (int)reader.ReadLiteral(3) + 9;
            frameWidth = Math.Max(((upscaledWidthIntra * 8) + (superresDenomIntra / 2)) / superresDenomIntra, Math.Min(16, upscaledWidthIntra));
        }

        // render_size().
        int renderWidth = upscaledWidthIntra;
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

        bool disableFrameEndUpdateCdf = sequenceHeader.ReducedStillPictureHeader || disableCdfUpdate || reader.ReadBoolean();

        int modeInfoColumns = 2 * ((frameWidth + 7) >> 3);
        int modeInfoRows = 2 * ((frameHeight + 7) >> 3);

        TileInfo tile = ReadTileInfo(ref reader, sequenceHeader, modeInfoColumns, modeInfoRows);

        Quantization q = ReadQuantizationParams(ref reader, sequenceHeader);

        ObuSegmentationParams segmentation = ReadSegmentationParams(ref reader, primaryRefNone: true, inherited: null);

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
        int deltaLfResolution = 0;
        bool deltaLfMulti = false;
        if (deltaQPresent && !allowIntraBlockCopy)
        {
            deltaLfPresent = reader.ReadBoolean();
            if (deltaLfPresent)
            {
                deltaLfResolution = (int)reader.ReadLiteral(2);
                deltaLfMulti = reader.ReadBoolean();
            }
        }

        // CodedLossless: with no segmentation, every block uses base_q_idx and the frame-level deltas.
        bool codedLossless = q.BaseQIndex == 0 && q.DeltaQYDc == 0 &&
            q.DeltaQUDc == 0 && q.DeltaQUAc == 0 && q.DeltaQVDc == 0 && q.DeltaQVAc == 0;

        LoopFilter loopFilter = ReadLoopFilterParams(ref reader, sequenceHeader, codedLossless, allowIntraBlockCopy);
        Cdef cdef = ReadCdefParams(ref reader, sequenceHeader, codedLossless, allowIntraBlockCopy);
        LoopRestoration loopRestoration = ReadLoopRestorationParams(ref reader, sequenceHeader, codedLossless, allowIntraBlockCopy);

        // read_tx_mode().
        int txMode = codedLossless ? 0 : (reader.ReadBoolean() ? 2 : 1);

        // frame_reference_mode() and skip_mode_params() contribute no bits for intra frames.
        bool reducedTxSet = reader.ReadBoolean();

        // global_motion_params() is empty for intra frames; film grain is gated by the sequence header.
        ObuFilmGrainParams? filmGrain = null;
        if (sequenceHeader.FilmGrainParamsPresent && (showFrame || showableFrame))
        {
            filmGrain = ObuFilmGrainParams.Parse(ref reader, sequenceHeader, frameType, referenceGrain: null);
        }

        return new ObuFrameHeader
        {
            FilmGrain = filmGrain,
            FrameType = frameType,
            ShowFrame = showFrame,
            DisableCdfUpdate = disableCdfUpdate,
            DisableFrameEndUpdateCdf = disableFrameEndUpdateCdf,
            AllowScreenContentTools = allowScreenContentTools,
            AllowIntraBlockCopy = allowIntraBlockCopy,
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            UpscaledWidth = upscaledWidthIntra,
            SuperresDenominator = superresDenomIntra,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            ModeInfoColumns = modeInfoColumns,
            ModeInfoRows = modeInfoRows,
            TileColumnsLog2 = tile.ColumnsLog2,
            TileRowsLog2 = tile.RowsLog2,
            TileSizeBytes = tile.SizeBytes,
            TileColumnStarts = tile.ColumnStarts,
            TileRowStarts = tile.RowStarts,
            ContextUpdateTileId = tile.ContextUpdateTileId,
            BaseQIndex = q.BaseQIndex,
            DeltaQYDc = q.DeltaQYDc,
            DeltaQUDc = q.DeltaQUDc,
            DeltaQUAc = q.DeltaQUAc,
            DeltaQVDc = q.DeltaQVDc,
            DeltaQVAc = q.DeltaQVAc,
            UsingQMatrix = q.UsingQMatrix,
            QmY = q.QmY,
            QmU = q.QmU,
            QmV = q.QmV,
            SegmentationEnabled = segmentation.Enabled,
            SegmentationParams = segmentation,
            DeltaLfResolution = deltaLfResolution,
            DeltaLfMulti = deltaLfMulti,
            DeltaQPresent = deltaQPresent,
            DeltaQResolution = deltaQResolution,
            DeltaLfPresent = deltaLfPresent,
            CodedLossless = codedLossless,
            TxMode = txMode,
            ReducedTxSet = reducedTxSet,
            CdefBits = cdef.Bits,
            CdefParameters = cdef,
            LoopFilterParameters = loopFilter,
            LoopRestorationParameters = loopRestoration,
            OrderHint = orderHintIntra,
            RefreshFrameFlags = refreshFrameFlagsIntra,
            GlobalMotionParams = [Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity],
            EndBitPosition = reader.BitPosition,
        };
    }

    /// <summary>
    /// Parses an uncompressed frame header for an inter frame (specification section 5.9.2). Only the
    /// feature subset exercised by simple single-reference clips is supported; rarer paths (short ref
    /// signalling, primary-ref context loading, non-identity global motion, film grain) throw.
    /// </summary>
    /// <param name="reader">The bit-stream reader positioned at the start of the header.</param>
    /// <param name="sequenceHeader">The active sequence header.</param>
    /// <param name="referenceOrderHints">The order hints of the eight reference slots.</param>
    /// <returns>The parsed inter frame header.</returns>
    public static ObuFrameHeader ParseInter(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, int[] referenceOrderHints, ObuPrimaryReferenceState?[]? slotStates = null)
    {
        bool showExistingFrame = !sequenceHeader.ReducedStillPictureHeader && reader.ReadBoolean();
        if (showExistingFrame)
        {
            throw new NotSupportedException("show_existing_frame is not supported yet.");
        }

        Av1FrameType frameType = (Av1FrameType)reader.ReadLiteral(2);
        if (frameType is Av1FrameType.Key or Av1FrameType.IntraOnly)
        {
            throw new InvalidOperationException("ParseInter called for an intra frame.");
        }

        bool showFrame = reader.ReadBoolean();
        bool showableFrame = showFrame ? frameType != Av1FrameType.Key : reader.ReadBoolean();

        bool errorResilientMode = frameType == Av1FrameType.Switch || reader.ReadBoolean();
        bool disableCdfUpdate = reader.ReadBoolean();

        bool allowScreenContentTools = sequenceHeader.ForceScreenContentTools == ObuSequenceHeader.Select
            ? reader.ReadBoolean()
            : sequenceHeader.ForceScreenContentTools != 0;

        bool forceIntegerMv = false;
        if (allowScreenContentTools)
        {
            forceIntegerMv = sequenceHeader.ForceIntegerMotionVector == ObuSequenceHeader.Select
                ? reader.ReadBoolean()
                : sequenceHeader.ForceIntegerMotionVector != 0;
        }

        if (sequenceHeader.FrameIdNumbersPresent)
        {
            reader.ReadLiteral(sequenceHeader.FrameIdLength); // current_frame_id
        }

        bool frameSizeOverride = frameType == Av1FrameType.Switch || reader.ReadBoolean();
        int orderHint = (int)reader.ReadLiteral(sequenceHeader.OrderHintBits);
        int primaryRefFrame = errorResilientMode ? 7 : (int)reader.ReadLiteral(3);

        int refreshFrameFlags = frameType == Av1FrameType.Switch ? 0xFF : (int)reader.ReadLiteral(8);
        if (errorResilientMode && sequenceHeader.EnableOrderHint)
        {
            for (int i = 0; i < 8; i++)
            {
                reader.ReadLiteral(sequenceHeader.OrderHintBits); // ref_order_hint[i]
            }
        }

        int[] refFrameIndices = new int[7];
        if (sequenceHeader.EnableOrderHint && reader.ReadBoolean())
        {
            throw new NotSupportedException("frame_ref_short_signaling is not supported yet.");
        }

        for (int i = 0; i < 7; i++)
        {
            refFrameIndices[i] = (int)reader.ReadLiteral(3);
            if (sequenceHeader.FrameIdNumbersPresent)
            {
                reader.ReadLiteral(sequenceHeader.DeltaFrameIdLength); // delta_frame_id_minus_1
            }
        }

        bool useRef = !errorResilientMode && frameSizeOverride;
        FrameSizeResult size = ReadFrameSizeInter(ref reader, sequenceHeader, useRef, frameSizeOverride, refFrameIndices, slotStates);

        bool allowHighPrecisionMv = !forceIntegerMv && reader.ReadBoolean();
        int interpolationFilter = reader.ReadBoolean() ? 4 : (int)reader.ReadLiteral(2);
        bool isMotionModeSwitchable = reader.ReadBoolean();
        bool useReferenceFrameMotionVectors = !errorResilientMode && sequenceHeader.EnableReferenceFrameMotionVectors
            && sequenceHeader.EnableOrderHint && reader.ReadBoolean();

        bool disableFrameEndUpdateCdf = sequenceHeader.ReducedStillPictureHeader || disableCdfUpdate || reader.ReadBoolean();

        int modeInfoColumns = 2 * ((size.FrameWidth + 7) >> 3);
        int modeInfoRows = 2 * ((size.FrameHeight + 7) >> 3);

        // With a primary reference the loop-filter deltas, segmentation feature table and global-motion
        // models start from the reference's saved state (load_previous) instead of the
        // setup_past_independence defaults.
        ObuPrimaryReferenceState inherited = primaryRefFrame != PrimaryReferenceNone && slotStates?[refFrameIndices[primaryRefFrame]] is { } saved
            ? saved
            : ObuPrimaryReferenceState.CreateDefault();

        TileInfo tile = ReadTileInfo(ref reader, sequenceHeader, modeInfoColumns, modeInfoRows);
        Quantization q = ReadQuantizationParams(ref reader, sequenceHeader);
        ObuSegmentationParams segmentation = ReadSegmentationParams(
            ref reader, primaryRefNone: primaryRefFrame == PrimaryReferenceNone, inherited: inherited.Segmentation);

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

        bool deltaLfPresent = false;
        int deltaLfResolution = 0;
        bool deltaLfMulti = false;
        if (deltaQPresent)
        {
            deltaLfPresent = reader.ReadBoolean();
            if (deltaLfPresent)
            {
                deltaLfResolution = (int)reader.ReadLiteral(2);
                deltaLfMulti = reader.ReadBoolean();
            }
        }

        bool codedLossless = q.BaseQIndex == 0 && q.DeltaQYDc == 0 &&
            q.DeltaQUDc == 0 && q.DeltaQUAc == 0 && q.DeltaQVDc == 0 && q.DeltaQVAc == 0;

        LoopFilter loopFilter = ReadLoopFilterParams(ref reader, sequenceHeader, codedLossless, false, inherited);
        Cdef cdef = ReadCdefParams(ref reader, sequenceHeader, codedLossless, false);
        LoopRestoration loopRestoration = ReadLoopRestorationParams(ref reader, sequenceHeader, codedLossless, false);

        int txMode = codedLossless ? 0 : (reader.ReadBoolean() ? 2 : 1);

        bool referenceSelect = reader.ReadBoolean();

        bool skipModeAllowed = ComputeSkipModeAllowed(referenceSelect, sequenceHeader, orderHint, refFrameIndices, referenceOrderHints, out int[] skipModeReferences);
        bool skipModeEnabled = skipModeAllowed && reader.ReadBoolean();

        bool allowWarpedMotion = !errorResilientMode && sequenceHeader.EnableWarpedMotion && reader.ReadBoolean();
        bool reducedTxSet = reader.ReadBoolean();

        // global_motion_params(): one model per reference, with parameter deltas coded against the
        // primary reference's saved models (identity when there is none).
        Av1WarpedMotionParams[] globalMotion = new Av1WarpedMotionParams[7];
        for (int i = 0; i < 7; i++)
        {
            globalMotion[i] = ReadGlobalMotionParams(ref reader, inherited.GlobalMotion[i], allowHighPrecisionMv);
        }

        ObuFilmGrainParams? filmGrain = null;
        if (sequenceHeader.FilmGrainParamsPresent && (showFrame || showableFrame))
        {
            ObuFilmGrainParams?[]? slotGrain = null;
            if (slotStates is not null)
            {
                slotGrain = new ObuFilmGrainParams?[slotStates.Length];
                for (int i = 0; i < slotStates.Length; i++)
                {
                    slotGrain[i] = slotStates[i]?.FilmGrain;
                }
            }

            filmGrain = ObuFilmGrainParams.Parse(ref reader, sequenceHeader, frameType, slotGrain);
        }

        return new ObuFrameHeader
        {
            FilmGrain = filmGrain,
            FrameType = frameType,
            ShowFrame = showFrame,
            DisableCdfUpdate = disableCdfUpdate,
            DisableFrameEndUpdateCdf = disableFrameEndUpdateCdf,
            AllowScreenContentTools = allowScreenContentTools,
            FrameWidth = size.FrameWidth,
            FrameHeight = size.FrameHeight,
            UpscaledWidth = size.UpscaledWidth,
            SuperresDenominator = size.SuperresDenominator,
            RenderWidth = size.RenderWidth,
            RenderHeight = size.RenderHeight,
            ModeInfoColumns = modeInfoColumns,
            ModeInfoRows = modeInfoRows,
            TileColumnsLog2 = tile.ColumnsLog2,
            TileRowsLog2 = tile.RowsLog2,
            TileSizeBytes = tile.SizeBytes,
            TileColumnStarts = tile.ColumnStarts,
            TileRowStarts = tile.RowStarts,
            ContextUpdateTileId = tile.ContextUpdateTileId,
            BaseQIndex = q.BaseQIndex,
            DeltaQYDc = q.DeltaQYDc,
            DeltaQUDc = q.DeltaQUDc,
            DeltaQUAc = q.DeltaQUAc,
            DeltaQVDc = q.DeltaQVDc,
            DeltaQVAc = q.DeltaQVAc,
            UsingQMatrix = q.UsingQMatrix,
            QmY = q.QmY,
            QmU = q.QmU,
            QmV = q.QmV,
            SegmentationEnabled = segmentation.Enabled,
            SegmentationParams = segmentation,
            DeltaLfResolution = deltaLfResolution,
            DeltaLfMulti = deltaLfMulti,
            DeltaQPresent = deltaQPresent,
            DeltaQResolution = deltaQResolution,
            DeltaLfPresent = deltaLfPresent,
            CodedLossless = codedLossless,
            TxMode = txMode,
            ReducedTxSet = reducedTxSet,
            CdefBits = cdef.Bits,
            CdefParameters = cdef,
            LoopFilterParameters = loopFilter,
            LoopRestorationParameters = loopRestoration,
            OrderHint = orderHint,
            RefreshFrameFlags = refreshFrameFlags,
            PrimaryRefFrame = primaryRefFrame,
            ReferenceFrameIndices = refFrameIndices,
            AllowHighPrecisionMv = allowHighPrecisionMv,
            ForceIntegerMv = forceIntegerMv,
            InterpolationFilter = interpolationFilter,
            IsMotionModeSwitchable = isMotionModeSwitchable,
            UseReferenceFrameMotionVectors = useReferenceFrameMotionVectors,
            ReferenceSelect = referenceSelect,
            SkipModeEnabled = skipModeEnabled,
            SkipModeReferences = skipModeReferences,
            AllowWarpedMotion = allowWarpedMotion,
            GlobalMotionParams = globalMotion,
            EndBitPosition = reader.BitPosition,
        };
    }

    // Order-hint difference with wrap-around (dav1d get_poc_diff).
    private static int GetOrderHintDiff(int orderHintBits, int a, int b)
    {
        if (orderHintBits == 0)
        {
            return 0;
        }

        int mask = 1 << (orderHintBits - 1);
        int diff = a - b;
        return (diff & (mask - 1)) - (diff & mask);
    }

    // dav1d skip_mode_params allowed computation: requires either a forward and backward reference, or
    // two distinct backward references, among the seven reference frames (by order hint).
    private static bool ComputeSkipModeAllowed(bool referenceSelect, in ObuSequenceHeader sequenceHeader, int orderHint, int[] refFrameIndices, int[] referenceOrderHints, out int[] skipModeReferences)
    {
        skipModeReferences = [0, 0];
        if (!referenceSelect || !sequenceHeader.EnableOrderHint)
        {
            return false;
        }

        int bits = sequenceHeader.OrderHintBits;
        int offBefore = -1;
        int offAfter = -1;
        int offBeforeIdx = -1;
        int offAfterIdx = -1;
        for (int i = 0; i < 7; i++)
        {
            int refPoc = referenceOrderHints[refFrameIndices[i]];
            int diff = GetOrderHintDiff(bits, refPoc, orderHint);
            if (diff > 0)
            {
                if (offAfter < 0 || GetOrderHintDiff(bits, offAfter, refPoc) > 0)
                {
                    offAfter = refPoc;
                    offAfterIdx = i;
                }
            }
            else if (diff < 0 && (offBefore < 0 || GetOrderHintDiff(bits, refPoc, offBefore) > 0))
            {
                offBefore = refPoc;
                offBeforeIdx = i;
            }
        }

        if (offBefore >= 0 && offAfter >= 0)
        {
            skipModeReferences = [Math.Min(offBeforeIdx, offAfterIdx), Math.Max(offBeforeIdx, offAfterIdx)];
            return true;
        }

        if (offBefore >= 0)
        {
            int offBefore2 = -1;
            int offBefore2Idx = -1;
            for (int i = 0; i < 7; i++)
            {
                int refPoc = referenceOrderHints[refFrameIndices[i]];
                if (GetOrderHintDiff(bits, refPoc, offBefore) < 0 &&
                    (offBefore2 < 0 || GetOrderHintDiff(bits, refPoc, offBefore2) > 0))
                {
                    offBefore2 = refPoc;
                    offBefore2Idx = i;
                }
            }

            if (offBefore2 >= 0)
            {
                skipModeReferences = [Math.Min(offBeforeIdx, offBefore2Idx), Math.Max(offBeforeIdx, offBefore2Idx)];
                return true;
            }

            return false;
        }

        return false;
    }

    private readonly struct FrameSizeResult
    {
        public FrameSizeResult(int frameWidth, int frameHeight, int renderWidth, int renderHeight, int upscaledWidth = 0, int superresDenominator = 8)
        {
            this.FrameWidth = frameWidth;
            this.FrameHeight = frameHeight;
            this.RenderWidth = renderWidth;
            this.RenderHeight = renderHeight;
            this.UpscaledWidth = upscaledWidth == 0 ? frameWidth : upscaledWidth;
            this.SuperresDenominator = superresDenominator;
        }

        public int FrameWidth { get; }

        public int FrameHeight { get; }

        public int RenderWidth { get; }

        public int RenderHeight { get; }

        public int UpscaledWidth { get; }

        public int SuperresDenominator { get; }
    }

    // dav1d read_frame_size for inter, including the frame_size_with_refs found-reference path: the
    // frame inherits the first flagged reference's stored size and render size, then codes its own
    // superres_params against that inherited upscaled width.
    private static FrameSizeResult ReadFrameSizeInter(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, bool useRef, bool frameSizeOverride, int[] refFrameIndices, ObuPrimaryReferenceState?[]? slotStates)
    {
        if (useRef)
        {
            for (int i = 0; i < 7; i++)
            {
                if (reader.ReadBoolean())
                {
                    ObuPrimaryReferenceState reference = slotStates?[refFrameIndices[i]]
                        ?? throw new InvalidDataException("frame_size_with_refs points at an empty reference slot.");
                    int inheritedUpscaledWidth = reference.UpscaledWidth;
                    int inheritedHeight = reference.FrameHeight;
                    if (inheritedUpscaledWidth == 0 || inheritedHeight == 0)
                    {
                        throw new InvalidDataException("frame_size_with_refs points at a reference without a stored size.");
                    }

                    int codedWidth = inheritedUpscaledWidth;
                    int denom = 8;
                    if (sequenceHeader.EnableSuperResolution && reader.ReadBoolean())
                    {
                        denom = (int)reader.ReadLiteral(3) + 9;
                        codedWidth = Math.Max(((inheritedUpscaledWidth * 8) + (denom / 2)) / denom, Math.Min(16, inheritedUpscaledWidth));
                    }

                    return new FrameSizeResult(codedWidth, inheritedHeight, reference.RenderWidth, reference.RenderHeight, inheritedUpscaledWidth, denom);
                }
            }
        }

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

        int upscaledWidth = frameWidth;
        int superresDenom = 8;
        if (sequenceHeader.EnableSuperResolution && reader.ReadBoolean())
        {
            superresDenom = (int)reader.ReadLiteral(3) + 9;
            frameWidth = Math.Max(((upscaledWidth * 8) + (superresDenom / 2)) / superresDenom, Math.Min(16, upscaledWidth));
        }

        int renderWidth = upscaledWidth;
        int renderHeight = frameHeight;
        if (reader.ReadBoolean())
        {
            renderWidth = (int)reader.ReadLiteral(16) + 1;
            renderHeight = (int)reader.ReadLiteral(16) + 1;
        }

        return new FrameSizeResult(frameWidth, frameHeight, renderWidth, renderHeight, upscaledWidth, superresDenom);
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
        List<int> columnStartsSb = [];
        List<int> rowStartsSb = [];
        bool uniformTileSpacing = reader.ReadBoolean();
        if (uniformTileSpacing)
        {
            tileColumnsLog2 = minLog2TileCols;
            while (tileColumnsLog2 < maxLog2TileCols && reader.ReadBoolean())
            {
                tileColumnsLog2++;
            }

            int tileWidthSb = (sbCols + (1 << tileColumnsLog2) - 1) >> tileColumnsLog2;
            for (int startSb = 0; startSb < sbCols; startSb += tileWidthSb)
            {
                columnStartsSb.Add(startSb);
            }

            int minLog2TileRows = Math.Max(minLog2Tiles - tileColumnsLog2, 0);
            tileRowsLog2 = minLog2TileRows;
            while (tileRowsLog2 < maxLog2TileRows && reader.ReadBoolean())
            {
                tileRowsLog2++;
            }

            int tileHeightSb = (sbRows + (1 << tileRowsLog2) - 1) >> tileRowsLog2;
            for (int startSb = 0; startSb < sbRows; startSb += tileHeightSb)
            {
                rowStartsSb.Add(startSb);
            }
        }
        else
        {
            int widestTileSb = 0;
            int startSb = 0;
            int i = 0;
            for (; startSb < sbCols; i++)
            {
                columnStartsSb.Add(startSb);
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
                rowStartsSb.Add(startSb);
                int maxHeight = Math.Min(sbRows - startSb, maxTileAreaSb2);
                int height = (int)reader.ReadNonSymmetric((uint)maxHeight) + 1;
                startSb += height;
            }

            tileRowsLog2 = TileLog2(1, j);
        }

        int contextUpdateTileId = 0;
        int tileSizeBytes = 1;
        if (tileColumnsLog2 > 0 || tileRowsLog2 > 0)
        {
            contextUpdateTileId = (int)reader.ReadLiteral(tileRowsLog2 + tileColumnsLog2);
            tileSizeBytes = (int)reader.ReadLiteral(2) + 1;
        }

        // Convert the superblock boundaries to 4x4 units, appending the frame end.
        int[] columnStarts = new int[columnStartsSb.Count + 1];
        for (int c = 0; c < columnStartsSb.Count; c++)
        {
            columnStarts[c] = Math.Min(columnStartsSb[c] << sbShift, miCols);
        }

        columnStarts[^1] = miCols;

        int[] rowStarts = new int[rowStartsSb.Count + 1];
        for (int r = 0; r < rowStartsSb.Count; r++)
        {
            rowStarts[r] = Math.Min(rowStartsSb[r] << sbShift, miRows);
        }

        rowStarts[^1] = miRows;

        return new TileInfo
        {
            ColumnsLog2 = tileColumnsLog2,
            RowsLog2 = tileRowsLog2,
            SizeBytes = tileSizeBytes,
            ColumnStarts = columnStarts,
            RowStarts = rowStarts,
            ContextUpdateTileId = contextUpdateTileId,
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
        int qmY = 15;
        int qmU = 15;
        int qmV = 15;
        if (usingQMatrix)
        {
            qmY = (int)reader.ReadLiteral(4);
            qmU = (int)reader.ReadLiteral(4);
            qmV = sequenceHeader.SeparateUvDeltaQ ? (int)reader.ReadLiteral(4) : qmU;
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
            QmY = qmY,
            QmU = qmU,
            QmV = qmV,
        };
    }

    private static int ReadDeltaQ(ref Av1BitStreamReader reader)
        => reader.ReadBoolean() ? reader.ReadSignedLiteral(7) : 0;

    private static ObuSegmentationParams ReadSegmentationParams(ref Av1BitStreamReader reader, bool primaryRefNone, ObuSegmentationParams? inherited)
    {
        bool enabled = reader.ReadBoolean();
        if (!enabled)
        {
            return ObuSegmentationParams.Disabled;
        }

        bool updateMap;
        bool temporalUpdate = false;
        bool updateData;
        if (primaryRefNone)
        {
            updateMap = true;
            updateData = true;
        }
        else
        {
            updateMap = reader.ReadBoolean();
            if (updateMap)
            {
                temporalUpdate = reader.ReadBoolean();
            }

            updateData = reader.ReadBoolean();
        }

        if (!updateData)
        {
            // The feature table is inherited from the primary reference.
            ObuSegmentationParams reference = inherited ?? ObuSegmentationParams.Disabled;
            return reference with
            {
                Enabled = true,
                UpdateMap = updateMap,
                TemporalUpdate = temporalUpdate,
                UpdateData = false,
            };
        }

        int[] deltaQ = new int[ObuSegmentationParams.SegmentCount];
        int[] deltaLfYV = new int[ObuSegmentationParams.SegmentCount];
        int[] deltaLfYH = new int[ObuSegmentationParams.SegmentCount];
        int[] deltaLfU = new int[ObuSegmentationParams.SegmentCount];
        int[] deltaLfV = new int[ObuSegmentationParams.SegmentCount];
        int[] refFeature = new int[ObuSegmentationParams.SegmentCount];
        bool[] skipFeature = new bool[ObuSegmentationParams.SegmentCount];
        bool[] globalMvFeature = new bool[ObuSegmentationParams.SegmentCount];
        int lastActive = -1;
        bool preSkip = false;
        for (int i = 0; i < ObuSegmentationParams.SegmentCount; i++)
        {
            if (reader.ReadBoolean())
            {
                deltaQ[i] = reader.ReadSignedLiteral(9);
                lastActive = i;
            }

            if (reader.ReadBoolean())
            {
                deltaLfYV[i] = reader.ReadSignedLiteral(7);
                lastActive = i;
            }

            if (reader.ReadBoolean())
            {
                deltaLfYH[i] = reader.ReadSignedLiteral(7);
                lastActive = i;
            }

            if (reader.ReadBoolean())
            {
                deltaLfU[i] = reader.ReadSignedLiteral(7);
                lastActive = i;
            }

            if (reader.ReadBoolean())
            {
                deltaLfV[i] = reader.ReadSignedLiteral(7);
                lastActive = i;
            }

            if (reader.ReadBoolean())
            {
                refFeature[i] = (int)reader.ReadLiteral(3);
                lastActive = i;
                preSkip = true;
            }
            else
            {
                refFeature[i] = -1;
            }

            skipFeature[i] = reader.ReadBoolean();
            if (skipFeature[i])
            {
                lastActive = i;
                preSkip = true;
            }

            globalMvFeature[i] = reader.ReadBoolean();
            if (globalMvFeature[i])
            {
                lastActive = i;
                preSkip = true;
            }
        }

        return new ObuSegmentationParams
        {
            Enabled = true,
            UpdateMap = updateMap,
            TemporalUpdate = temporalUpdate,
            UpdateData = true,
            PreSkip = preSkip,
            LastActiveSegmentId = lastActive,
            DeltaQ = deltaQ,
            DeltaLfYVertical = deltaLfYV,
            DeltaLfYHorizontal = deltaLfYH,
            DeltaLfU = deltaLfU,
            DeltaLfV = deltaLfV,
            Reference = refFeature,
            Skip = skipFeature,
            GlobalMv = globalMvFeature,
        };
    }

    // global_motion_params() for one reference (dav1d obu.c): the model type as up to three flag bits,
    // then the matrix entries as sub-exponential deltas against the primary reference's saved model.
    // The inner 2x2 codes in Q13 around identity (scaled to Q16 by the *2), the translation in units
    // that depend on the model type and the motion-vector precision.
    private static Av1WarpedMotionParams ReadGlobalMotionParams(ref Av1BitStreamReader reader, Av1WarpedMotionParams reference, bool allowHighPrecisionMv)
    {
        Av1WarpModelType type = !reader.ReadBoolean() ? Av1WarpModelType.Identity :
            reader.ReadBoolean() ? Av1WarpModelType.RotZoom :
            reader.ReadBoolean() ? Av1WarpModelType.Translation : Av1WarpModelType.Affine;

        if (type == Av1WarpModelType.Identity)
        {
            return Av1WarpedMotionParams.Identity;
        }

        int[] matrix = [0, 0, 1 << 16, 0, 0, 1 << 16];
        int[] referenceMatrix = reference.Matrix;
        int bits;
        int shift;
        if (type >= Av1WarpModelType.RotZoom)
        {
            matrix[2] = (1 << 16) + (2 * reader.ReadSubExponential((referenceMatrix[2] - (1 << 16)) >> 1, 12));
            matrix[3] = 2 * reader.ReadSubExponential(referenceMatrix[3] >> 1, 12);
            bits = 12;
            shift = 10;
        }
        else
        {
            bits = allowHighPrecisionMv ? 9 : 8;
            shift = allowHighPrecisionMv ? 13 : 14;
        }

        if (type == Av1WarpModelType.Affine)
        {
            matrix[4] = 2 * reader.ReadSubExponential(referenceMatrix[4] >> 1, 12);
            matrix[5] = (1 << 16) + (2 * reader.ReadSubExponential((referenceMatrix[5] - (1 << 16)) >> 1, 12));
        }
        else
        {
            matrix[4] = -matrix[3];
            matrix[5] = matrix[2];
        }

        matrix[0] = reader.ReadSubExponential(referenceMatrix[0] >> shift, bits) * (1 << shift);
        matrix[1] = reader.ReadSubExponential(referenceMatrix[1] >> shift, bits) * (1 << shift);
        return new Av1WarpedMotionParams(type, matrix);
    }

    private static LoopFilter ReadLoopFilterParams(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, bool codedLossless, bool allowIntraBlockCopy, ObuPrimaryReferenceState? inherited = null)
    {
        // The deltas start from the primary reference's saved state when there is one, otherwise from
        // the spec defaults established by setup_past_independence.
        inherited ??= ObuPrimaryReferenceState.CreateDefault();
        int[] refDeltas = (int[])inherited.LoopFilterRefDeltas.Clone();
        int[] modeDeltas = (int[])inherited.LoopFilterModeDeltas.Clone();

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

    private static LoopRestoration ReadLoopRestorationParams(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, bool codedLossless, bool allowIntraBlockCopy)
    {
        int[] types = [0, 0, 0];
        int[] unitSizeLog2 = [8, 8];
        if (codedLossless || allowIntraBlockCopy || !sequenceHeader.EnableRestoration)
        {
            return new LoopRestoration { Types = types, UnitSizeLog2 = unitSizeLog2 };
        }

        bool usesLr = false;
        bool usesChromaLr = false;
        for (int i = 0; i < sequenceHeader.NumPlanes; i++)
        {
            types[i] = (int)reader.ReadLiteral(2);
            if (types[i] != 0)
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
            int size = 6 + (sequenceHeader.Use128x128Superblock ? 1 : 0);
            if (reader.ReadBoolean())
            {
                size++;
                if (!sequenceHeader.Use128x128Superblock && reader.ReadBoolean())
                {
                    size++;
                }
            }

            unitSizeLog2[0] = size;
            unitSizeLog2[1] = size;
            if (usesChromaLr && sequenceHeader.SubsamplingX == 1 && sequenceHeader.SubsamplingY == 1 && reader.ReadBoolean())
            {
                unitSizeLog2[1] = size - 1;
            }
        }

        return new LoopRestoration { Types = types, UnitSizeLog2 = unitSizeLog2 };
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

        public int[] ColumnStarts { get; init; }

        public int[] RowStarts { get; init; }

        public int ContextUpdateTileId { get; init; }
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

    /// <summary>
    /// The loop-restoration parameters from the frame header (specification section 5.9.20).
    /// </summary>
    public readonly struct LoopRestoration
    {
        /// <summary>Gets the per-plane restoration type (0 = none, 1 = switchable, 2 = Wiener, 3 = SGR).</summary>
        public int[] Types { get; init; }

        /// <summary>Gets the log2 restoration unit size for [luma, chroma].</summary>
        public int[] UnitSizeLog2 { get; init; }
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

        public int QmY { get; init; }

        public int QmU { get; init; }

        public int QmV { get; init; }
    }
}
