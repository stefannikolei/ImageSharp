// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// The film-grain synthesis parameters of a frame (specification section 5.9.30,
/// <c>film_grain_params</c>). Grain is synthesised into displayed output only; reference frames stay
/// grain-free.
/// </summary>
internal sealed class ObuFilmGrainParams
{
    /// <summary>Gets the 16-bit grain seed.</summary>
    public int Seed { get; init; }

    /// <summary>Gets the luma scaling points as (value, scaling) pairs.</summary>
    public byte[][] YPoints { get; init; } = [];

    /// <summary>Gets a value indicating whether chroma scaling is derived from the luma points.</summary>
    public bool ChromaScalingFromLuma { get; init; }

    /// <summary>Gets the chroma scaling points per plane as (value, scaling) pairs.</summary>
    public byte[][][] UvPoints { get; init; } = [[], []];

    /// <summary>Gets the scaling LUT shift (<c>grain_scaling_minus_8 + 8</c>).</summary>
    public int ScalingShift { get; init; }

    /// <summary>Gets the auto-regression lag in [0, 3].</summary>
    public int ArCoeffLag { get; init; }

    /// <summary>Gets the signed luma AR coefficients.</summary>
    public sbyte[] ArCoeffsY { get; init; } = [];

    /// <summary>Gets the signed chroma AR coefficients per plane (the last entry weighs the luma grain).</summary>
    public sbyte[][] ArCoeffsUv { get; init; } = [[], []];

    /// <summary>Gets the AR filter shift (<c>ar_coeff_shift_minus_6 + 6</c>).</summary>
    public int ArCoeffShift { get; init; }

    /// <summary>Gets the extra downscale shift applied to the Gaussian noise.</summary>
    public int GrainScaleShift { get; init; }

    /// <summary>Gets the signed per-plane chroma multiplier (<c>cb_mult / cr_mult - 128</c>).</summary>
    public int[] UvMult { get; init; } = new int[2];

    /// <summary>Gets the signed per-plane luma multiplier (<c>cb_luma_mult / cr_luma_mult - 128</c>).</summary>
    public int[] UvLumaMult { get; init; } = new int[2];

    /// <summary>Gets the signed per-plane chroma offset (<c>cb_offset / cr_offset - 256</c>).</summary>
    public int[] UvOffset { get; init; } = new int[2];

    /// <summary>Gets a value indicating whether grain blocks blend across their overlap rows/columns.</summary>
    public bool OverlapFlag { get; init; }

    /// <summary>Gets a value indicating whether output samples clip to the studio range.</summary>
    public bool ClipToRestrictedRange { get; init; }

