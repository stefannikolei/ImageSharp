// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the variable-transform tree reader (<see cref="Av1TransformTreeReader"/>).
/// A test-only encoder mirrors the reader's recursion, context derivation and neighbour updates exactly,
/// emitting chosen split decisions; the reader must reproduce the same per-depth split masks and the same
/// neighbour transform-size categories.
/// </summary>
public class Av1TransformTreeReaderTests
{
    private const int FrameWidth4 = 32;
    private const int FrameHeight4 = 32;
    private const int Bx4 = 4;
    private const int By4 = 4;

    private static ushort[][][] CloneDefault()
    {
        ushort[][][] source = Av1DefaultTransformPartitionCdf.Split;
        ushort[][][] result = new ushort[source.Length][][];
        for (int i = 0; i < source.Length; i++)
        {
            result[i] = new ushort[source[i].Length][];
            for (int j = 0; j < source[i].Length; j++)
            {
                result[i][j] = (ushort[])source[i][j].Clone();
            }
        }

        return result;
    }

    [Theory]
    [InlineData((int)Av1TransformSize.Size8x8, false)]
    [InlineData((int)Av1TransformSize.Size8x8, true)]
    [InlineData((int)Av1TransformSize.Size16x16, false)]
    [InlineData((int)Av1TransformSize.Size16x16, true)]
    [InlineData((int)Av1TransformSize.Size32x32, true)]
    public void Read_RoundTripsMaskAndNeighbours(int transformSize, bool splitTop)
    {
        Av1TransformSize from = (Av1TransformSize)transformSize;

        // Split decision: split the top node when requested, never split deeper.
        static bool Decide(int depth) => depth == 0;
        bool ShouldSplit(int depth) => splitTop && Decide(depth);

        // Encode.
        Av1SymbolEncoder encoder = new();
        ushort[][][] encoderCdf = CloneDefault();
        ushort[] encoderMasks = new ushort[3];
        sbyte[] encoderAbove = NeighbourArray(FrameWidth4);
        sbyte[] encoderLeft = NeighbourArray(FrameHeight4);
        EncodeTree(encoder, encoderCdf, from, 0, 0, 0, encoderMasks, encoderAbove, encoderLeft, Bx4, By4, ShouldSplit);
        byte[] payload = encoder.Finish();

        // Decode.
        Av1SymbolDecoder decoder = new(payload);
        ushort[][][] decoderCdf = CloneDefault();
        ushort[] decoderMasks = new ushort[3];
        sbyte[] decoderAbove = NeighbourArray(FrameWidth4);
        sbyte[] decoderLeft = NeighbourArray(FrameHeight4);
        Av1TransformTreeReader.Read(decoder, decoderCdf, from, 0, 0, 0, decoderMasks, decoderAbove, decoderLeft, Bx4, By4, FrameWidth4, FrameHeight4);

        Assert.Equal(encoderMasks, decoderMasks);
        Assert.Equal(encoderAbove, decoderAbove);
        Assert.Equal(encoderLeft, decoderLeft);
    }

    private static sbyte[] NeighbourArray(int length)
    {
        sbyte[] array = new sbyte[length];
        Array.Fill(array, (sbyte)-1);
        return array;
    }

    private static void EncodeTree(
        Av1SymbolEncoder encoder,
        ushort[][][] splitCdf,
        Av1TransformSize from,
        int depth,
        int xOffset,
        int yOffset,
        ushort[] masks,
        sbyte[] aboveTx,
        sbyte[] leftTx,
        int bx4,
        int by4,
        Func<int, bool> shouldSplit)
    {
        int categoryWidth = from.GetWidthLog2() - 2;
        int categoryHeight = from.GetHeightLog2() - 2;
        int maxCategory = Math.Max(categoryWidth, categoryHeight);

        bool isSplit;
        if (depth < 2 && from != Av1TransformSize.Size4x4)
        {
            int category = (2 * (4 - maxCategory)) - depth;
            int above = aboveTx[bx4] < categoryWidth ? 1 : 0;
            int left = leftTx[by4] < categoryHeight ? 1 : 0;
            isSplit = shouldSplit(depth);
            encoder.WriteSymbol(isSplit ? 1 : 0, splitCdf[category][above + left]);
            if (isSplit)
            {
                masks[depth] |= (ushort)(1 << ((yOffset * 4) + xOffset));
            }
        }
        else
        {
            isSplit = false;
        }

        if (isSplit && maxCategory > 1)
        {
            Av1TransformSize sub = from.GetSubSize();
            int subWidth4 = sub.GetWidth() >> 2;
            int subHeight4 = sub.GetHeight() >> 2;

            EncodeTree(encoder, splitCdf, sub, depth + 1, (xOffset * 2) + 0, (yOffset * 2) + 0, masks, aboveTx, leftTx, bx4, by4, shouldSplit);
            if (categoryWidth >= categoryHeight && bx4 + subWidth4 < FrameWidth4)
            {
                EncodeTree(encoder, splitCdf, sub, depth + 1, (xOffset * 2) + 1, (yOffset * 2) + 0, masks, aboveTx, leftTx, bx4 + subWidth4, by4, shouldSplit);
            }

            if (categoryHeight >= categoryWidth && by4 + subHeight4 < FrameHeight4)
            {
                EncodeTree(encoder, splitCdf, sub, depth + 1, (xOffset * 2) + 0, (yOffset * 2) + 1, masks, aboveTx, leftTx, bx4, by4 + subHeight4, shouldSplit);
                if (categoryWidth >= categoryHeight && bx4 + subWidth4 < FrameWidth4)
                {
                    EncodeTree(encoder, splitCdf, sub, depth + 1, (xOffset * 2) + 1, (yOffset * 2) + 1, masks, aboveTx, leftTx, bx4 + subWidth4, by4 + subHeight4, shouldSplit);
                }
            }
        }
        else
        {
            sbyte aboveValue = (sbyte)(isSplit ? 0 : categoryWidth);
            sbyte leftValue = (sbyte)(isSplit ? 0 : categoryHeight);
            for (int x = 0; x < (from.GetWidth() >> 2) && bx4 + x < aboveTx.Length; x++)
            {
                aboveTx[bx4 + x] = aboveValue;
            }

            for (int y = 0; y < (from.GetHeight() >> 2) && by4 + y < leftTx.Length; y++)
            {
                leftTx[by4 + y] = leftValue;
            }
        }
    }
}
