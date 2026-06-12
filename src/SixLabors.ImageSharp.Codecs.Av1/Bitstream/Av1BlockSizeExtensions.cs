// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Dimension and partition helpers for <see cref="Av1BlockSize"/>.
/// </summary>
internal static class Av1BlockSizeExtensions
{
    // Partition-context fill bytes for PARTITION_NONE per block level (dav1d_al_part_ctx, NONE column).
    private static readonly byte[] NonePartitionContext = [0x00, 0x10, 0x18, 0x1c, 0x1e];

    /// <summary>Gets the block width in 4x4 mode-info units.</summary>
    public static int GetWidth4(this Av1BlockSize size) => 1 << (int)size;

    /// <summary>Gets the block height in 4x4 mode-info units.</summary>
    public static int GetHeight4(this Av1BlockSize size) => 1 << (int)size;

    /// <summary>Gets the base-2 logarithm of the block width in mode-info units.</summary>
    public static int GetWidthLog2(this Av1BlockSize size) => (int)size;

    /// <summary>Gets the block level used to index the partition CDFs (128x128 = 0 .. 8x8 = 4).</summary>
    public static int GetPartitionLevel(this Av1BlockSize size) => 5 - (int)size;

    /// <summary>Gets the size resulting from applying a partition (only NONE and SPLIT are supported).</summary>
    public static Av1BlockSize GetSubSize(this Av1BlockSize size, Av1Partition partition) => partition switch
    {
        Av1Partition.None => size,
        Av1Partition.Split => (Av1BlockSize)((int)size - 1),
        _ => throw new NotSupportedException($"Partition {partition} is not supported."),
    };

    /// <summary>Gets the largest transform size for the (square) luma block.</summary>
    public static Av1TransformSize GetMaxTransformSize(this Av1BlockSize size) => size switch
    {
        Av1BlockSize.Block4x4 => Av1TransformSize.Size4x4,
        Av1BlockSize.Block8x8 => Av1TransformSize.Size8x8,
        Av1BlockSize.Block16x16 => Av1TransformSize.Size16x16,
        Av1BlockSize.Block32x32 => Av1TransformSize.Size32x32,
        _ => Av1TransformSize.Size64x64,
    };

    /// <summary>Gets the largest transform size for the subsampled chroma block.</summary>
    public static Av1TransformSize GetMaxChromaTransformSize(this Av1BlockSize size, in ObuSequenceHeader sequenceHeader)
    {
        int chromaWidth = (size.GetWidth4() * 4) >> sequenceHeader.SubsamplingX;
        int chromaHeight = (size.GetHeight4() * 4) >> sequenceHeader.SubsamplingY;
        int dimension = Math.Min(Math.Min(chromaWidth, chromaHeight), 32);
        return dimension switch
        {
            <= 4 => Av1TransformSize.Size4x4,
            8 => Av1TransformSize.Size8x8,
            16 => Av1TransformSize.Size16x16,
            _ => Av1TransformSize.Size32x32,
        };
    }

    /// <summary>Gets the partition-context fill byte for a decoded (PARTITION_NONE leaf) block.</summary>
    public static byte PartitionContextFill(this Av1BlockSize size) => NonePartitionContext[size.GetPartitionLevel()];
}
