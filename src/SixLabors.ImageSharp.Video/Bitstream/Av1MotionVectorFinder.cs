// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Assembles the single-reference motion-vector candidate list for a block by scanning the neighbouring
/// cells of the reference grid, a port of the spatial portion of the reference decoder's
/// <c>dav1d_refmvs_find</c>. Temporal candidates (the <c>use_ref_frame_mvs</c> path) and compound
/// references are not handled here; those frames must disable temporal motion-vector prediction.
/// </summary>
internal static class Av1MotionVectorFinder
{
    private const uint NotScanned = ~0U;

    /// <summary>
    /// Finds the single-reference candidate list and mode context for a block.
    /// </summary>
    /// <param name="grid">The frame-wide reference grid.</param>
    /// <param name="stack">The candidate list to populate (must be freshly created).</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="referenceFrame">The one-based reference frame index being predicted.</param>
    /// <param name="bounds">The tile bounds in 4x4 units.</param>
    /// <param name="topRightAvailable">Whether the top-right neighbour is available.</param>
    /// <param name="imageWidth4">The frame width in 4x4 units (for clamping).</param>
    /// <param name="imageHeight4">The frame height in 4x4 units (for clamping).</param>
    /// <param name="globalMv">The reference's global-motion (predictor fill) vector.</param>
    /// <param name="globalMvSubstitution">Whether neighbours using global motion substitute it.</param>
    /// <param name="signBias">The per-reference sign bias, indexed by zero-based reference.</param>
    /// <returns>The candidate count and the composed single-reference mode context.</returns>
    public static (int CandidateCount, int Context) Find(
        Av1MotionVectorGrid grid,
        Av1MotionVectorStack stack,
        int bx4,
        int by4,
        Av1BlockSize blockSize,
        int referenceFrame,
        Av1TileBounds bounds,
        bool topRightAvailable,
        int imageWidth4,
        int imageHeight4,
        Av1MotionVector globalMv,
        bool globalMvSubstitution,
        ReadOnlySpan<int> signBias)
    {
        int bw4 = blockSize.GetWidth4();
        int bh4 = blockSize.GetHeight4();
        int w4 = Math.Min(Math.Min(bw4, 16), bounds.ColumnEnd - bx4);
        int h4 = Math.Min(Math.Min(bh4, 16), bounds.RowEnd - by4);

        bool haveNewMv = false;
        bool haveRowMvs = false;
        bool haveColMvs = false;
        bool dummyNewMv = false;

        // Direct top row.
        uint nRows = NotScanned;
        int maxRows = 0;
        if (by4 > bounds.RowStart)
        {
            maxRows = Math.Min((by4 - bounds.RowStart + 1) >> 1, 2 + (bh4 > 1 ? 1 : 0));
            nRows = (uint)stack.ScanRow(
                grid.Row(by4 - 1, bx4), bw4, w4, maxRows, bw4 >= 16 ? 4 : 1,
                referenceFrame, globalMv, globalMvSubstitution, ref haveNewMv, ref haveRowMvs);
        }

        // Direct left column.
        uint nCols = NotScanned;
        int maxCols = 0;
        if (bx4 > bounds.ColumnStart)
        {
            maxCols = Math.Min((bx4 - bounds.ColumnStart + 1) >> 1, 2 + (bw4 > 1 ? 1 : 0));
            nCols = (uint)stack.ScanColumn(
                GatherColumn(grid, by4, bx4 - 1, h4), bh4, h4, maxCols, bh4 >= 16 ? 4 : 1,
                referenceFrame, globalMv, globalMvSubstitution, ref haveNewMv, ref haveColMvs);
        }

        // Top-right.
        if (nRows != NotScanned && topRightAvailable && Math.Max(bw4, bh4) <= 16 && bw4 + bx4 < bounds.ColumnEnd)
        {
            stack.AddSpatialCandidate(
                grid[by4 - 1, bx4 + bw4], 4, referenceFrame, globalMv, globalMvSubstitution, ref haveNewMv, ref haveRowMvs);
        }

        int nearestMatch = (haveColMvs ? 1 : 0) + (haveRowMvs ? 1 : 0);
        int nearestCount = stack.Count;
        stack.ApplyNearestWeightBonus(nearestCount);

        // Temporal candidates are not supported; globalmv_ctx is therefore zero.
        int globalMvContext = 0;

        // Top-left (reached only when both the top and left edges were scanned).
        if ((nRows | nCols) != NotScanned)
        {
            stack.AddSpatialCandidate(
                grid[by4 - 1, bx4 - 1], 4, referenceFrame, globalMv, globalMvSubstitution, ref dummyNewMv, ref haveRowMvs);
        }

        // Secondary (non-direct) top and left edges, in 8x8 resolution.
        for (int n = 2; n <= 3; n++)
        {
            if ((uint)n > nRows && (uint)n <= (uint)maxRows)
            {
                int secRow = (by4 - (2 * n) + 1) | 1;
                nRows += (uint)stack.ScanRow(
                    grid.Row(secRow, bx4 | 1), bw4, w4, 1 + maxRows - n,
                    bw4 >= 16 ? 4 : 2, referenceFrame, globalMv, globalMvSubstitution, ref dummyNewMv, ref haveRowMvs);
            }

            if ((uint)n > nCols && (uint)n <= (uint)maxCols)
            {
                int secCol = (bx4 - (n * 2) + 1) | 1;
                nCols += (uint)stack.ScanColumn(
                    GatherColumn(grid, by4 | 1, secCol, h4), bh4, h4, 1 + maxCols - n,
                    bh4 >= 16 ? 4 : 2, referenceFrame, globalMv, globalMvSubstitution, ref dummyNewMv, ref haveColMvs);
            }
        }

        int referenceMatchCount = (haveColMvs ? 1 : 0) + (haveRowMvs ? 1 : 0);
        (int newMvContext, int refMvContext) = Av1MotionVectorStack.DeriveContexts(nearestMatch, referenceMatchCount, haveNewMv);

        stack.Sort(nearestCount);

        // Single-reference extended candidates to fill towards two entries.
        if (stack.Count < 2 && referenceFrame > 0)
        {
            int sign = signBias[referenceFrame - 1];
            int sz4 = Math.Min(w4, h4);
            if (nRows != NotScanned)
            {
                for (int x = 0; x < sz4 && stack.Count < 2;)
                {
                    Av1RefMvsBlock candidate = grid[by4 - 1, bx4 + x];
                    stack.AddSingleExtendedCandidate(candidate, sign, signBias);
                    x += candidate.BlockSize.GetWidth4();
                }
            }

            if (nCols != NotScanned)
            {
                for (int y = 0; y < sz4 && stack.Count < 2;)
                {
                    Av1RefMvsBlock candidate = grid[by4 + y, bx4 - 1];
                    stack.AddSingleExtendedCandidate(candidate, sign, signBias);
                    y += candidate.BlockSize.GetHeight4();
                }
            }
        }

        stack.Clamp(bx4, bw4, by4, bh4, imageWidth4, imageHeight4);
        stack.FillGlobalPredictors(globalMv);

        int context = Av1MotionVectorStack.ComposeContext(refMvContext, globalMvContext, newMvContext);
        return (stack.Count, context);
    }

    private static Av1RefMvsBlock[] GatherColumn(Av1MotionVectorGrid grid, int startRow, int column, int length)
    {
        Av1RefMvsBlock[] result = new Av1RefMvsBlock[length];
        for (int y = 0; y < length; y++)
        {
            int row = startRow + y;
            if (row >= 0 && row < grid.Rows4 && column >= 0 && column < grid.Columns4)
            {
                result[y] = grid[row, column];
            }
        }

        return result;
    }
}
