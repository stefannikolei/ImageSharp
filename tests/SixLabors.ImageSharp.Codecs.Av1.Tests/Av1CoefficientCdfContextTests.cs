// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class Av1CoefficientCdfContextTests
{
    [Fact]
    public void CreateDefault_CopiesDefaultValues()
    {
        Av1CoefficientCdfContext context = Av1CoefficientCdfContext.CreateDefault(0);

        // Two-symbol group: stored as [boundary, terminal, count].
        Assert.Equal([Av1DefaultCoefficientCdf.Skip[0, 0, 0], 0, 0], context.Skip[0]);
        Assert.Equal([Av1DefaultCoefficientCdf.DcSign[0, 1, 2], 0, 0], context.DcSign[(1 * 3) + 2]);

        // Multi-symbol group: full inverse CDF.
        ushort[] expectedBaseToken = new ushort[5];
        for (int i = 0; i < 5; i++)
        {
            expectedBaseToken[i] = Av1DefaultCoefficientCdf.BaseToken[0, 2, 1, 3, i];
        }

        Assert.Equal(expectedBaseToken, context.BaseToken[(((2 * 2) + 1) * 41) + 3]);
    }

    [Fact]
    public void CreateDefault_DifferentQuantizerContextsAreIndependent()
    {
        Av1CoefficientCdfContext a = Av1CoefficientCdfContext.CreateDefault(0);
        Av1CoefficientCdfContext b = Av1CoefficientCdfContext.CreateDefault(3);

        a.BaseToken[0][0] = 12345;
        Assert.NotEqual(12345, b.BaseToken[0][0]);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        Av1CoefficientCdfContext original = Av1CoefficientCdfContext.CreateDefault(1);
        Av1CoefficientCdfContext clone = original.Clone();

        ushort before = original.BaseRange[0][0];
        clone.BaseRange[0][0] = (ushort)(before ^ 0x1FFF);
        clone.EobBin[0][0][0] = 4242;

        Assert.Equal(before, original.BaseRange[0][0]);
        Assert.NotEqual(4242, original.EobBin[0][0][0]);
    }

    [Fact]
    public void EobBin_HasExpectedShape()
    {
        Av1CoefficientCdfContext context = Av1CoefficientCdfContext.CreateDefault(0);

        // Seven transform-size contexts (16..1024); the first five are [plane][ctx] = 4 CDFs.
        Assert.Equal(7, context.EobBin.Length);
        Assert.Equal(4, context.EobBin[0].Length);  // eob_bin_16: 2x2
        Assert.Equal(2, context.EobBin[5].Length);  // eob_bin_512: 2
        Assert.Equal(6, context.EobBin[0][0].Length); // 5-symbol CDF
    }

    [Fact]
    public void ContextCdf_RoundTrips()
    {
        // Two default contexts start identical and adapt in lock-step, so a sequence encoded with
        // one decodes with the other.
        Av1CoefficientCdfContext encoderContext = Av1CoefficientCdfContext.CreateDefault(2);
        Av1CoefficientCdfContext decoderContext = Av1CoefficientCdfContext.CreateDefault(2);
        int index = (((1 * 2) + 0) * 41) + 5;

        Random random = new(99);
        int[] symbols = new int[700];
        Av1SymbolEncoder encoder = new();
        for (int i = 0; i < symbols.Length; i++)
        {
            symbols[i] = random.Next(4);
            encoder.WriteSymbol(symbols[i], encoderContext.BaseToken[index]);
        }

        byte[] data = encoder.Finish();

        Av1SymbolDecoder decoder = new(data);
        for (int i = 0; i < symbols.Length; i++)
        {
            Assert.Equal(symbols[i], decoder.ReadSymbol(decoderContext.BaseToken[index]));
        }
    }
}
