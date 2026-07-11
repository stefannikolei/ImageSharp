// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// Represents the AV1 sequence header (specification section 5.5.1, <c>sequence_header_obu</c>),
/// including the coding-tool enable flags and colour configuration required by the frame-header and
/// block-decoding pipeline.
/// </summary>
internal readonly struct ObuSequenceHeader
{
    /// <summary>The <c>SELECT_SCREEN_CONTENT_TOOLS</c> / <c>SELECT_INTEGER_MV</c> sentinel.</summary>
    public const int Select = 2;

    /// <summary>Gets the sequence profile (<c>seq_profile</c>), in the range [0, 2].</summary>
    public int SeqProfile { get; init; }

    /// <summary>Gets a value indicating whether the bitstream contains a single coded picture.</summary>
    public bool StillPicture { get; init; }

    /// <summary>Gets a value indicating whether the reduced still-picture header syntax was used.</summary>
    public bool ReducedStillPictureHeader { get; init; }

    /// <summary>Gets the number of bits used to encode <c>frame_width_minus_1</c>.</summary>
    public int FrameWidthBits { get; init; }

    /// <summary>Gets the number of bits used to encode <c>frame_height_minus_1</c>.</summary>
    public int FrameHeightBits { get; init; }

    /// <summary>Gets the maximum coded frame width in pixels.</summary>
    public int MaxFrameWidth { get; init; }

    /// <summary>Gets the maximum coded frame height in pixels.</summary>
    public int MaxFrameHeight { get; init; }

    /// <summary>Gets a value indicating whether frame id numbers are present.</summary>
    public bool FrameIdNumbersPresent { get; init; }

    /// <summary>Gets the additional bits used for delta frame ids (<c>delta_frame_id_length_minus_2 + 2</c>).</summary>
    public int DeltaFrameIdLength { get; init; }

    /// <summary>Gets the total bits used for frame ids (<c>additional_frame_id_length_minus_1 + delta + 1</c>).</summary>
    public int FrameIdLength { get; init; }

    /// <summary>Gets a value indicating whether the superblock size is 128x128 (otherwise 64x64).</summary>
    public bool Use128x128Superblock { get; init; }

    /// <summary>Gets a value indicating whether intra filtering is enabled.</summary>
    public bool EnableFilterIntra { get; init; }

    /// <summary>Gets a value indicating whether the intra edge filter is enabled.</summary>
    public bool EnableIntraEdgeFilter { get; init; }

    /// <summary>Gets a value indicating whether inter-intra compound prediction is enabled.</summary>
    public bool EnableInterIntraCompound { get; init; }

    /// <summary>Gets a value indicating whether masked compound prediction is enabled.</summary>
    public bool EnableMaskedCompound { get; init; }

    /// <summary>Gets a value indicating whether warped motion is enabled.</summary>
    public bool EnableWarpedMotion { get; init; }

    /// <summary>Gets a value indicating whether the dual interpolation filter is enabled.</summary>
    public bool EnableDualFilter { get; init; }

    /// <summary>Gets a value indicating whether order hints are enabled.</summary>
    public bool EnableOrderHint { get; init; }

    /// <summary>Gets a value indicating whether jnt (distance-weighted) compound is enabled.</summary>
    public bool EnableJntComp { get; init; }

    /// <summary>Gets a value indicating whether reference frame motion vectors are enabled.</summary>
    public bool EnableReferenceFrameMotionVectors { get; init; }

    /// <summary>Gets the forced screen-content-tools mode (0, 1 or <see cref="Select"/>).</summary>
    public int ForceScreenContentTools { get; init; }

    /// <summary>Gets the forced integer-mv mode (0, 1 or <see cref="Select"/>).</summary>
    public int ForceIntegerMotionVector { get; init; }

    /// <summary>Gets the number of bits used to encode order hints.</summary>
    public int OrderHintBits { get; init; }

    /// <summary>Gets a value indicating whether super-resolution is enabled.</summary>
    public bool EnableSuperResolution { get; init; }

    /// <summary>Gets a value indicating whether CDEF is enabled.</summary>
    public bool EnableCdef { get; init; }

    /// <summary>Gets a value indicating whether loop restoration is enabled.</summary>
    public bool EnableRestoration { get; init; }

    /// <summary>Gets the bit depth (8, 10 or 12).</summary>
    public int BitDepth { get; init; }

    /// <summary>Gets a value indicating whether the content is monochrome (luma only).</summary>
    public bool MonoChrome { get; init; }

    /// <summary>Gets the number of planes (1 if monochrome, otherwise 3).</summary>
    public int NumPlanes => this.MonoChrome ? 1 : 3;

    /// <summary>Gets the horizontal chroma subsampling (1 = subsampled).</summary>
    public int SubsamplingX { get; init; }

    /// <summary>Gets the vertical chroma subsampling (1 = subsampled).</summary>
    public int SubsamplingY { get; init; }

    /// <summary>Gets a value indicating whether luma and chroma use separate delta-q.</summary>
    public bool SeparateUvDeltaQ { get; init; }

    /// <summary>Gets a value indicating whether the colour range is full (otherwise studio).</summary>
    public bool ColorRange { get; init; }

    /// <summary>Gets a value indicating whether film grain parameters may be present.</summary>
    public bool FilmGrainParamsPresent { get; init; }

    /// <summary>
    /// Parses a sequence header OBU payload (specification section 5.5.1).
    /// </summary>
    /// <param name="payload">The OBU payload (the bytes following the OBU header and size).</param>
    /// <returns>The parsed <see cref="ObuSequenceHeader"/>.</returns>
    public static ObuSequenceHeader Parse(ReadOnlySpan<byte> payload)
    {
        Av1BitStreamReader reader = new(payload);

        int seqProfile = (int)reader.ReadLiteral(3);
        bool stillPicture = reader.ReadBoolean();
        bool reducedStillPictureHeader = reader.ReadBoolean();

        bool decoderModelInfoPresent = false;
        int bufferDelayLengthMinus1 = 0;

        if (reducedStillPictureHeader)
        {
            // seq_level_idx[0]
            reader.ReadLiteral(5);
        }
        else
        {
            bool timingInfoPresent = reader.ReadBoolean();
            if (timingInfoPresent)
            {
                ReadTimingInfo(ref reader);

                decoderModelInfoPresent = reader.ReadBoolean();
                if (decoderModelInfoPresent)
                {
                    bufferDelayLengthMinus1 = (int)reader.ReadLiteral(5);
                    reader.ReadLiteral(32); // num_units_in_decoding_tick
                    reader.ReadLiteral(5);  // buffer_removal_time_length_minus_1
                    reader.ReadLiteral(5);  // frame_presentation_time_length_minus_1
                }
            }

            bool initialDisplayDelayPresent = reader.ReadBoolean();
            int operatingPointsCountMinus1 = (int)reader.ReadLiteral(5);

            for (int i = 0; i <= operatingPointsCountMinus1; i++)
            {
                reader.ReadLiteral(12); // operating_point_idc[i]
                int seqLevelIdx = (int)reader.ReadLiteral(5);
                if (seqLevelIdx > 7)
                {
                    reader.ReadLiteral(1); // seq_tier[i]
                }

                if (decoderModelInfoPresent)
                {
                    bool decoderModelPresentForThisOp = reader.ReadBoolean();
                    if (decoderModelPresentForThisOp)
                    {
                        int n = bufferDelayLengthMinus1 + 1;
                        reader.ReadLiteral(n); // decoder_buffer_delay[op]
                        reader.ReadLiteral(n); // encoder_buffer_delay[op]
                        reader.ReadLiteral(1); // low_delay_mode_flag[op]
                    }
                }

                if (initialDisplayDelayPresent)
                {
                    bool initialDisplayDelayPresentForThisOp = reader.ReadBoolean();
                    if (initialDisplayDelayPresentForThisOp)
                    {
                        reader.ReadLiteral(4); // initial_display_delay_minus_1[i]
                    }
                }
            }
        }

        int frameWidthBitsMinus1 = (int)reader.ReadLiteral(4);
        int frameHeightBitsMinus1 = (int)reader.ReadLiteral(4);
        int maxFrameWidthMinus1 = (int)reader.ReadLiteral(frameWidthBitsMinus1 + 1);
        int maxFrameHeightMinus1 = (int)reader.ReadLiteral(frameHeightBitsMinus1 + 1);

        bool frameIdNumbersPresent = false;
        int deltaFrameIdLength = 0;
        int frameIdLength = 0;
        if (!reducedStillPictureHeader)
        {
            frameIdNumbersPresent = reader.ReadBoolean();
        }

        if (frameIdNumbersPresent)
        {
            deltaFrameIdLength = (int)reader.ReadLiteral(4) + 2;
            frameIdLength = (int)reader.ReadLiteral(3) + deltaFrameIdLength + 1;
        }

        bool use128x128Superblock = reader.ReadBoolean();
        bool enableFilterIntra = reader.ReadBoolean();
        bool enableIntraEdgeFilter = reader.ReadBoolean();

        bool enableInterIntraCompound = false;
        bool enableMaskedCompound = false;
        bool enableWarpedMotion = false;
        bool enableDualFilter = false;
        bool enableOrderHint = false;
        bool enableJntComp = false;
        bool enableReferenceFrameMotionVectors = false;
        int forceScreenContentTools = Select;
        int forceIntegerMotionVector = Select;
        int orderHintBits = 0;

        if (!reducedStillPictureHeader)
        {
            enableInterIntraCompound = reader.ReadBoolean();
            enableMaskedCompound = reader.ReadBoolean();
            enableWarpedMotion = reader.ReadBoolean();
            enableDualFilter = reader.ReadBoolean();
            enableOrderHint = reader.ReadBoolean();
            if (enableOrderHint)
            {
                enableJntComp = reader.ReadBoolean();
                enableReferenceFrameMotionVectors = reader.ReadBoolean();
            }

            forceScreenContentTools = reader.ReadBoolean() ? Select : (int)reader.ReadLiteral(1);
            if (forceScreenContentTools > 0)
            {
                forceIntegerMotionVector = reader.ReadBoolean() ? Select : (int)reader.ReadLiteral(1);
            }
            else
            {
                forceIntegerMotionVector = Select;
            }

            if (enableOrderHint)
            {
                orderHintBits = (int)reader.ReadLiteral(3) + 1;
            }
        }

        bool enableSuperResolution = reader.ReadBoolean();
        bool enableCdef = reader.ReadBoolean();
        bool enableRestoration = reader.ReadBoolean();

        ColorConfig color = ReadColorConfig(ref reader, seqProfile);

        bool filmGrainParamsPresent = reader.ReadBoolean();

        return new ObuSequenceHeader
        {
            SeqProfile = seqProfile,
            StillPicture = stillPicture,
            ReducedStillPictureHeader = reducedStillPictureHeader,
            FrameWidthBits = frameWidthBitsMinus1 + 1,
            FrameHeightBits = frameHeightBitsMinus1 + 1,
            MaxFrameWidth = maxFrameWidthMinus1 + 1,
            MaxFrameHeight = maxFrameHeightMinus1 + 1,
            FrameIdNumbersPresent = frameIdNumbersPresent,
            DeltaFrameIdLength = deltaFrameIdLength,
            FrameIdLength = frameIdLength,
            Use128x128Superblock = use128x128Superblock,
            EnableFilterIntra = enableFilterIntra,
            EnableIntraEdgeFilter = enableIntraEdgeFilter,
            EnableInterIntraCompound = enableInterIntraCompound,
            EnableMaskedCompound = enableMaskedCompound,
            EnableWarpedMotion = enableWarpedMotion,
            EnableDualFilter = enableDualFilter,
            EnableOrderHint = enableOrderHint,
            EnableJntComp = enableJntComp,
            EnableReferenceFrameMotionVectors = enableReferenceFrameMotionVectors,
            ForceScreenContentTools = forceScreenContentTools,
            ForceIntegerMotionVector = forceIntegerMotionVector,
            OrderHintBits = orderHintBits,
            EnableSuperResolution = enableSuperResolution,
            EnableCdef = enableCdef,
            EnableRestoration = enableRestoration,
            BitDepth = color.BitDepth,
            MonoChrome = color.MonoChrome,
            SubsamplingX = color.SubsamplingX,
            SubsamplingY = color.SubsamplingY,
            ColorRange = color.ColorRange,
            SeparateUvDeltaQ = color.SeparateUvDeltaQ,
            FilmGrainParamsPresent = filmGrainParamsPresent,
        };
    }

    /// <summary>
    /// Reads <c>color_config()</c> (specification section 5.5.2).
    /// </summary>
    private static ColorConfig ReadColorConfig(ref Av1BitStreamReader reader, int seqProfile)
    {
        const int colorPrimariesBt709 = 1;
        const int transferCharacteristicsSrgb = 13;
        const int matrixCoefficientsIdentity = 0;
        const int colorPrimariesUnspecified = 2;
        const int transferCharacteristicsUnspecified = 2;
        const int matrixCoefficientsUnspecified = 2;

        bool highBitdepth = reader.ReadBoolean();
        int bitDepth;
        if (seqProfile == 2 && highBitdepth)
        {
            bitDepth = reader.ReadBoolean() ? 12 : 10;
        }
        else
        {
            bitDepth = highBitdepth ? 10 : 8;
        }

        bool monoChrome = seqProfile != 1 && reader.ReadBoolean();

        bool colorDescriptionPresent = reader.ReadBoolean();
        int colorPrimaries = colorPrimariesUnspecified;
        int transferCharacteristics = transferCharacteristicsUnspecified;
        int matrixCoefficients = matrixCoefficientsUnspecified;
        if (colorDescriptionPresent)
        {
            colorPrimaries = (int)reader.ReadLiteral(8);
            transferCharacteristics = (int)reader.ReadLiteral(8);
            matrixCoefficients = (int)reader.ReadLiteral(8);
        }

        int subsamplingX;
        int subsamplingY;
        bool colorRange;
        bool separateUvDeltaQ = false;

        if (monoChrome)
        {
            colorRange = reader.ReadBoolean();
            subsamplingX = 1;
            subsamplingY = 1;
        }
        else if (colorPrimaries == colorPrimariesBt709 &&
                 transferCharacteristics == transferCharacteristicsSrgb &&
                 matrixCoefficients == matrixCoefficientsIdentity)
        {
            colorRange = true;
            subsamplingX = 0;
            subsamplingY = 0;
            separateUvDeltaQ = reader.ReadBoolean();
        }
        else
        {
            colorRange = reader.ReadBoolean();
            if (seqProfile == 0)
            {
                subsamplingX = 1;
                subsamplingY = 1;
            }
            else if (seqProfile == 1)
            {
                subsamplingX = 0;
                subsamplingY = 0;
            }
            else if (bitDepth == 12)
            {
                subsamplingX = (int)reader.ReadLiteral(1);
                subsamplingY = subsamplingX == 1 ? (int)reader.ReadLiteral(1) : 0;
            }
            else
            {
                subsamplingX = 1;
                subsamplingY = 0;
            }

            if (subsamplingX == 1 && subsamplingY == 1)
            {
                reader.ReadLiteral(2); // chroma_sample_position
            }

            separateUvDeltaQ = reader.ReadBoolean();
        }

        return new ColorConfig
        {
            BitDepth = bitDepth,
            MonoChrome = monoChrome,
            SubsamplingX = subsamplingX,
            SubsamplingY = subsamplingY,
            ColorRange = colorRange,
            SeparateUvDeltaQ = separateUvDeltaQ,
        };
    }

    /// <summary>
    /// Reads <c>timing_info()</c> (specification section 5.5.3).
    /// </summary>
    private static void ReadTimingInfo(ref Av1BitStreamReader reader)
    {
        reader.ReadLiteral(32); // num_units_in_display_tick
        reader.ReadLiteral(32); // time_scale

        bool equalPictureInterval = reader.ReadBoolean();
        if (equalPictureInterval)
        {
            reader.ReadUnsignedVariableLength(); // num_ticks_per_picture_minus_1
        }
    }

    private readonly struct ColorConfig
    {
        public int BitDepth { get; init; }

        public bool MonoChrome { get; init; }

        public int SubsamplingX { get; init; }

        public int SubsamplingY { get; init; }

        public bool ColorRange { get; init; }

        public bool SeparateUvDeltaQ { get; init; }
    }
}
