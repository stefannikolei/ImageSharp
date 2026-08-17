// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the inter motion mode for a block, a port of the reference decoder's motion-variation parse
/// (<c>decode.c</c>). When warped motion is allowed a three-way SIMPLE / OBMC / WARP symbol is read;
/// otherwise a binary OBMC flag is read.
/// </summary>
internal static class Av1MotionModeReader
{
    /// <summary>
    /// Reads the motion mode for a block.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive motion-mode CDFs.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="allowWarp">Whether warped motion is allowed for the block.</param>
    /// <returns>The decoded motion mode.</returns>
    public static Av1MotionMode ReadMotionMode(
        Av1SymbolDecoder decoder,
        Av1MotionModeCdfContext cdf,
        Av1BlockSize blockSize,
        bool allowWarp)
    {
        int index = (int)blockSize;
        if (allowWarp)
        {
            return (Av1MotionMode)decoder.ReadSymbol(cdf.MotionMode[index]!);
        }

        return decoder.ReadSymbol(cdf.Obmc[index]!) != 0 ? Av1MotionMode.Obmc : Av1MotionMode.Translation;
    }
}
