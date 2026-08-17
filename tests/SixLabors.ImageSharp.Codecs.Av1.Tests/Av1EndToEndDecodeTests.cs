// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// End-to-end decode of a real AV1/IVF clip into an image, exercising the full path from container
/// demuxing through reconstruction and YUV-to-RGB conversion.
/// </summary>
public class Av1EndToEndDecodeTests
{
    // A real 64x64 two-frame all-intra AV1/IVF clip produced by aomenc 3.8.2.
    private const string TinyIvfBase64 = "REtJRgAAIABBVjAxQABAAB4AAAABAAAAAgAAAAAAAAAlAAAAAAAAAAAAAAASAAoKAAAAAq//m18wCDIVEADQAAACiAAAH/gZXiPv/K/uo02mJQAAAAEAAAAAAAAAEgAKCgAAAAKv/5tfMAgyFRAA0AAAAogAAB/4GV4j7/y47H02mA==";

    [Fact]
    public void Decode_RealClip_ProducesImage()
    {
        byte[] ivf = Convert.FromBase64String(TinyIvfBase64);
        using MemoryStream stream = new(ivf);

        using Image<Rgba32> image = Av1Decoder.Instance.Decode<Rgba32>(new DecoderOptions(), stream);

        Assert.Equal(64, image.Width);
        Assert.Equal(64, image.Height);

        // The decoded luma is a smooth gradient; the converted image must be non-trivial and in range.
        Rgba32 topLeft = image[0, 0];
        Rgba32 bottomRight = image[63, 63];
        Assert.Equal(255, topLeft.A);
        Assert.True(bottomRight.R > topLeft.R, "Expected the reconstructed gradient to brighten towards the bottom-right.");
    }
}