    /// <summary>
    /// Parses <c>film_grain_params</c>. Returns <see langword="null"/> when the frame applies no grain.
    /// </summary>
    /// <param name="reader">The bit reader positioned at <c>apply_grain</c>.</param>
    /// <param name="sequenceHeader">The active sequence header.</param>
    /// <param name="frameType">The frame type (only inter frames may inherit reference parameters).</param>
    /// <param name="referenceGrain">The stored grain parameters of the eight reference slots (for
    /// <c>film_grain_params_ref_idx</c>), or <see langword="null"/> for intra parses.</param>
    /// <returns>The parsed parameters, or <see langword="null"/>.</returns>
    public static ObuFilmGrainParams? Parse(ref Av1BitStreamReader reader, in ObuSequenceHeader sequenceHeader, Av1FrameType frameType, ObuFilmGrainParams?[]? referenceGrain)
    {
        if (!reader.ReadBoolean())
        {
            return null; // apply_grain
        }

        int seed = (int)reader.ReadLiteral(16);
        bool update = frameType != Av1FrameType.Inter || reader.ReadBoolean();
        if (!update)
        {
            int refIdx = (int)reader.ReadLiteral(3);
            ObuFilmGrainParams inherited = referenceGrain?[refIdx]
                ?? throw new InvalidDataException($"film_grain_params_ref_idx references slot {refIdx} without stored grain parameters.");
            return new ObuFilmGrainParams
            {
                Seed = seed,
                YPoints = inherited.YPoints,
                ChromaScalingFromLuma = inherited.ChromaScalingFromLuma,
                UvPoints = inherited.UvPoints,
                ScalingShift = inherited.ScalingShift,
                ArCoeffLag = inherited.ArCoeffLag,
                ArCoeffsY = inherited.ArCoeffsY,
                ArCoeffsUv = inherited.ArCoeffsUv,
                ArCoeffShift = inherited.ArCoeffShift,
                GrainScaleShift = inherited.GrainScaleShift,
                UvMult = inherited.UvMult,
                UvLumaMult = inherited.UvLumaMult,
                UvOffset = inherited.UvOffset,
                OverlapFlag = inherited.OverlapFlag,
                ClipToRestrictedRange = inherited.ClipToRestrictedRange,
            };
        }

        int numYPoints = (int)reader.ReadLiteral(4);
        byte[][] yPoints = new byte[numYPoints][];
        for (int i = 0; i < numYPoints; i++)
        {
            yPoints[i] = [(byte)reader.ReadLiteral(8), (byte)reader.ReadLiteral(8)];
        }

        bool chromaScalingFromLuma = !sequenceHeader.MonoChrome && reader.ReadBoolean();

        byte[][][] uvPoints = [[], []];
        if (!(sequenceHeader.MonoChrome || chromaScalingFromLuma ||
              (sequenceHeader.SubsamplingX == 1 && sequenceHeader.SubsamplingY == 1 && numYPoints == 0)))
        {
            for (int pl = 0; pl < 2; pl++)
            {
                int numUvPoints = (int)reader.ReadLiteral(4);
                uvPoints[pl] = new byte[numUvPoints][];
                for (int i = 0; i < numUvPoints; i++)
                {
                    uvPoints[pl][i] = [(byte)reader.ReadLiteral(8), (byte)reader.ReadLiteral(8)];
                }
            }
        }

        int scalingShift = (int)reader.ReadLiteral(2) + 8;
        int arCoeffLag = (int)reader.ReadLiteral(2);
        int numYPos = 2 * arCoeffLag * (arCoeffLag + 1);

        sbyte[] arCoeffsY = [];
        if (numYPoints > 0)
        {
            arCoeffsY = new sbyte[numYPos];
            for (int i = 0; i < numYPos; i++)
            {
                arCoeffsY[i] = (sbyte)((int)reader.ReadLiteral(8) - 128);
            }
        }

        sbyte[][] arCoeffsUv = [[], []];
        for (int pl = 0; pl < 2; pl++)
        {
            if (uvPoints[pl].Length > 0 || chromaScalingFromLuma)
            {
                int numUvPos = numYPos + (numYPoints > 0 ? 1 : 0);
                arCoeffsUv[pl] = new sbyte[numUvPos + (numYPoints > 0 ? 0 : 1)];
                for (int i = 0; i < numUvPos; i++)
                {
                    arCoeffsUv[pl][i] = (sbyte)((int)reader.ReadLiteral(8) - 128);
                }
            }
        }

        int arCoeffShift = (int)reader.ReadLiteral(2) + 6;
        int grainScaleShift = (int)reader.ReadLiteral(2);

        int[] uvMult = new int[2];
        int[] uvLumaMult = new int[2];
        int[] uvOffset = new int[2];
        for (int pl = 0; pl < 2; pl++)
        {
            if (uvPoints[pl].Length > 0)
            {
                uvMult[pl] = (int)reader.ReadLiteral(8) - 128;
                uvLumaMult[pl] = (int)reader.ReadLiteral(8) - 128;
                uvOffset[pl] = (int)reader.ReadLiteral(9) - 256;
            }
        }

        return new ObuFilmGrainParams
        {
            Seed = seed,
            YPoints = yPoints,
            ChromaScalingFromLuma = chromaScalingFromLuma,
            UvPoints = uvPoints,
            ScalingShift = scalingShift,
            ArCoeffLag = arCoeffLag,
            ArCoeffsY = arCoeffsY,
            ArCoeffsUv = arCoeffsUv,
            ArCoeffShift = arCoeffShift,
            GrainScaleShift = grainScaleShift,
            UvMult = uvMult,
            UvLumaMult = uvLumaMult,
            UvOffset = uvOffset,
            OverlapFlag = reader.ReadBoolean(),
            ClipToRestrictedRange = reader.ReadBoolean(),
        };
    }
}
