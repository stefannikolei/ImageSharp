// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

public class ScratchDump
{
    [Fact]
    public void Dump()
    {
        string? input = Environment.GetEnvironmentVariable("HBD_IN");
        string? output = Environment.GetEnvironmentVariable("HBD_OUT");
        if (input is null || output is null || !File.Exists(input))
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("HBD_HDR") == "1")
        {
            using FileStream s2 = File.OpenRead(input);
            SixLabors.ImageSharp.Formats.Av1.Containers.Ivf.IvfReader.ReadFileHeader(s2);
            SixLabors.ImageSharp.Formats.Av1.Obu.ObuSequenceHeader seq = default;
            bool haveSeq = false;
            int fi = 0;
            while (SixLabors.ImageSharp.Formats.Av1.Containers.Ivf.IvfReader.TryReadFrame(s2, out _, out byte[] fr) && fi < 3)
            {
                int off = 0;
                while (SixLabors.ImageSharp.Formats.Av1.Obu.ObuReader.TryRead(fr, ref off, out var hdr, out var pl))
                {
                    if (hdr.Type == SixLabors.ImageSharp.Formats.Av1.Obu.ObuType.SequenceHeader)
                    {
                        seq = SixLabors.ImageSharp.Formats.Av1.Obu.ObuSequenceHeader.Parse(pl);
                        haveSeq = true;
                        File.AppendAllText("/tmp/hbd/hdr.txt", $"seq max={seq.MaxFrameWidth}x{seq.MaxFrameHeight}\n");
                    }
                    else if (hdr.Type == SixLabors.ImageSharp.Formats.Av1.Obu.ObuType.Frame && haveSeq)
                    {
                        var rd = new SixLabors.ImageSharp.Formats.Av1.Bitstream.Av1BitStreamReader(pl);
                        var fh = SixLabors.ImageSharp.Formats.Av1.Obu.ObuFrameHeader.ParseIntra(ref rd, seq);
                        File.AppendAllText("/tmp/hbd/hdr.txt", $"frame {fi}: coded={fh.FrameWidth}x{fh.FrameHeight} upscaled={fh.UpscaledWidth} denom={fh.SuperresDenominator} miCols={fh.ModeInfoColumns} tiles={fh.TileColumnStarts?.Length - 1}\n");
                        fi++;
                    }
                }
            }

            return;
        }

        using FileStream stream = File.OpenRead(input);
        List<Av1DisplayFrame> frames = Av1DecoderCore.DecodeDisplayFrames(stream);
        using BinaryWriter w = new(File.Create(output));
        foreach (Av1DisplayFrame f in frames)
        {
            WritePlane(w, f.Luma);
            WritePlane(w, f.ChromaU);
            WritePlane(w, f.ChromaV);
        }
    }

    private static void WritePlane(BinaryWriter w, Av1Plane p)
    {
        bool eight = Environment.GetEnvironmentVariable("HBD_8BIT") == "1";
        for (int y = 0; y < p.CropHeight; y++)
        {
            for (int x = 0; x < p.CropWidth; x++)
            {
                if (eight)
                {
                    w.Write((byte)p.Samples[(y * p.Width) + x]);
                }
                else
                {
                    w.Write(p.Samples[(y * p.Width) + x]);
                }
            }
        }
    }
}
