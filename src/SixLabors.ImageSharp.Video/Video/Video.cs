// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Video;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// A loaded video: a codec-agnostic, lazily-decoded sequence of frames, the parallel of
/// <see cref="Image"/> for moving pictures. Loading only reads the container index (cheap); each frame
/// is decoded on demand via <see cref="GetFrame{TPixel}(int)"/> / <see cref="DecodeFrames{TPixel}()"/>,
/// so the whole video is never materialized in memory at once. Frames are returned as standalone
/// <see cref="Image{TPixel}"/> instances owned by the caller.
/// </summary>
public sealed class Video : IDisposable
{
    private readonly Stream stream;
    private readonly bool ownsStream;
    private readonly IVideoFrameSource frameSource;
    private readonly Configuration configuration;
    private bool isDisposed;

    private Video(Stream stream, bool ownsStream, IVideoFrameSource frameSource, Configuration configuration)
    {
        this.stream = stream;
        this.ownsStream = ownsStream;
        this.frameSource = frameSource;
        this.configuration = configuration;
    }

    /// <summary>Gets the frame dimensions in pixels.</summary>
    public Size Size => this.frameSource.Size;

    /// <summary>Gets the frame width in pixels.</summary>
    public int Width => this.Size.Width;

    /// <summary>Gets the frame height in pixels.</summary>
    public int Height => this.Size.Height;

    /// <summary>Gets the number of frames.</summary>
    public int FrameCount => this.frameSource.FrameCount;

    /// <summary>Gets the container-level metadata (frame rate, etc.).</summary>
    public VideoMetadata Metadata => this.frameSource.Metadata;

    /// <summary>Loads a video from a file path.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The loaded video.</returns>
    public static Video Load(string path) => Load(new VideoDecoderOptions(), path);

    /// <summary>Loads a video from a file path with the given options.</summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="path">The file path.</param>
    /// <returns>The loaded video.</returns>
    public static Video Load(VideoDecoderOptions options, string path)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNullOrWhiteSpace(path, nameof(path));
        FileStream fileStream = File.OpenRead(path);
        return Load(options, fileStream, ownsStream: true);
    }

    /// <summary>Loads a video from a stream.</summary>
    /// <param name="stream">The input stream.</param>
    /// <returns>The loaded video.</returns>
    public static Video Load(Stream stream) => Load(new VideoDecoderOptions(), stream);

    /// <summary>Loads a video from a stream with the given options.</summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="stream">The input stream.</param>
    /// <returns>The loaded video.</returns>
    public static Video Load(VideoDecoderOptions options, Stream stream)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));
        return Load(options, stream, ownsStream: false);
    }

    /// <summary>
    /// Reads the container headers to report the video's dimensions, frame count and frame rate without
    /// decoding any pixels.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <returns>The identified video info.</returns>
    public static VideoInfo Identify(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));
        VideoDecoderOptions options = new();
        (Stream seekable, bool owns) = EnsureSeekable(stream);
        try
        {
            IVideoDecoder decoder = FindDecoder(seekable);
            return decoder.Identify(options, seekable);
        }
        finally
        {
            if (owns)
            {
                seekable.Dispose();
            }
        }
    }

    /// <summary>
    /// Decodes the frame at the given index into a new image. Frames are decoded on demand; accessing an
    /// arbitrary frame seeks to the nearest preceding keyframe and decodes forward.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="index">The zero-based frame index.</param>
    /// <returns>The decoded frame as a standalone image owned by the caller.</returns>
    public Image<TPixel> GetFrame<TPixel>(int index)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.EnsureNotDisposed();
        if ((uint)index >= (uint)this.FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return this.frameSource.DecodeFrame<TPixel>(index, this.configuration);
    }

    /// <summary>Decodes the frame at the given index as an <see cref="Rgba32"/> image.</summary>
    /// <param name="index">The zero-based frame index.</param>
    /// <returns>The decoded frame.</returns>
    public Image<Rgba32> GetFrame(int index) => this.GetFrame<Rgba32>(index);

    /// <summary>
    /// Lazily decodes every frame in order. Each yielded image is owned by the caller and should be
    /// disposed before requesting the next when memory is a concern.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <returns>An enumerable of decoded frames.</returns>
    public IEnumerable<Image<TPixel>> DecodeFrames<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
        => this.DecodeFrames<TPixel>(0, this.FrameCount);

    /// <summary>
    /// Lazily decodes a range of frames in order.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="start">The first frame index.</param>
    /// <param name="count">The number of frames to decode.</param>
    /// <returns>An enumerable of decoded frames.</returns>
    public IEnumerable<Image<TPixel>> DecodeFrames<TPixel>(int start, int count)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (int i = 0; i < count; i++)
        {
            yield return this.GetFrame<TPixel>(start + i);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.frameSource.Dispose();
        if (this.ownsStream)
        {
            this.stream.Dispose();
        }

        this.isDisposed = true;
    }

    private static Video Load(VideoDecoderOptions options, Stream stream, bool ownsStream)
    {
        (Stream seekable, bool buffered) = EnsureSeekable(stream);
        bool owns = ownsStream || buffered;
        try
        {
            IVideoDecoder decoder = FindDecoder(seekable);
            IVideoFrameSource source = decoder.CreateFrameSource(options, seekable);
            return new Video(seekable, owns, source, options.Configuration);
        }
        catch
        {
            if (buffered)
            {
                seekable.Dispose();
            }

            throw;
        }
    }

    private static (Stream Stream, bool Buffered) EnsureSeekable(Stream stream)
    {
        if (stream.CanSeek)
        {
            return (stream, false);
        }

        // Buffer the (small) encoded stream so the frame source can seek for random access.
        MemoryStream buffer = new();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return (buffer, true);
    }

    private static IVideoDecoder FindDecoder(Stream stream)
    {
        Span<byte> header = stackalloc byte[32];
        long position = stream.Position;
        int read = stream.Read(header);
        stream.Position = position;
        if (!VideoFormatManager.Default.TryFindDecoder(header[..read], out IVideoDecoder? decoder))
        {
            throw new NotSupportedException("The stream is not a recognized video format.");
        }

        return decoder!;
    }

    private void EnsureNotDisposed()
    {
        if (this.isDisposed)
        {
            throw new ObjectDisposedException(nameof(Video));
        }
    }
}
