// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Derives the chroma transform type, which is never coded in the bitstream. For an intra block it comes
/// from a fixed lookup on the chroma prediction mode (dav1d's <c>dav1d_txtp_from_uvmode</c>); for an inter
/// block it is inferred from the co-located luma transform type (dav1d's <c>get_uv_inter_txtp</c>).
/// </summary>
internal static class Av1ChromaTransformType
{
    // dav1d_txtp_from_uvmode: the chroma transform type implied by each of the 13 chroma intra modes.
    private static readonly Av1TransformType[] FromUvModeTable =
    [
        Av1TransformType.DctDct,    // DC
        Av1TransformType.AdstDct,   // VERT
        Av1TransformType.DctAdst,   // HOR
        Av1TransformType.DctDct,    // DIAG_DOWN_LEFT
        Av1TransformType.AdstAdst,  // DIAG_DOWN_RIGHT
        Av1TransformType.AdstDct,   // VERT_RIGHT
        Av1TransformType.DctAdst,   // HOR_DOWN
        Av1TransformType.DctAdst,   // HOR_UP
        Av1TransformType.AdstDct,   // VERT_LEFT
        Av1TransformType.AdstAdst,  // SMOOTH
        Av1TransformType.AdstDct,   // SMOOTH_V
        Av1TransformType.DctAdst,   // SMOOTH_H
        Av1TransformType.AdstAdst,  // PAETH
    ];

    /// <summary>The square transform-size category of a 64x64 transform (dav1d <c>TX_64X64</c>).</summary>
    private const int Category64x64 = 4;

    /// <summary>The square transform-size category of a 32x32 transform (dav1d <c>TX_32X32</c>).</summary>
    private const int Category32x32 = 3;

    /// <summary>The square transform-size category of a 16x16 transform (dav1d <c>TX_16X16</c>).</summary>
    private const int Category16x16 = 2;

    /// <summary>Derives the chroma transform type for an intra block from its chroma prediction mode.</summary>
    public static Av1TransformType FromIntra(Av1TransformSize chromaTransformSize, int uvMode)
    {
        // dav1d: a chroma transform whose largest dimension is 32x32 (so max + intra >= TX_64X64) is DCT.
        if (MaxCategory(chromaTransformSize) + 1 >= Category64x64)
        {
            return Av1TransformType.DctDct;
        }

        // CFL (mode 13) is not in the lookup; dav1d's designated-initialiser table leaves it DCT_DCT.
        return uvMode < FromUvModeTable.Length ? FromUvModeTable[uvMode] : Av1TransformType.DctDct;
    }

    /// <summary>Derives the chroma transform type for an inter block from the co-located luma type.</summary>
    public static Av1TransformType FromInter(Av1TransformSize chromaTransformSize, Av1TransformType lumaType)
    {
        if (MaxCategory(chromaTransformSize) == Category32x32)
        {
            return lumaType == Av1TransformType.Identity ? Av1TransformType.Identity : Av1TransformType.DctDct;
        }

        if (MinCategory(chromaTransformSize) == Category16x16 &&
            lumaType is Av1TransformType.HorizontalFlipAdst or Av1TransformType.VerticalFlipAdst
                or Av1TransformType.HorizontalAdst or Av1TransformType.VerticalAdst)
        {
            return Av1TransformType.DctDct;
        }

        return lumaType;
    }

    private static int MaxCategory(Av1TransformSize tx) => Math.Max(tx.GetWidthLog2() - 2, tx.GetHeightLog2() - 2);

    private static int MinCategory(Av1TransformSize tx) => Math.Min(tx.GetWidthLog2() - 2, tx.GetHeightLog2() - 2);
}
