// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the motion-vector residual (the difference from a predictor) using the adaptive MV CDFs,
/// a port of the reference decoder's <c>read_mv_residual</c> / <c>read_mv_component_diff</c>
/// (specification section 5.11.32, <c>read_mv</c>).
/// </summary>
internal static class Av1MotionVectorReader
{
    // The two motion-vector joint flags. A joint symbol is the OR of these: bit 0 selects a non-zero
    // column (horizontal) component, bit 1 selects a non-zero row (vertical) component.
    private const int JointHorizontal = 1;
    private const int JointVertical = 2;

    /// <summary>
    /// Reads a motion-vector residual and adds it to the supplied predictor.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive motion-vector CDFs.</param>
    /// <param name="predictor">The motion-vector predictor to add the residual to.</param>
    /// <param name="precision">
    /// The motion-vector precision: negative for force-integer, zero for integer-subpel and positive
    /// when high-precision (eighth-pel) motion vectors are allowed.
    /// </param>
    /// <returns>The decoded motion vector.</returns>
    public static Av1MotionVector ReadResidual(
        Av1SymbolDecoder decoder,
        Av1MotionVectorCdfContext cdf,
        Av1MotionVector predictor,
        int precision)
    {
        int y = predictor.Y;
        int x = predictor.X;
        int joint = decoder.ReadSymbol(cdf.Joint);
        if ((joint & JointVertical) != 0)
        {
            y += ReadComponentDiff(decoder, cdf.Components[0], precision);
        }

        if ((joint & JointHorizontal) != 0)
        {
            x += ReadComponentDiff(decoder, cdf.Components[1], precision);
        }

        return new Av1MotionVector(y, x);
    }

    private static int ReadComponentDiff(
        Av1SymbolDecoder decoder,
        Av1MotionVectorCdfContext.Component component,
        int precision)
    {
        int sign = decoder.ReadSymbol(component.Sign);
        int classIndex = decoder.ReadSymbol(component.Classes);
        int up;
        int fp = 3;
        int hp = 1;

        if (classIndex == 0)
        {
            up = decoder.ReadSymbol(component.Class0);
            if (precision >= 0)
            {
                fp = decoder.ReadSymbol(component.Class0Fp[up]);
                if (precision > 0)
                {
                    hp = decoder.ReadSymbol(component.Class0Hp);
                }
            }
        }
        else
        {
            up = 1 << classIndex;
            for (int n = 0; n < classIndex; n++)
            {
                up |= decoder.ReadSymbol(component.ClassN[n]) << n;
            }

            if (precision >= 0)
            {
                fp = decoder.ReadSymbol(component.ClassNFp);
                if (precision > 0)
                {
                    hp = decoder.ReadSymbol(component.ClassNHp);
                }
            }
        }

        int diff = ((up << 3) | (fp << 1) | hp) + 1;
        return sign != 0 ? -diff : diff;
    }
}
