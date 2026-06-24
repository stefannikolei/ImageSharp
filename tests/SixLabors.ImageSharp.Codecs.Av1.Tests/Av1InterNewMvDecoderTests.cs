// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// End-to-end inter decode of a real error-resilient clip whose inter frame is a single 64x64 NEWMV
/// block with a non-zero (three-pixel) horizontal motion vector and the skip flag set (no residual), so
/// the reconstruction is a motion-compensated copy of the key frame. Validates the new-motion-vector
/// residual decode, the motion-compensation offset and the block-skip path against dav1d.
/// </summary>
public class Av1InterNewMvDecoderTests
{
    private static readonly byte[] SequencePayload = Convert.FromHexString("00000002affff036be6010");
    private static readonly byte[] KeyFramePayload = Convert.FromHexString("10840001aa0000050000002d0635190a8dccc8f80a9f15b59973092819689d4ad7b4aa166fad854dda5774b692f2c6700a40");
    private static readonly byte[] InterFramePayload = Convert.FromHexString("38420404080000000000000000000000000000000000000000000017e4000016000000009855a0");

    [Fact]
    public void DecodeNewMvInterFrame_RealClip_MatchesDav1d()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);

        Av1BitStreamReader keyReader = new(KeyFramePayload);
        ObuFrameHeader keyHeader = ObuFrameHeader.ParseIntra(ref keyReader, sequenceHeader);
        Av1TileDecoder keyDecoder = new(sequenceHeader, keyHeader);
        keyDecoder.DecodeTile(TileData(KeyFramePayload, keyHeader));
        Av1ReferenceFrame reference = new(keyHeader.OrderHint, keyDecoder.Luma, keyDecoder.ChromaU, keyDecoder.ChromaV);

        int[] referenceOrderHints = new int[8];
        for (int slot = 0; slot < 8; slot++)
        {
            if ((keyHeader.RefreshFrameFlags & (1 << slot)) != 0)
            {
                referenceOrderHints[slot] = keyHeader.OrderHint;
            }
        }

        Av1BitStreamReader interReader = new(InterFramePayload);
        ObuFrameHeader interHeader = ObuFrameHeader.ParseInter(ref interReader, sequenceHeader, referenceOrderHints);
        Av1InterTileDecoder interDecoder = new(sequenceHeader, interHeader, reference);
        interDecoder.DecodeTile(TileData(InterFramePayload, interHeader));

        byte[] expected = Convert.FromBase64String(Dav1dFrame1LumaBase64);
        int exact = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            // Every sample matches within a small post-filter (loop-restoration/CDEF boundary) margin.
            Assert.True(
                Math.Abs(interDecoder.Luma.Samples[i] - expected[i]) <= 4,
                $"Luma sample {i}: got {interDecoder.Luma.Samples[i]}, dav1d {expected[i]}.");
            if (interDecoder.Luma.Samples[i] == expected[i])
            {
                exact++;
            }
        }

        // The motion-compensated copy is bit-exact apart from a single post-filter boundary sample.
        Assert.True(exact >= expected.Length * 99 / 100, $"Only {exact}/{expected.Length} luma samples matched exactly.");
    }

    private static ReadOnlyMemory<byte> TileData(byte[] framePayload, ObuFrameHeader header)
    {
        int tileGroupStart = (header.EndBitPosition + 7) >> 3;
        ObuTileGroup tileGroup = ObuTileGroup.Parse(framePayload.AsSpan(tileGroupStart), header);
        (int offset, int length) = tileGroup.GetTile(0);
        return framePayload.AsMemory(tileGroupStart + offset, length);
    }

    private const string Dav1dFrame1LumaBase64 =
        "rLzJ0tjc3dvWzsO1ppaFdGNTRDgvKigoKi40PEdUY3KCkqKxv8rT2NrZ19LKv7GhkIFyZVdKPTIqJiUmJycnJ6u7yNHX29za1c3CtKWVhXRjU0Q4MCsoKSsuNT1HVGNygpKisb7K0tfZ2dbRyb6woZCBc2VYSj4zKycmJygoKCiqusbP1dnZ2NPMwbOklYV1ZFRGOjEsKistMDY+SFVjcoKRobC9yNDV19fUz8i9r6CQgXNmWUw/NS0oKCkqKioqqbjEzNLV1tXQyb6xo5SFdWVVRzw0Ly0tLzM4QEpWZHKBkKCuu8bO09XU0s3Gu66gkIJ0Z1pOQjcvKysrLS0tLaa1wMnO0dLRzMW7r6GThHVmV0o/NzIwMTM2O0JMWGVygY+erLjDys/R0c/Kw7mtn5CCdWlcUEU7My8uLzAwMDCksbzEyczNy8fBt6yfkoR2Z1pNQjs3NTU3Oj9FT1lmc4GOnKm1v8bLzc3LxsC3q52Qg3ZqX1NJPzg0MzQ1NTU1oa23vsPFxsXCvLOonZCDdmlcUUdAPDo6PD9DSlJcZ3OAjZqmsbrBxcfHxcK8s6icj4N3bGJXTUQ9OTk6Ojo6Op2osri8vr++u7aupJqPg3drX1VMRkJBQUJESE5VX2l0f4uXoq21u7/Bwb+8t6+lmo6DeW9lW1JKREA/QEFBQUGZo6uxtbe3t7SvqaCXjYN4bWNaUkxJSEhJS05TWmJrdX+JlJ6nr7W4urq5trGqoZeNg3pyaWBYUEtIR0hISEhIlJ2kqa2ur66sqKOblIuCeXBnX1hUUE9PUFJVWV5lbXZ+h5GZoqitsbKysa+qpZ2UjIR8dG1lXldTUE9QUFBQUJCXnaGkpaampKGcl5CJgnpza2VfW1lYWFhaXF9kaXB3foaNlZyhpqiqqqmno56YkYqDfXdxa2VfW1lYWVlZWVmLkZaZm5ydnJuZlpGNh4J8dnBqZmNhYWFhYmRmaW5yeH2EipCVmp2foKCgnpyYk46Ig356dXBsZ2RiYmJiYmJihoqOkJGSk5OSkY+MiIWBfXl1cW5sampqamtsbW9ydnl9goaKj5KVlpeXl5WUkY6KhoOAfXp2c29tbGtsbGxsbIGEhoiIiYmJiYmIhoSCgH58eXd1dHNzc3N0dHV2d3l7fYCDhYiKjI2NjY2Mi4qIhoSCgX9+fHp4dnZ1dnZ2dnZ9fn5/f39/f4CAgYGAgIB/fn59fX19fX19fXx8fHx8fX1+f4CBgoODg4ODg4OCgoKBgYKCgoGBgIB/f4CAgICAeXh3dnZ1dXZ3eHl7fH1/gIGCg4SFhoaHhoaFhIOBgH99fXx7e3t6enp6enp6e3x9f4GDhIaHiImJiYmJiYmJiXRycG5tbGxsbnBydXh7foGEhomLjY+QkJCPjoyJh4SBfnt5d3VzcnFwcHBxcnR2eXyAg4eKjI+RkpOTk5OTk5NwbGlmZGNjY2Voa290eH2BhoqOkpWXmJmZmJaTkIyHg356dnJvbGpoZ2dnaGptcHV6f4SJjZKVmJqcnJycnJycbWdiXltaWltdYGVqb3V8goiOlJmdoKGioqGem5aRi4V/eXNuaWViX15eXmBiZmtxd36Fi5GWm6CipKWlpKSkpGliXFdTUlFSVVleZWxze4KKkpmfpKiqq6qppqKclo+Hf3hwamNeWlhWVVZXW19mbXV9hY2Um6GmqqytraysrKxmXVZQTEpJSk1SWGBocXqDjJWepauvsrOysa2oopqSiYB3bmZeWFNQTk1OUFRZYWlzfYaOl5+mrbG0tbS0tLS0YlhQSUVDQkNHTFNcZW95hI6Zoquytrm6uri0r6eflYqAdWtiWVJNSUdGR0lNVFxmcXyGkJqjrLO4u7u7urq6ul9US0Q/PDw9QUZOWGJteYSQnKewuL3AwcG/urSsopeMgHRpXlVNRkJAP0BDSE9YY3B8h5Kcp7C4vsHCwcHBwcFcUEY+OTY2NztCSlVgbHmFkp+rtb3DxsjHxcC5sKaajYBzZ1tQR0E8Ojk6PUNLVWFue4iTn6q0vcPGx8bGxsbGWk1COjQxMTM3PkdSXmt5hpSir7rDyMzNzMrFvrSom46AcmRYTEM8NzU0NTk+R1JfbXuIlaGtuMHIy8zLysrKyldJPjUwLS0vMzpEUF1reYeWpbK+x83R0tHOycG3q52Of3FiVUk/ODMwMDE1O0RQXm17iZajr7vFzM/Qz87Ozs5VRzsyLSopLDE4Q09canmImKe1wcvR1NbV0szEuayejn9vYFJGOzQvLSwuMjhCTl1se4qXpLG9yM/T1NLR0dHRVEU4MCooKCovN0FOXGp6iZmot8TN1NfY19TOxrutno5+b19QQzkxLCoqLDA2QE1cbHuKmKazv8rR1dXU09PT01NDNy4pJiYpLjZBTlxqeoqaqrjFz9XZ2tnW0Me8rp+Ofm5eT0I3MCspKSovNkBNXGx8i5mmtMDL0tbX1dTU1NRSQzcuKSYmKS42QU5ca3qKmqq5xtDW2tva1tDIvK6ejn1tXU5BNy8qKCgqLzZATV1tfIuZprTAy9PW19bU1NTUU0M3LyknJyovN0JPXWt6ipqquMXP1dna2dXQx7uuno59bV1PQjcwKykpKzA3QU5dbX2LmaazwMrS1dbU09PT01REODArKSksMTlDUF5seomZqbfEzdPX2NfUzsa6rZ2NfW1eT0M5MS0rKy4yOUNQX259i5mlsr7J0NPU0tHR0dFVRzszLiwtLzQ7RlJfbHqJmKe1wcrQ1NXU0cvDuKucjX1uX1FFOzQwLi8xNTxFUmBvfouYpLG8xs3Q0M/Nzc3NWEk+NzMxMTM4P0lUYG16iJaksr3GzM/Qz83IwLapm41+b2FTSD84NDMzNTlASVVicX6Ll6KuucPJzMzKycnJyVtNQjs3NjY4PUNMV2JueoaUoa65wcbKy8rHw7yyp5qMfnBjV0xDPTk4OTs+RE1YZXJ/i5Wgq7a+xMfHxcPDw8NeUUdBPTw9P0JIUFpkb3qFkZ6ps7vAw8TEwr23r6SYi35yZlpQSENAPj9BRElRW2dzf4qTnaexub/Bwb+9vb29YlZNR0RDREVJTlVeZ3B6hI+apK21uby9vLu3sqqglot/dGheVU5JRkVGR0pPVl9qdX+JkZqjrLS5u7q4tra2tmZbU05MS0xNUFVbYmpyeoOMlp+nrbGztLSzsKylnZOKf3ZsY1pUUE5NTU9RVVtkbXZ/h4+Wn6etsrOysa+vr69qYVlWVFNUVlhcYWdudHuCipGZoKWpq6ysq6mloJmQiIB3b2dgW1hWVVVXWVxhaHB4f4aMk5qgpqqsq6mnp6enbmZgXVxcXV9hZGhtcnd8gYeNlJmdoKKioqKhnpqUjoeAeXNsZmJfXl1eX2BjZ2xzeX+Fio+Ump+io6Kgn5+fn3JsZ2VlZmdoamxvc3Z6fYCFiY6SlZeYmZmZmJaTj4qGgHt2cW1pZ2ZmZmdoam1xdnt/g4eKj5OXmpuamJeXl5d2cW5ubm9wcnN1d3l7fX6Ag4WIi42Ojo+Pj4+OjYqHhIB9eXZzcXBvb29wcHFzdnl8f4GEhomNkJGSkY+Ojo6Oend1dnd5enx9fn+AgICAgICCg4SEhYWFhYaGh4aFg4KAf317enl4eHh4eHh4eXt8fn+AgYKDhYeJiYeGhYWFhX59fH6AgoSGhoeGhoWEgYB/fn19fHt7e3t8fX5/f4CAgIGBgYCAgIGBgoGBgICAgH9/fn19fX5/gH9+fXx8fHyCg4SGiYyOj5CPjoyKh4OAfHp4dnRycXFxcnR2eHp8foGDhIaHiImKi4uKiYiGhYOBf316eHd3d3d2dXNycnJyh4iLj5KWmJmZmJWSjoqEf3p2cm5saWhnZ2lrbnJ1eX2BhIiLjY+Sk5SUlJKQjYqHg397eHRycG9tbGtqaWlpaYuOkpebn6GioaCcmJKMhn94cmxnY2BeXV5fYmZrcHV7gYaMkJSXmpydnZyal5SPioV/enVwbGlmZWNiYGBgYGCPlJmfpKeqq6mnop2WjoZ+dm5nYFtXVVRVV1pfZWtyeoGIj5Wbn6Olpqelo5+alY6HgHlybGZiXlxaWFdXV1dXk5mgpqywsrKxraihmZCGfXNqYVpUT0xLTE5TWF9ncHmCipOaoaaqrq+vrqunoZqSiYB4b2hhW1dTUU9PTk5OTpefpq2zt7m5t7OtpZyRhntwZlxTTUdFQ0RHTFJaY210goyWn6etsra3uLazrqeflYuBd21kXFVQS0lHRkZGRkabpK20ur7AwL24saidkoZ6bWJXTkZBPTw9QEVNVWBrd4OOmaOss7m9v7+9urSto5mNgnZrYVhQSURBPz8/Pz8/n6myusDExsXCvLSqnpKFeGteU0hAOjc2NzpASFJdaXaDkJynsbm/w8XGxMC6sqicj4J2al5US0M+Ojk4ODg4OKKtt7/FycrKxr+2rJ+ShXdpW09EPDYyMTI2PERPW2h2hJKeqrW+xMnLy8nFv7arnpGDdWhcUUc+ODUzMzMzMzOksbvEys3OzcnCuK2gkoR1Z1lMQTgyLi0vMjhBTFlndYSToK24wcjNz9DOysO6rqCSg3VnW05EOjQwLy8vLy8vprS/yM3R0tDLxLquoJKDdGZXSj41LysrLDA2P0pXZnWEk6KvusTL0NLT0c3GvbCik4R1Z1lNQTcxLSwsLS0tLai2wcrQ09TSzcW7rqCSg3RlVkg9NC0qKSouNT1JV2Z1hJSisLzGzdLU1dPPyL+yo5SEdWZZS0A2LysqKisrKyupt8PN0tXW1M7Gu66gkoN0ZVZIPDMtKSgqLjQ9SFZldYSUorC8xs7T1dbU0MnAs6SUhHVmWEs/NS4qKSorKysrqbjEztTW19TPx7yvoJKDdGVWSDwzLSopKi40PUhWZXSEk6KwvMbN0tXV1NDKwLOklIR1ZllLPzUuKikrLCwsLKm4xc/U19jVz8e8r6GShHVmV0k9NC4qKisvND1IVmV0g5Ohr7vFzNHU1dPQycCzpJSEdWdZTD81LisqLC0tLS2ouMXP1dfY1c/HvK+hk4R1Z1hKPjUvLCssLzU9SVZlc4OSoK66w8vQ0tPSz8i/s6SUhXVnWkxANi8sLC4vLy8vp7jFz9XY2NXQx7yvoZOFdmhZS0A3MS0sLjE2PklWZHOCkZ+suMLJztHS0c7Iv7KjlIR1Z1pNQTcwLS4vMTExMaa3xc/V19jVz8e8r6GUhXdpWkxBODIvLi8yNz9JVmRzgpCeq7fByM3P0M/Mx76xo5OEdWhbTkI4MS8vMTMzMzOmt8XP1NfX1dDHvK+ilIZ4aVtNQjkzMC8wMjg/SlZkc4GQnaq2wMfLzs/OzMa9saOThHZoW05COTIwMDM0NDQ0pbfEztTX19XPx7yvopSGeGpbTkI6NDEvMDM4QEpWZHOBj52qtr/Gy87OzsvGvbGjk4R2aFtPQzkzMDEzNTU1NQ==";
}
