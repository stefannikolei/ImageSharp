// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Dimension and partition helpers for <see cref="Av1BlockSize"/> (dav1d <c>block_dimensions</c>,
/// <c>al_part_ctx</c> and the partition sub-size logic).
/// </summary>
internal static class Av1BlockSizeExtensions
{
    // dav1d_block_dimensions: {w4, h4, w_log2, h_log2} indexed by Av1BlockSize.
    private static readonly byte[] Width4Table =
        [32, 32, 16, 16, 16, 16, 8, 8, 8, 8, 4, 4, 4, 4, 4, 2, 2, 2, 2, 1, 1, 1];

    private static readonly byte[] Height4Table =
        [32, 16, 32, 16, 8, 4, 16, 8, 4, 2, 16, 8, 4, 2, 1, 8, 4, 2, 1, 4, 2, 1];

    private static readonly byte[] Width4Log2Table =
        [5, 5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 2, 2, 1, 1, 1, 1, 0, 0, 0];

    private static readonly byte[] Height4Log2Table =
        [5, 4, 5, 4, 3, 2, 4, 3, 2, 1, 4, 3, 2, 1, 0, 3, 2, 1, 0, 2, 1, 0];

    // dav1d_al_part_ctx[0=above, 1=left][block level][partition]; -1 marks invalid combinations.
    private static readonly short[][][] AlPartitionContext =
    [
        [
            [0x00, 0x00, 0x10, -1, 0x00, 0x10, 0x10, 0x10, -1, -1],
            [0x10, 0x10, 0x18, -1, 0x10, 0x18, 0x18, 0x18, 0x10, 0x1c],
            [0x18, 0x18, 0x1c, -1, 0x18, 0x1c, 0x1c, 0x1c, 0x18, 0x1e],
            [0x1c, 0x1c, 0x1e, -1, 0x1c, 0x1e, 0x1e, 0x1e, 0x1c, 0x1f],
            [0x1e, 0x1e, 0x1f, 0x1f, -1, -1, -1, -1, -1, -1],
        ],
        [
            [0x00, 0x10, 0x00, -1, 0x10, 0x10, 0x00, 0x10, -1, -1],
            [0x10, 0x18, 0x10, -1, 0x18, 0x18, 0x10, 0x18, 0x1c, 0x10],
            [0x18, 0x1c, 0x18, -1, 0x1c, 0x1c, 0x18, 0x1c, 0x1e, 0x18],
            [0x1c, 0x1e, 0x1c, -1, 0x1e, 0x1e, 0x1c, 0x1e, 0x1f, 0x1c],
            [0x1e, 0x1f, 0x1e, 0x1f, -1, -1, -1, -1, -1, -1],
        ],
    ];

    /// <summary>Gets the block width in 4x4 mode-info units.</summary>
    public static int GetWidth4(this Av1BlockSize size) => Width4Table[(int)size];

    /// <summary>Gets the block height in 4x4 mode-info units.</summary>
    public static int GetHeight4(this Av1BlockSize size) => Height4Table[(int)size];

    /// <summary>Gets the base-2 logarithm of the block width in mode-info units.</summary>
    public static int GetWidthLog2(this Av1BlockSize size) => Width4Log2Table[(int)size];

    /// <summary>Gets the base-2 logarithm of the block height in mode-info units.</summary>
    public static int GetHeightLog2(this Av1BlockSize size) => Height4Log2Table[(int)size];

    /// <summary>Gets a value indicating whether the block is square.</summary>
    public static bool IsSquare(this Av1BlockSize size) => Width4Table[(int)size] == Height4Table[(int)size];

    /// <summary>Gets the block level used to index the partition CDFs (128x128 = 0 .. 8x8 = 4).</summary>
    public static int GetPartitionLevel(this Av1BlockSize size) => 5 - GetWidthLog2(size);

    /// <summary>Resolves the block size for the given mode-info dimensions.</summary>
    public static Av1BlockSize FromDimensions(int width4, int height4)
    {
        for (int i = 0; i < Width4Table.Length; i++)
        {
            if (Width4Table[i] == width4 && Height4Table[i] == height4)
            {
                return (Av1BlockSize)i;
            }
        }

        throw new NotSupportedException($"No block size for {width4 * 4}x{height4 * 4}.");
    }

    /// <summary>Gets the square sub-size produced by a 4-way split.</summary>
    public static Av1BlockSize GetSplitSubSize(this Av1BlockSize size)
    {
        int half = GetWidth4(size) >> 1;
        return FromDimensions(half, half);
    }

    /// <summary>Gets the largest (rectangular) transform size for the luma block.</summary>
    public static Av1TransformSize GetMaxTransformSize(this Av1BlockSize size)
        => MapTransformSize(Math.Min(GetWidth4(size) * 4, 64), Math.Min(GetHeight4(size) * 4, 64));

    /// <summary>Gets the largest (rectangular) transform size for the subsampled chroma block.</summary>
    public static Av1TransformSize GetMaxChromaTransformSize(this Av1BlockSize size, in ObuSequenceHeader sequenceHeader)
    {
        int chromaWidth = Math.Clamp((GetWidth4(size) * 4) >> sequenceHeader.SubsamplingX, 4, 32);
        int chromaHeight = Math.Clamp((GetHeight4(size) * 4) >> sequenceHeader.SubsamplingY, 4, 32);
        return MapTransformSize(chromaWidth, chromaHeight);
    }

    /// <summary>Gets the above partition-context fill byte for a decoded block and partition.</summary>
    public static byte AbovePartitionContext(this Av1BlockSize squareSize, Av1Partition partition)
        => (byte)AlPartitionContext[0][GetPartitionLevel(squareSize)][(int)partition];

    /// <summary>Gets the left partition-context fill byte for a decoded block and partition.</summary>
    public static byte LeftPartitionContext(this Av1BlockSize squareSize, Av1Partition partition)
        => (byte)AlPartitionContext[1][GetPartitionLevel(squareSize)][(int)partition];

    private static Av1TransformSize MapTransformSize(int width, int height) => (width, height) switch
    {
        (4, 4) => Av1TransformSize.Size4x4,
        (8, 8) => Av1TransformSize.Size8x8,
        (16, 16) => Av1TransformSize.Size16x16,
        (32, 32) => Av1TransformSize.Size32x32,
        (64, 64) => Av1TransformSize.Size64x64,
        (4, 8) => Av1TransformSize.Size4x8,
        (8, 4) => Av1TransformSize.Size8x4,
        (8, 16) => Av1TransformSize.Size8x16,
        (16, 8) => Av1TransformSize.Size16x8,
        (16, 32) => Av1TransformSize.Size16x32,
        (32, 16) => Av1TransformSize.Size32x16,
        (32, 64) => Av1TransformSize.Size32x64,
        (64, 32) => Av1TransformSize.Size64x32,
        (4, 16) => Av1TransformSize.Size4x16,
        (16, 4) => Av1TransformSize.Size16x4,
        (8, 32) => Av1TransformSize.Size8x32,
        (32, 8) => Av1TransformSize.Size32x8,
        (16, 64) => Av1TransformSize.Size16x64,
        (64, 16) => Av1TransformSize.Size64x16,
        _ => throw new NotSupportedException($"No transform size for {width}x{height}."),
    };
}
