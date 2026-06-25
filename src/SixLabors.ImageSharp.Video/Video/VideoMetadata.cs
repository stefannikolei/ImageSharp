// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp;

/// <summary>
/// Container-level information about a decoded video: its dimensions, frame count and frame rate.
/// </summary>
public sealed class VideoMetadata
{
    /// <summary>Gets the frame dimensions in pixels.</summary>
    public Size Size { get; init; }

    /// <summary>Gets the number of coded frames.</summary>
    public int FrameCount { get; init; }

    /// <summary>Gets the frame-rate numerator (frames per <see cref="FrameRateDenominator"/> seconds).</summary>
    public int FrameRateNumerator { get; init; }

    /// <summary>Gets the frame-rate denominator.</summary>
    public int FrameRateDenominator { get; init; }

    /// <summary>
    /// Gets the nominal duration of a single frame, derived from the frame rate, or
    /// <see cref="TimeSpan.Zero"/> when the frame rate is unknown.
    /// </summary>
    public TimeSpan FrameDuration => this.FrameRateNumerator > 0
        ? TimeSpan.FromSeconds((double)this.FrameRateDenominator / this.FrameRateNumerator)
        : TimeSpan.Zero;
}
