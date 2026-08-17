// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the chroma-from-luma AC computation and prediction against dav1d 1.4.1's <c>cfl_ac</c> and
/// <c>cfl_pred</c> for random luma blocks, covering the 4:2:0, 4:2:2 and 4:4:4 subsampling layouts.
/// </summary>
public class Av1ChromaFromLumaTests
{
    [Theory]
    [InlineData(
        4,
        4,
        1,
        1,
        128,
        8,
        "RCCCPP3m8cJrMPkOx90B5Ih1NKIPCw0Ew27YDnHg/XewdnDrlAvVM1+XParYYZuR/8kR9XzO1Fi7vyzgN1PJvQ==",
        "vv1K/84C8AAcADj/lv7K/vj/RABw/ygARALk/2j/JAE=",
        "OGnanoRnU1l/iW6FyXxtpQ==")]
    [InlineData(
        8,
        8,
        1,
        0,
        100,
        -12,
        "HC4ruFadgGwSUdzJvuOJEg667qPC2FRaeHYMWqZYRbhd5NS6tbnkUszsf/qO/7Xo7LPp+XGmVYn1npvQn2r6uyauBGE2HhmLdDZFiH1rHtgQHbm4WH8MKjoiDBQKv4JBUF4AxRZ+TRICsDmSrPoPneUXh81O8nMvoTQM5UHJ+ac=",
        "M/2X/9f/u/+X/Z8CjwJ3/iv/TwJzAsP+w/+j/QMA//8PAUMCwwHjAOsC7wE/An8ChwKTA2cAg/9XArcBLwDfAlv/n/1b/Zv+s/4//6v/4/+//M8BZ//j/Hv9i/wv/xf/w/4f/1v+h/3T/jf/owK7/vv/WwELAZP+X//P/zMAiwI=",
        "6nhscdgAAK6MAACfb9VjZDEADzkABwAAAABRewASWwCD1uOnooh0af8Ngfnd/4uQn46z25yKAKFlIzKogm1aAA==")]
    [InlineData(
        8,
        4,
        0,
        0,
        140,
        16,
        "eUK98iEG8IR3YvDzy012TccHIFEVmg+J8sbayuNEuzE=",
        "pP/s/cQBbAPk/Az8XAP8/5T/7P5cA3QDNAJE/oz/RP4UAhT83Pxk/oT8rABU/CQAbAMMAqwCLAL0Avz9tAFk/Q==",
        "dQf9/wAA/4txR////x1vHf8AACUAtwCV//////8L+QA=")]
    public void ComputeAcAndPredict_MatchDav1d(int cw, int ch, int sh, int sv, int dc, int alpha, string lumaBase64, string acBase64, string predBase64)
    {
        ushort[] luma = Av1TestData.Widen(Convert.FromBase64String(lumaBase64));
        byte[] acBytes = Convert.FromBase64String(acBase64);
        ushort[] expectedPred = Av1TestData.Widen(Convert.FromBase64String(predBase64));

        int n = cw * ch;
        int[] expectedAc = new int[n];
        for (int k = 0; k < n; k++)
        {
            expectedAc[k] = BinaryPrimitives.ReadInt16LittleEndian(acBytes.AsSpan(k * 2));
        }

        int lw = cw << sh;
        int[] ac = new int[n];
        Av1ChromaFromLuma.ComputeAc(luma, 0, lw, cw, ch, sh, sv, ac);
        Assert.Equal(expectedAc, ac);

        ushort[] pred = new ushort[n];
        Av1ChromaFromLuma.Predict(dc, alpha, ac, cw, ch, pred);
        Assert.Equal(expectedPred, pred);
    }
}
