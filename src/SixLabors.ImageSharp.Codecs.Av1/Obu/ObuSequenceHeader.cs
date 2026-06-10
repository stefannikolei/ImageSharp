// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// Represents the subset of the AV1 sequence header (specification section 5.5.1,
/// <c>sequence_header_obu</c>) required to determine the coded frame dimensions.
/// </summary>
/// <remarks>
/// Parsing currently covers everything up to and including <c>max_frame_width_minus_1</c> and
/// <c>max_frame_height_minus_1</c>. Fields that follow (colour configuration, bit depth, the many
/// coding-tool enable flags) are intentionally not parsed yet; they will be added alongside the
/// frame decoding pipeline. Bit depth therefore defaults to 8.
/// </remarks>
internal readonly struct ObuSequenceHeader
{
    private ObuSequenceHeader(int seqProfile, bool stillPicture, bool reducedStillPictureHeader, int maxFrameWidth, int maxFrameHeight)
    {
        this.SeqProfile = seqProfile;
        this.StillPicture = stillPicture;
        this.ReducedStillPictureHeader = reducedStillPictureHeader;
        this.MaxFrameWidth = maxFrameWidth;
        this.MaxFrameHeight = maxFrameHeight;
    }

    /// <summary>
    /// Gets the sequence profile (<c>seq_profile</c>), in the range [0, 2].
    /// </summary>
    public int SeqProfile { get; }

    /// <summary>
    /// Gets a value indicating whether the bitstream contains a single coded picture.
    /// </summary>
    public bool StillPicture { get; }

    /// <summary>
    /// Gets a value indicating whether the reduced still-picture header syntax was used.
    /// </summary>
    public bool ReducedStillPictureHeader { get; }

    /// <summary>
    /// Gets the maximum coded frame width in pixels.
    /// </summary>
    public int MaxFrameWidth { get; }

    /// <summary>
    /// Gets the maximum coded frame height in pixels.
    /// </summary>
    public int MaxFrameHeight { get; }

    /// <summary>
    /// Parses a sequence header OBU payload.
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

                    // num_units_in_decoding_tick f(32), buffer_removal_time_length_minus_1 f(5),
                    // frame_presentation_time_length_minus_1 f(5).
                    reader.ReadLiteral(32);
                    reader.ReadLiteral(5);
                    reader.ReadLiteral(5);
                }
            }

            bool initialDisplayDelayPresent = reader.ReadBoolean();
            int operatingPointsCountMinus1 = (int)reader.ReadLiteral(5);

            for (int i = 0; i <= operatingPointsCountMinus1; i++)
            {
                // operating_point_idc[i] f(12)
                reader.ReadLiteral(12);
                int seqLevelIdx = (int)reader.ReadLiteral(5);
                if (seqLevelIdx > 7)
                {
                    // seq_tier[i] f(1)
                    reader.ReadLiteral(1);
                }

                if (decoderModelInfoPresent)
                {
                    bool decoderModelPresentForThisOp = reader.ReadBoolean();
                    if (decoderModelPresentForThisOp)
                    {
                        // operating_parameters_info(i)
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
                        // initial_display_delay_minus_1[i] f(4)
                        reader.ReadLiteral(4);
                    }
                }
            }
        }

        int frameWidthBitsMinus1 = (int)reader.ReadLiteral(4);
        int frameHeightBitsMinus1 = (int)reader.ReadLiteral(4);
        int maxFrameWidthMinus1 = (int)reader.ReadLiteral(frameWidthBitsMinus1 + 1);
        int maxFrameHeightMinus1 = (int)reader.ReadLiteral(frameHeightBitsMinus1 + 1);

        return new ObuSequenceHeader(
            seqProfile,
            stillPicture,
            reducedStillPictureHeader,
            maxFrameWidthMinus1 + 1,
            maxFrameHeightMinus1 + 1);
    }

    /// <summary>
    /// Reads <c>timing_info()</c> (specification section 5.5.3).
    /// </summary>
    private static void ReadTimingInfo(ref Av1BitStreamReader reader)
    {
        // num_units_in_display_tick f(32), time_scale f(32).
        reader.ReadLiteral(32);
        reader.ReadLiteral(32);

        bool equalPictureInterval = reader.ReadBoolean();
        if (equalPictureInterval)
        {
            // num_ticks_per_picture_minus_1 uvlc().
            reader.ReadUnsignedVariableLength();
        }
    }
}
