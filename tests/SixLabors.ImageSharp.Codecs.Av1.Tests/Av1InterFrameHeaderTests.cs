// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Containers.Ivf;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the inter frame-header parse against dav1d's parsed values for a 2-frame clip (a key frame
/// followed by a single-reference inter frame). The key frame establishes the reference order hints; the
/// inter header fields and the resulting tile-data offset are then checked.
/// </summary>
public class Av1InterFrameHeaderTests
{
    private const string IvfBase64 = "REtJRgAAIABBVjAxQABAAB4AAAABAAAAAgAAAAAAAAB8AAAAAAAAAAAAAAASAAoKAAAAAq//m18wCDJsFAAjAAAAgAAAgOU0yEBV+N3klCtyyHXjf8Vyy8vDISzuE6KJgZ6uyqpUShWglA6L2ZksPfcK4iQUAGkPdj5xSB1qL87v2VKDAexpLfR+XNcRRYfXpuIJdyobzk9gHAV8T87D/L1F5nuCtP0SJgAAAAEAAAAAAAAAEgAyIjIB4EAAAANgAAABQAABQADP5ee9s/82a8qQNqkCQ+Dl+tw=";

    [Fact]
    public void ParseInter_MatchesDav1d()
    {
        using MemoryStream stream = new(Convert.FromBase64String(IvfBase64));
        IvfReader.ReadFileHeader(stream);

        // Frame 1: key frame -> all reference slots take its order hint.
        Assert.True(IvfReader.TryReadFrame(stream, out _, out byte[] keyFrame));
        ObuSequenceHeader sequenceHeader = default;
        bool haveSeq = false;
        int orderHint0 = 0;
        int off = 0;
        while (ObuReader.TryRead(keyFrame, ref off, out ObuHeader h, out ReadOnlySpan<byte> payload))
        {
            if (h.Type == ObuType.SequenceHeader)
            {
                sequenceHeader = ObuSequenceHeader.Parse(payload);
                haveSeq = true;
            }
            else if (h.Type == ObuType.Frame)
            {
                Av1BitStreamReader r = new(payload);
                ObuFrameHeader kh = ObuFrameHeader.ParseIntra(ref r, sequenceHeader);
                orderHint0 = kh.OrderHint;
            }
        }

        Assert.True(haveSeq);
        int[] refOrderHints = new int[8];
        for (int i = 0; i < 8; i++)
        {
            refOrderHints[i] = orderHint0;
        }

        // Frame 2: inter frame.
        Assert.True(IvfReader.TryReadFrame(stream, out _, out byte[] interFrame));
        off = 0;
        ObuFrameHeader ih = default;
        byte[] interPayload = [];
        bool found = false;
        while (ObuReader.TryRead(interFrame, ref off, out ObuHeader h, out ReadOnlySpan<byte> payload))
        {
            if (h.Type == ObuType.SequenceHeader)
            {
                sequenceHeader = ObuSequenceHeader.Parse(payload);
            }
            else if (h.Type == ObuType.Frame)
            {
                Av1BitStreamReader r = new(payload);
                ih = ObuFrameHeader.ParseInter(ref r, sequenceHeader, refOrderHints);
                interPayload = payload.ToArray();
                found = true;
            }
        }

        Assert.True(found);
        Assert.Equal(Av1FrameType.Inter, ih.FrameType);
        Assert.Equal(7, ih.PrimaryRefFrame);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0 }, ih.ReferenceFrameIndices);
        Assert.False(ih.AllowHighPrecisionMv);
        Assert.Equal(0, ih.InterpolationFilter);
        Assert.True(ih.IsMotionModeSwitchable);
        Assert.True(ih.UseReferenceFrameMotionVectors);
        Assert.Equal(64, ih.FrameWidth);
        Assert.Equal(64, ih.FrameHeight);
        Assert.Equal(1, ih.OrderHint);
        Assert.False(ih.SkipModeEnabled);
        Assert.True(ih.AllowWarpedMotion);

        // The header must consume exactly the right number of bits so the tile group parses to a valid tile.
        int tileGroupStart = (ih.EndBitPosition + 7) >> 3;
        ObuTileGroup tileGroup = ObuTileGroup.Parse(interPayload.AsSpan(tileGroupStart), ih);
        (int tileOffset, int tileLength) = tileGroup.GetTile(0);
        Assert.True(tileLength > 0);
        Assert.True(tileGroupStart + tileOffset + tileLength <= interPayload.Length);
    }
}
