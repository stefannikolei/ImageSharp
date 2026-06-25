// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the codec-agnostic <see cref="Video"/> API end-to-end on a real two-frame AV1/IVF clip
/// (a key frame and a single-reference inter frame): loading, metadata, lazy and random-access frame
/// extraction, and equivalence with the already-dav1d-validated <see cref="Av1Decoder"/> image path.
/// </summary>
public class Av1VideoApiTests
{
    private static readonly byte[] ClipE64 = Convert.FromBase64String(
        "REtJRgAAIABBVjAxQABAAB4AAAABAAAAAgAAAAAAAAA0AAAAAAAAAAAAAAASAAoLAAAAAq//8Da+YBAyIxAIAAExAAAEFgAAHUIzTFWvgVZVuTURi6pw4B6icPVgXyJUKwAAAAEAAAAAAAAAEgAyJzgEBAQIAAAAAAAAAAAAAAAAAAAAAAAAAAAAABYAAAAUWAAAACA/gA==");

    [Fact]
    public void Load_ReportsContainerMetadata()
    {
        using Video video = Video.Load(new MemoryStream(ClipE64));

        Assert.Equal(64, video.Width);
        Assert.Equal(64, video.Height);
        Assert.Equal(2, video.FrameCount);
        Assert.True(video.Metadata.FrameRateNumerator > 0);
        Assert.True(video.Metadata.FrameDuration > TimeSpan.Zero);
    }

    [Fact]
    public void Identify_ReadsHeaderWithoutDecoding()
    {
        VideoInfo info = Video.Identify(new MemoryStream(ClipE64));

        Assert.Equal(new Size(64, 64), info.Size);
        Assert.Equal(2, info.FrameCount);
    }

    [Fact]
    public void GetFrame_DecodesEachFrameAsImage()
    {
        using Video video = Video.Load(new MemoryStream(ClipE64));

        using Image<Rgba32> frame0 = video.GetFrame<Rgba32>(0);
        using Image<Rgba32> frame1 = video.GetFrame<Rgba32>(1);

        Assert.Equal(64, frame0.Width);
        Assert.Equal(1, frame0.Frames.Count);

        // The inter frame is the key frame plus a residual, so it must differ.
        Assert.NotEqual(frame0[10, 10], frame1[10, 10]);
    }

    [Fact]
    public void GetFrame_RandomAccess_MatchesSequentialDecode()
    {
        // Decode frame 1 directly after loading (forces a seek to the keyframe + forward decode).
        using Image<Rgba32> randomAccess = DecodeFrameFresh(1);

        // Decode it sequentially (frame 0 first, then frame 1) in a separate video.
        using Video sequential = Video.Load(new MemoryStream(ClipE64));
        using Image<Rgba32> _ = sequential.GetFrame<Rgba32>(0);
        using Image<Rgba32> sequentialFrame1 = sequential.GetFrame<Rgba32>(1);

        AssertPixelEqual(sequentialFrame1, randomAccess);
    }

    [Fact]
    public void DecodeFrames_EnumeratesAllFrames()
    {
        using Video video = Video.Load(new MemoryStream(ClipE64));

        List<Image<Rgba32>> frames = video.DecodeFrames<Rgba32>().ToList();
        try
        {
            Assert.Equal(2, frames.Count);
            Assert.All(frames, f => Assert.Equal(new Size(64, 64), f.Size));
        }
        finally
        {
            foreach (Image<Rgba32> frame in frames)
            {
                frame.Dispose();
            }
        }
    }

    [Fact]
    public void VideoFrame_MatchesImageDecoderPath()
    {
        // The Image.Load path is already validated against dav1d; the Video path must produce identical pixels.
        using Image<Rgba32> viaImage = ((IImageDecoder)Av1Decoder.Instance).Decode<Rgba32>(new DecoderOptions(), new MemoryStream(ClipE64));
        using Image<Rgba32> viaVideo = DecodeFrameFresh(1);

        using Image<Rgba32> imageFrame1 = viaImage.Frames.CloneFrame(1);
        AssertPixelEqual(imageFrame1, viaVideo);
    }

    private static Image<Rgba32> DecodeFrameFresh(int index)
    {
        using Video video = Video.Load(new MemoryStream(ClipE64));
        return video.GetFrame<Rgba32>(index);
    }

    private static void AssertPixelEqual(Image<Rgba32> expected, Image<Rgba32> actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }
}
