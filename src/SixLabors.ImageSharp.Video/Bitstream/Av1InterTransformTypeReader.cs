// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the luma transform type of an inter transform block, a port of the inter branch of the
/// reference decoder's transform-type parse (<c>recon_tmpl.c</c>). The caller is responsible for the
/// cases that do not code a transform type (lossless, a 64x64 maximum, or a zero quantizer, which all
/// imply DCT_DCT); this reader handles the three coded sets selected by the transform's minimum and
/// maximum square categories.
/// </summary>
internal static class Av1InterTransformTypeReader
{
    /// <summary>
    /// Reads the inter transform type for a coded luma transform block.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive inter transform-type CDFs.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="reducedTransformSet">Whether the frame uses the reduced transform set.</param>
    /// <returns>The decoded transform type.</returns>
    public static Av1TransformType Read(
        Av1SymbolDecoder decoder,
        Av1InterTransformTypeCdfContext cdf,
        Av1TransformSize transformSize,
        bool reducedTransformSet)
    {
        int categoryWidth = transformSize.GetWidthLog2() - 2;
        int categoryHeight = transformSize.GetHeightLog2() - 2;
        int minCategory = Math.Min(categoryWidth, categoryHeight);
        int maxCategory = Math.Max(categoryWidth, categoryHeight);

        if (reducedTransformSet || maxCategory == 3)
        {
            int idx = decoder.ReadSymbol(cdf.Set3[minCategory]);
            return (Av1TransformType)((idx - 1) & 9); // idx ? DCT_DCT : IDTX
        }

        if (minCategory == 2)
        {
            return Av1InterTransformTypeCdfContext.FromSet2(decoder.ReadSymbol(cdf.Set2));
        }

        return Av1InterTransformTypeCdfContext.FromSet1(decoder.ReadSymbol(cdf.Set1[minCategory]));
    }
}
