// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Derives a block's local warped-motion model from its already-decoded neighbours: the edge scan for
/// same-reference single-reference neighbours (dav1d <c>find_matching_ref</c>, whose non-empty result
/// also gates the WARP motion-mode syntax) and the least-squares model fit over their motion vectors
/// (dav1d <c>derive_warpmv</c>).
/// </summary>
internal static class Av1WarpDerivation
{
    /// <summary>
    /// Scans the block's top and left edges (plus the top-left and top-right corners) for neighbours
    /// predicted from the same single reference, recording their 4x4 offsets as bit masks: bits 0-31 of
    /// <paramref name="masks"/>[0] are top-edge offsets and bit 32 the top-right corner; bits 0-31 of
    /// <paramref name="masks"/>[1] are left-edge offsets and bit 32 the top-left corner. The scan stops
    /// after eight matches.
    /// </summary>
    /// <param name="grid">The motion-vector reference grid.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="bw4">The block width in 4x4 units.</param>
    /// <param name="bh4">The block height in 4x4 units.</param>
    /// <param name="w4">The block width clamped to the frame, in 4x4 units.</param>
    /// <param name="h4">The block height clamped to the frame, in 4x4 units.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="topRightAvailable">Whether the top-right neighbour is available (the I444
    /// top-has-right edge flag).</param>
    /// <param name="columnEnd">The tile column end in 4x4 units.</param>
    /// <param name="reference">The block's zero-based reference.</param>
    /// <param name="masks">Receives the two neighbour masks.</param>
    public static void FindMatchingRef(
        Av1MotionVectorGrid grid,
        int bx4,
        int by4,
        int bw4,
        int bh4,
        int w4,
        int h4,
        bool haveLeft,
        bool haveTop,
        bool topRightAvailable,
        int columnEnd,
        int reference,
        Span<ulong> masks)
    {
        int count = 0;
        bool haveTopLeft = haveTop && haveLeft;
        bool haveTopRight = Math.Max(bw4, bh4) < 32 && haveTop && bx4 + bw4 < columnEnd && topRightAvailable;

        if (haveTop)
        {
            Av1RefMvsBlock above = grid[by4 - 1, bx4];
            if (Matches(above, reference))
            {
                masks[0] |= 1;
                count = 1;
            }

            int aw4 = above.BlockSize.GetWidth4();
            if (aw4 >= bw4)
            {
                int off = bx4 & (aw4 - 1);
                if (off != 0)
                {
                    haveTopLeft = false;
                }

                if (aw4 - off > bw4)
                {
                    haveTopRight = false;
                }
            }
            else
            {
                ulong mask = 1UL << aw4;
                for (int x = aw4; x < w4; x += aw4)
                {
                    above = grid[by4 - 1, bx4 + x];
                    if (Matches(above, reference))
                    {
                        masks[0] |= mask;
                        if (++count >= 8)
                        {
                            return;
                        }
                    }

                    aw4 = above.BlockSize.GetWidth4();
                    mask <<= aw4;
                }
            }
        }

        if (haveLeft)
        {
            Av1RefMvsBlock left = grid[by4, bx4 - 1];
            if (Matches(left, reference))
            {
                masks[1] |= 1;
                if (++count >= 8)
                {
                    return;
                }
            }

            int lh4 = left.BlockSize.GetHeight4();
            if (lh4 >= bh4)
            {
                if ((by4 & (lh4 - 1)) != 0)
                {
                    haveTopLeft = false;
                }
            }
            else
            {
                ulong mask = 1UL << lh4;
                for (int y = lh4; y < h4; y += lh4)
                {
                    left = grid[by4 + y, bx4 - 1];
                    if (Matches(left, reference))
                    {
                        masks[1] |= mask;
                        if (++count >= 8)
                        {
                            return;
                        }
                    }

                    lh4 = left.BlockSize.GetHeight4();
                    mask <<= lh4;
                }
            }
        }

        if (haveTopLeft && Matches(grid[by4 - 1, bx4 - 1], reference))
        {
            masks[1] |= 1UL << 32;
            if (++count >= 8)
            {
                return;
            }
        }

        if (haveTopRight && Matches(grid[by4 - 1, bx4 + bw4], reference))
        {
            masks[0] |= 1UL << 32;
        }
    }

