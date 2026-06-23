// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Round-trip validation of the inter transform-type decoder (<see cref="Av1InterTransformTypeReader"/>):
/// the three coded sets selected by the transform's minimum and maximum square categories, each decoded
/// through the adaptive CDFs and mapped to the transform type.
/// </summary>
public class Av1InterTransformTypeReaderTests
{
    [Theory]
    // 8x8 (min=max=1): 16-symbol Set1, a few representative indices.
    [InlineData((int)Av1TransformSize.Size8x8, false, 0, (int)Av1TransformType.Identity)]
    [InlineData((int)Av1TransformSize.Size8x8, false, 7, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size8x8, false, 15, (int)Av1TransformType.FlipAdstAdst)]
    // 16x16 (min=2): 12-symbol Set2.
    [InlineData((int)Av1TransformSize.Size16x16, false, 0, (int)Av1TransformType.Identity)]
    [InlineData((int)Av1TransformSize.Size16x16, false, 3, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size16x16, false, 11, (int)Av1TransformType.FlipAdstAdst)]
    // 32x32 (max=3): binary Set3.
    [InlineData((int)Av1TransformSize.Size32x32, false, 1, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size32x32, false, 0, (int)Av1TransformType.Identity)]
    // Reduced set forces the binary Set3 even for a small transform.
    [InlineData((int)Av1TransformSize.Size8x8, true, 1, (int)Av1TransformType.DctDct)]
    [InlineData((int)Av1TransformSize.Size8x8, true, 0, (int)Av1TransformType.Identity)]
    public void Read_RoundTripsThroughEncoder(int transformSize, bool reduced, int index, int expectedType)
    {
        Av1TransformSize tx = (Av1TransformSize)transformSize;

        Av1SymbolEncoder encoder = new();
        Av1InterTransformTypeCdfContext encoderCdf = Av1InterTransformTypeCdfContext.CreateDefault();
        WriteSymbol(encoder, encoderCdf, tx, reduced, index);
        byte[] payload = encoder.Finish();

        Av1SymbolDecoder decoder = new(payload);
        Av1InterTransformTypeCdfContext decoderCdf = Av1InterTransformTypeCdfContext.CreateDefault();
        Av1TransformType actual = Av1InterTransformTypeReader.Read(decoder, decoderCdf, tx, reduced);

        Assert.Equal((Av1TransformType)expectedType, actual);
    }

    private static void WriteSymbol(Av1SymbolEncoder encoder, Av1InterTransformTypeCdfContext cdf, Av1TransformSize tx, bool reduced, int index)
    {
        int minCategory = Math.Min(tx.GetWidthLog2() - 2, tx.GetHeightLog2() - 2);
        int maxCategory = Math.Max(tx.GetWidthLog2() - 2, tx.GetHeightLog2() - 2);
        if (reduced || maxCategory == 3)
        {
            encoder.WriteSymbol(index, cdf.Set3[minCategory]);
        }
        else if (minCategory == 2)
        {
            encoder.WriteSymbol(index, cdf.Set2);
        }
        else
        {
            encoder.WriteSymbol(index, cdf.Set1[minCategory]);
        }
    }
}
