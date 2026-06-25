// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <content>
/// Dispatches a one-dimensional inverse transform by kind and length, selecting the appropriate
/// butterfly implementation. This is the building block for the separable 2D inverse transform.
/// </content>
internal static partial class Av1InverseTransform1d
{
    /// <summary>
    /// Applies a one-dimensional inverse transform of the given kind and length over a strided
    /// sequence of coefficients.
    /// </summary>
    /// <param name="type">The 1D transform kind.</param>
    /// <param name="length">The transform length (4, 8, 16, 32 or 64).</param>
    /// <param name="c">The coefficient buffer.</param>
    /// <param name="offset">The offset of the first coefficient.</param>
    /// <param name="stride">The distance between consecutive coefficients.</param>
    /// <param name="min">The lower clamp bound.</param>
    /// <param name="max">The upper clamp bound.</param>
    /// <exception cref="NotSupportedException">The kind/length combination is not valid in AV1.</exception>
    public static void Apply(Av1Transform1dType type, int length, Span<int> c, int offset, int stride, int min, int max)
    {
        switch (type)
        {
            case Av1Transform1dType.Dct:
                ApplyDct(length, c, offset, stride, min, max);
                break;
            case Av1Transform1dType.Adst:
                ApplyAdst(length, c, offset, stride, min, max, flip: false);
                break;
            case Av1Transform1dType.FlipAdst:
                ApplyAdst(length, c, offset, stride, min, max, flip: true);
                break;
            case Av1Transform1dType.Identity:
                ApplyIdentity(length, c, offset, stride);
                break;
            default:
                throw new NotSupportedException($"Unsupported 1D transform kind '{type}'.");
        }
    }

    private static void ApplyDct(int length, Span<int> c, int offset, int stride, int min, int max)
    {
        switch (length)
        {
            case 4:
                InverseDct4(c, offset, stride, min, max);
                break;
            case 8:
                InverseDct8(c, offset, stride, min, max);
                break;
            case 16:
                InverseDct16(c, offset, stride, min, max);
                break;
            case 32:
                InverseDct32(c, offset, stride, min, max);
                break;
            case 64:
                InverseDct64(c, offset, stride, min, max);
                break;
            default:
                throw new NotSupportedException($"Unsupported inverse DCT length '{length}'.");
        }
    }

    private static void ApplyAdst(int length, Span<int> c, int offset, int stride, int min, int max, bool flip)
    {
        switch (length)
        {
            case 4:
                if (flip)
                {
                    InverseFlipAdst4(c, offset, stride, min, max);
                }
                else
                {
                    InverseAdst4(c, offset, stride, min, max);
                }

                break;
            case 8:
                if (flip)
                {
                    InverseFlipAdst8(c, offset, stride, min, max);
                }
                else
                {
                    InverseAdst8(c, offset, stride, min, max);
                }

                break;
            case 16:
                if (flip)
                {
                    InverseFlipAdst16(c, offset, stride, min, max);
                }
                else
                {
                    InverseAdst16(c, offset, stride, min, max);
                }

                break;
            default:
                throw new NotSupportedException($"Unsupported inverse ADST length '{length}'.");
        }
    }

    private static void ApplyIdentity(int length, Span<int> c, int offset, int stride)
    {
        switch (length)
        {
            case 4:
                InverseIdentity4(c, offset, stride);
                break;
            case 8:
                InverseIdentity8(c, offset, stride);
                break;
            case 16:
                InverseIdentity16(c, offset, stride);
                break;
            case 32:
                InverseIdentity32(c, offset, stride);
                break;
            default:
                throw new NotSupportedException($"Unsupported inverse identity length '{length}'.");
        }
    }
}
