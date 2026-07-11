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

        using FileStream stream = File.OpenRead(input);
        List<Av1DisplayFrame> frames = Av1DecoderCore.DecodeDisplayFrames(stream);
        using BinaryWriter w = new(File.Create(output));
        bool lumaOnly = Environment.GetEnvironmentVariable("HBD_LUMA_ONLY") == "1";
        foreach (Av1DisplayFrame f in frames)
        {
            WritePlane(w, f.Luma);
            if (!lumaOnly)
            {
                WritePlane(w, f.ChromaU);
                WritePlane(w, f.ChromaV);
            }
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