    /// <summary>
    /// Fits a local affine warp model to the matching neighbours found by
    /// <see cref="FindMatchingRef"/>: each neighbour contributes its centre and motion vector as a
    /// projected sample, samples whose vector differs too much from the block's own are discarded, and
    /// the model is solved by <see cref="Prediction.Av1WarpedMotion.FindAffineInt"/> and validated by
    /// the shear limits.
    /// </summary>
    /// <param name="grid">The motion-vector reference grid.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="bw4">The block width in 4x4 units.</param>
    /// <param name="bh4">The block height in 4x4 units.</param>
    /// <param name="masks">The neighbour masks from <see cref="FindMatchingRef"/>.</param>
    /// <param name="mv">The block's motion vector.</param>
    /// <param name="matrix">Receives the six-entry warp matrix on success.</param>
    /// <param name="shear">Receives the derived shear parameters on success.</param>
    /// <returns><see langword="true"/> when the derived model is affine and warpable; otherwise the
    /// block falls back to translational prediction.</returns>
    public static bool TryDeriveWarpMv(
        Av1MotionVectorGrid grid,
        int bx4,
        int by4,
        int bw4,
        int bh4,
        ReadOnlySpan<ulong> masks,
        Av1MotionVector mv,
        Span<int> matrix,
        Span<short> shear)
    {
        Span<int> pts = stackalloc int[8 * 4];
        int np = 0;

        void AddSample(Span<int> pts, ref int np, int dx, int dy, int sx, int sy, in Av1RefMvsBlock neighbour)
        {
            pts[np * 4] = (16 * ((2 * dx) + (sx * neighbour.BlockSize.GetWidth4()))) - 8;
            pts[(np * 4) + 1] = (16 * ((2 * dy) + (sy * neighbour.BlockSize.GetHeight4()))) - 8;
            pts[(np * 4) + 2] = pts[np * 4] + neighbour.MotionVector0.X;
            pts[(np * 4) + 3] = pts[(np * 4) + 1] + neighbour.MotionVector0.Y;
            np++;
        }

        // Gather the projectable motion vectors along the edges. A single top match at offset zero
        // (with no top-left corner match) samples the covering above block at its own origin, which
        // may extend left of this block.
        if ((uint)masks[0] == 1 && (masks[1] >> 32) == 0)
        {
            Av1RefMvsBlock above = grid[by4 - 1, bx4];
            int off = bx4 & (above.BlockSize.GetWidth4() - 1);
            AddSample(pts, ref np, -off, 0, 1, -1, above);
        }
        else
        {
            for (uint off = 0, xmask = (uint)masks[0]; np < 8 && xmask != 0;)
            {
                int tz = BitOperations.TrailingZeroCount(xmask);
                off += (uint)tz;
                xmask >>= tz;
                AddSample(pts, ref np, (int)off, 0, 1, -1, grid[by4 - 1, bx4 + (int)off]);
                xmask &= ~1u;
            }
        }

        if (np < 8 && masks[1] == 1)
        {
            Av1RefMvsBlock left = grid[by4, bx4 - 1];
            int off = by4 & (left.BlockSize.GetHeight4() - 1);
            AddSample(pts, ref np, 0, -off, -1, 1, grid[by4 - off, bx4 - 1]);
        }
        else
        {
            for (uint off = 0, ymask = (uint)masks[1]; np < 8 && ymask != 0;)
            {
                int tz = BitOperations.TrailingZeroCount(ymask);
                off += (uint)tz;
                ymask >>= tz;
                AddSample(pts, ref np, 0, (int)off, -1, 1, grid[by4 + (int)off, bx4 - 1]);
                ymask &= ~1u;
            }
        }

        if (np < 8 && (masks[1] >> 32) != 0)
        {
            AddSample(pts, ref np, 0, 0, -1, -1, grid[by4 - 1, bx4 - 1]);
        }

        if (np < 8 && (masks[0] >> 32) != 0)
        {
            AddSample(pts, ref np, bw4, 0, 1, -1, grid[by4 - 1, bx4 + bw4]);
        }

        // Select the samples whose motion-vector difference against the block's own vector is within
        // the size-dependent threshold, compacting the retained samples to the front.
        Span<int> mvd = stackalloc int[8];
        int ret = 0;
        int thresh = 4 * Math.Clamp(Math.Max(bw4, bh4), 4, 28);
        for (int i = 0; i < np; i++)
        {
            mvd[i] = Math.Abs(pts[(i * 4) + 2] - pts[i * 4] - mv.X) +
                     Math.Abs(pts[(i * 4) + 3] - pts[(i * 4) + 1] - mv.Y);
            if (mvd[i] > thresh)
            {
                mvd[i] = -1;
            }
            else
            {
                ret++;
            }
        }

        if (ret == 0)
        {
            ret = 1;
        }
        else
        {
            for (int i = 0, j = np - 1, k = 0; k < np - ret; k++, i++, j--)
            {
                while (mvd[i] != -1)
                {
                    i++;
                }

                while (mvd[j] == -1)
                {
                    j--;
                }

                if (i > j)
                {
                    break;
                }

                // Replace the discarded sample with the last retained one.
                mvd[i] = mvd[j];
                pts.Slice(j * 4, 4).CopyTo(pts.Slice(i * 4, 4));
            }
        }

        return Prediction.Av1WarpedMotion.FindAffineInt(pts, ret, bw4, bh4, mv, matrix, bx4, by4)
            && !Prediction.Av1WarpedMotion.TryGetShearParams(matrix, shear);
    }

    // A neighbour matches when it is single-reference inter from the same reference
    // (one-based in the grid).
    private static bool Matches(in Av1RefMvsBlock neighbour, int reference)
        => neighbour.Reference0 == reference + 1 && neighbour.Reference1 == -1;
}
