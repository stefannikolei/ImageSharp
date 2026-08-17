// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// End-to-end "load a video, extract frames as images" validation: a real two-frame error-resilient
/// AV1/IVF clip (a key frame and a single-reference inter frame) is decoded through the public
/// <see cref="Av1Decoder"/> into a multi-frame <see cref="Image{TPixel}"/>, exercising the frame loop,
/// the reference-frame store, the inter decode and the YUV-to-RGB conversion.
/// </summary>
public class Av1VideoDecodeTests
{
    // A 64x64, two-frame, error-resilient clip: frame 0 key, frame 1 single 64x64 NEARESTMV block.
    private static readonly byte[] ClipE64 = Convert.FromBase64String(
        "REtJRgAAIABBVjAxQABAAB4AAAABAAAAAgAAAAAAAAA0AAAAAAAAAAAAAAASAAoLAAAAAq//8Da+YBAyIxAIAAExAAAEFgAAHUIzTFWvgVZVuTURi6pw4B6icPVgXyJUKwAAAAEAAAAAAAAAEgAyJzgEBAQIAAAAAAAAAAAAAAAAAAAAAAAAAAAAABYAAAAUWAAAACA/gA==");

    [Fact]
    public void Decode_TwoFrameClip_ProducesTwoImageFrames()
    {
        using MemoryStream stream = new(ClipE64);
        using Image<Rgba32> image = ((IImageDecoder)Av1Decoder.Instance).Decode<Rgba32>(new DecoderOptions(), stream);

        Assert.Equal(64, image.Width);
        Assert.Equal(64, image.Height);
        Assert.Equal(2, image.Frames.Count);

        // The two frames differ (the inter frame is the key frame plus a small residual), confirming the
        // inter frame was actually decoded rather than copied.
        Rgba32 keyPixel = image.Frames[0][10, 10];
        Rgba32 interPixel = image.Frames[1][10, 10];
        Assert.NotEqual(keyPixel, interPixel);
    }

    [Fact]
    public void Decode_ExtractsArbitraryFrameAsImage()
    {
        using MemoryStream stream = new(ClipE64);
        using Image<Rgba32> video = ((IImageDecoder)Av1Decoder.Instance).Decode<Rgba32>(new DecoderOptions(), stream);

        // Pull out the second (inter) frame as a standalone image, as a frame-extraction workflow would.
        using Image<Rgba32> secondFrame = video.Frames.CloneFrame(1);

        Assert.Equal(1, secondFrame.Frames.Count);
        Assert.Equal(64, secondFrame.Width);
        Assert.Equal(64, secondFrame.Height);
    }
}
