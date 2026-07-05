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
    /// <param name="temporal">The temporal motion-vector prediction state, or <see langword="null"/>.</param>
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
        ReadOnlySpan<int> signBias,
        Av1TemporalMvContext? temporal = null)
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

        // Temporal candidates: sample the projected motion field over the block (plus corner positions
        // for mid-sized blocks) and add each valid cell as a weight-2 candidate. The global-mv context
        // starts at the header's use_ref_frame_mvs flag and is replaced by the first cell's distance to
        // the global-motion vector.
        int globalMvContext = temporal is not null ? 1 : 0;
        if (temporal?.Projected is { } projected)
        {
            int stride8 = temporal.Stride8;
            int bx8 = bx4 >> 1;
            int by8 = by4 >> 1;
            int stepH = bw4 >= 16 ? 2 : 1;
            int stepV = bh4 >= 16 ? 2 : 1;
            int w8 = Math.Min((w4 + 1) >> 1, 8);
            int h8 = Math.Min((h4 + 1) >> 1, 8);
            for (int y = 0; y < h8; y += stepV)
            {
                for (int x = 0; x < w8; x += stepH)
                {
                    AddTemporalCandidate(temporal, stack, projected[((by8 + y) * stride8) + bx8 + x], referenceFrame, (x | y) == 0, ref globalMvContext, globalMv);
                }
            }

            if (Math.Min(bw4, bh4) >= 2 && Math.Max(bw4, bh4) < 16)
            {
                int bw8 = bw4 >> 1;
                int bh8 = bh4 >> 1;
                int rowEnd8 = Math.Min(bounds.RowEnd >> 1, (by8 & ~7) + 8);
                int colEnd8 = Math.Min(bounds.ColumnEnd >> 1, (bx8 & ~7) + 8);
                bool hasBottom = by8 + bh8 < rowEnd8;
                if (hasBottom && bx8 - 1 >= Math.Max(bounds.ColumnStart >> 1, bx8 & ~7))
                {
                    AddTemporalCandidate(temporal, stack, projected[((by8 + bh8) * stride8) + bx8 - 1], referenceFrame, false, ref globalMvContext, globalMv);
                }

                if (bx8 + bw8 < colEnd8)
                {
                    if (hasBottom)
                    {
                        AddTemporalCandidate(temporal, stack, projected[((by8 + bh8) * stride8) + bx8 + bw8], referenceFrame, false, ref globalMvContext, globalMv);
                    }

                    if (by8 + bh8 - 1 < rowEnd8)
                    {
                        AddTemporalCandidate(temporal, stack, projected[((by8 + bh8 - 1) * stride8) + bx8 + bw8], referenceFrame, false, ref globalMvContext, globalMv);
                    }
                }
            }
        }

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

    // Adds one projected motion-field cell as a temporal candidate, a port of add_temporal_candidate:
    // the cell's motion vector is scaled by the current frame's distance to the block's reference over
    // the cell's own stored distance, precision-fixed, and merged into the stack with weight 2.
    private static void AddTemporalCandidate(Av1TemporalMvContext temporal, Av1MotionVectorStack stack, in Av1TemporalMvBlock cell, int referenceFrame, bool isFirst, ref int globalMvContext, Av1MotionVector globalMv)
    {
        if (cell.Reference == 0)
        {
            return;
        }

        Av1MotionVector mv = Av1MotionVectorProjection.Project(cell.Mv, temporal.PocDiff[referenceFrame - 1], cell.Reference);
        mv = Av1MotionVectorPrecision.Fix(mv, temporal.AllowHighPrecisionMv, temporal.ForceIntegerMv);

        if (isFirst)
        {
            globalMvContext = (Math.Abs(mv.X - globalMv.X) | Math.Abs(mv.Y - globalMv.Y)) >= 16 ? 1 : 0;
        }

        stack.AddTemporalCandidate(mv);
    }

    /// <summary>
    /// Builds the compound (two-reference) motion-vector candidate list, the pair half of
    /// <c>dav1d_refmvs_find</c>: the same edge scans as the single-reference path but matching the
    /// reference PAIR, followed by the compound extended-candidate fill towards two entries,
    /// unconditional clamping and the compound mode-context composition.
    /// </summary>
    /// <param name="grid">The motion-vector reference grid.</param>
    /// <param name="stack">The compound candidate stack to fill.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="referenceFrame0">The one-based first reference.</param>
    /// <param name="referenceFrame1">The one-based second reference.</param>
    /// <param name="bounds">The tile bounds in 4x4 units.</param>
    /// <param name="topRightAvailable">Whether the top-right neighbour is available.</param>
    /// <param name="imageWidth4">The frame width in 4x4 units.</param>
    /// <param name="imageHeight4">The frame height in 4x4 units.</param>
    /// <param name="globalMv0">The first reference's block-centre global vector.</param>
    /// <param name="globalMv0Substitution">Whether neighbours using global motion substitute it (first).</param>
    /// <param name="globalMv1">The second reference's block-centre global vector.</param>
    /// <param name="globalMv1Substitution">Whether neighbours using global motion substitute it (second).</param>
    /// <param name="signBias">The per-reference sign bias, indexed by zero-based reference.</param>
    /// <param name="temporal">The temporal motion-vector prediction state, or <see langword="null"/>.</param>
    /// <returns>The candidate count and the composed compound mode context.</returns>
    public static (int CandidateCount, int Context) FindCompound(
        Av1MotionVectorGrid grid,
        Av1CompoundMotionVectorStack stack,
        int bx4,
        int by4,
        Av1BlockSize blockSize,
        int referenceFrame0,
        int referenceFrame1,
        Av1TileBounds bounds,
        bool topRightAvailable,
        int imageWidth4,
        int imageHeight4,
        Av1MotionVector globalMv0,
        bool globalMv0Substitution,
        Av1MotionVector globalMv1,
        bool globalMv1Substitution,
        ReadOnlySpan<int> signBias,
        Av1TemporalMvContext? temporal = null)
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
                referenceFrame0, referenceFrame1, globalMv0, globalMv0Substitution, globalMv1, globalMv1Substitution,
                ref haveNewMv, ref haveRowMvs);
        }

        // Direct left column.
        uint nCols = NotScanned;
        int maxCols = 0;
        if (bx4 > bounds.ColumnStart)
        {
            maxCols = Math.Min((bx4 - bounds.ColumnStart + 1) >> 1, 2 + (bw4 > 1 ? 1 : 0));
            nCols = (uint)stack.ScanColumn(
                GatherColumn(grid, by4, bx4 - 1, h4), bh4, h4, maxCols, bh4 >= 16 ? 4 : 1,
                referenceFrame0, referenceFrame1, globalMv0, globalMv0Substitution, globalMv1, globalMv1Substitution,
                ref haveNewMv, ref haveColMvs);
        }

        // Top-right.
        if (nRows != NotScanned && topRightAvailable && Math.Max(bw4, bh4) <= 16 && bw4 + bx4 < bounds.ColumnEnd)
        {
            stack.AddSpatialCandidate(
                grid[by4 - 1, bx4 + bw4], 4, referenceFrame0, referenceFrame1,
                globalMv0, globalMv0Substitution, globalMv1, globalMv1Substitution, ref haveNewMv, ref haveRowMvs);
        }

        int nearestMatch = (haveColMvs ? 1 : 0) + (haveRowMvs ? 1 : 0);
        int nearestCount = stack.Count;
        stack.ApplyNearestWeightBonus(nearestCount);

        // Temporal pair candidates.
        if (temporal?.Projected is { } projected)
        {
            int stride8 = temporal.Stride8;
            int bx8 = bx4 >> 1;
            int by8 = by4 >> 1;
            int stepH = bw4 >= 16 ? 2 : 1;
            int stepV = bh4 >= 16 ? 2 : 1;
            int w8 = Math.Min((w4 + 1) >> 1, 8);
            int h8 = Math.Min((h4 + 1) >> 1, 8);
            for (int y = 0; y < h8; y += stepV)
            {
                for (int x = 0; x < w8; x += stepH)
                {
                    AddCompoundTemporalCandidate(temporal, stack, projected[(((by8 + y) & 15) * stride8) + bx8 + x], referenceFrame0, referenceFrame1);
                }
            }

            if (Math.Min(bw4, bh4) >= 2 && Math.Max(bw4, bh4) < 16)
            {
                int bh8 = bh4 >> 1;
                int bw8 = bw4 >> 1;
                int rowBase = ((by8 + bh8) & 15) * stride8;
                bool hasBottom = by8 + bh8 < Math.Min(bounds.RowEnd >> 1, (by8 & ~7) + 8);
                if (hasBottom && bx8 - 1 >= Math.Max(bounds.ColumnStart >> 1, bx8 & ~7))
                {
                    AddCompoundTemporalCandidate(temporal, stack, projected[rowBase + bx8 - 1], referenceFrame0, referenceFrame1);
                }

                if (bx8 + bw8 < Math.Min(bounds.ColumnEnd >> 1, (bx8 & ~7) + 8))
                {
                    if (hasBottom)
                    {
                        AddCompoundTemporalCandidate(temporal, stack, projected[rowBase + bx8 + bw8], referenceFrame0, referenceFrame1);
                    }

                    if (by8 + bh8 - 1 < Math.Min(bounds.RowEnd >> 1, (by8 & ~7) + 8))
                    {
                        AddCompoundTemporalCandidate(temporal, stack, projected[(((by8 + bh8 - 1) & 15) * stride8) + bx8 + bw8], referenceFrame0, referenceFrame1);
                    }
                }
            }
        }

        // Top-left (reached only when both the top and left edges were scanned).
        if ((nRows | nCols) != NotScanned)
        {
            stack.AddSpatialCandidate(
                grid[by4 - 1, bx4 - 1], 4, referenceFrame0, referenceFrame1,
                globalMv0, globalMv0Substitution, globalMv1, globalMv1Substitution, ref dummyNewMv, ref haveRowMvs);
        }

        // Secondary (non-direct) top and left edges at 8x8 resolution.
        for (int n = 2; n <= 3; n++)
        {
            if ((uint)n > nRows && (uint)n <= (uint)maxRows)
            {
                int secRow = (by4 - (2 * n) + 1) | 1;
                nRows += (uint)stack.ScanRow(
                    grid.Row(secRow, bx4 | 1), bw4, w4, 1 + maxRows - n, bw4 >= 16 ? 4 : 2,
                    referenceFrame0, referenceFrame1, globalMv0, globalMv0Substitution, globalMv1, globalMv1Substitution,
                    ref dummyNewMv, ref haveRowMvs);
            }

            if ((uint)n > nCols && (uint)n <= (uint)maxCols)
            {
                int secCol = (bx4 - (n * 2) + 1) | 1;
                nCols += (uint)stack.ScanColumn(
                    GatherColumn(grid, by4 | 1, secCol, h4), bh4, h4, 1 + maxCols - n, bh4 >= 16 ? 4 : 2,
                    referenceFrame0, referenceFrame1, globalMv0, globalMv0Substitution, globalMv1, globalMv1Substitution,
                    ref dummyNewMv, ref haveColMvs);
            }
        }

        int referenceMatchCount = (haveColMvs ? 1 : 0) + (haveRowMvs ? 1 : 0);
        (int newMvContext, int refMvContext) = Av1MotionVectorStack.DeriveContexts(nearestMatch, referenceMatchCount, haveNewMv);

        stack.Sort(nearestCount);

        // Compound extended candidates to fill towards two entries.
        if (stack.Count < 2)
        {
            int sign0 = signBias[referenceFrame0 - 1];
            int sign1 = signBias[referenceFrame1 - 1];
            int sz4 = Math.Min(w4, h4);
            Span<Av1MotionVector> sameMv0 = stackalloc Av1MotionVector[2];
            Span<Av1MotionVector> sameMv1 = stackalloc Av1MotionVector[2];
            Span<Av1MotionVector> diffMv0 = stackalloc Av1MotionVector[2];
            Span<Av1MotionVector> diffMv1 = stackalloc Av1MotionVector[2];
            Span<int> sameCount = stackalloc int[2];
            Span<int> diffCount = stackalloc int[2];

            if (nRows != NotScanned)
            {
                for (int x = 0; x < sz4;)
                {
                    Av1RefMvsBlock candidate = grid[by4 - 1, bx4 + x];
                    AddCompoundExtendedCandidate(sameMv0, sameMv1, sameCount, diffMv0, diffMv1, diffCount, candidate, sign0, sign1, referenceFrame0, referenceFrame1, signBias);
                    x += candidate.BlockSize.GetWidth4();
                }
            }

            if (nCols != NotScanned)
            {
                for (int y = 0; y < sz4;)
                {
                    Av1RefMvsBlock candidate = grid[by4 + y, bx4 - 1];
                    AddCompoundExtendedCandidate(sameMv0, sameMv1, sameCount, diffMv0, diffMv1, diffCount, candidate, sign0, sign1, referenceFrame0, referenceFrame1, signBias);
                    y += candidate.BlockSize.GetHeight4();
                }
            }

            stack.FillCompoundExtended(sameMv0, sameMv1, sameCount, diffMv0, diffMv1, diffCount, globalMv0, globalMv1);
        }

        stack.Clamp(bx4, bw4, by4, bh4, imageWidth4, imageHeight4);

        int context = Av1CompoundMotionVectorStack.ComposeCompoundContext(refMvContext, newMvContext);
        return (stack.Count, context);
    }

    // The compound half of add_compound_extended_candidate: a neighbour's vectors feed each
    // component's same-reference list on an exact reference match, and the opposite (sign-adjusted)
    // different-reference list otherwise.
    private static void AddCompoundExtendedCandidate(
        Span<Av1MotionVector> sameMv0,
        Span<Av1MotionVector> sameMv1,
        Span<int> sameCount,
        Span<Av1MotionVector> diffMv0,
        Span<Av1MotionVector> diffMv1,
        Span<int> diffCount,
        in Av1RefMvsBlock candidate,
        int sign0,
        int sign1,
        int referenceFrame0,
        int referenceFrame1,
        ReadOnlySpan<int> signBias)
    {
        for (int n = 0; n < 2; n++)
        {
            int candidateReference = n == 0 ? candidate.Reference0 : candidate.Reference1;
            if (candidateReference <= 0)
            {
                break;
            }

            Av1MotionVector mv = n == 0 ? candidate.MotionVector0 : candidate.MotionVector1;
            if (candidateReference == referenceFrame0)
            {
                if (sameCount[0] < 2)
                {
                    sameMv0[sameCount[0]++] = mv;
                }

                if (diffCount[1] < 2)
                {
                    Av1MotionVector adjusted = (sign1 ^ signBias[candidateReference - 1]) != 0
                        ? new Av1MotionVector(-mv.Y, -mv.X)
                        : mv;
                    diffMv1[diffCount[1]++] = adjusted;
                }
            }
            else if (candidateReference == referenceFrame1)
            {
                if (sameCount[1] < 2)
                {
                    sameMv1[sameCount[1]++] = mv;
                }

                if (diffCount[0] < 2)
                {
                    Av1MotionVector adjusted = (sign0 ^ signBias[candidateReference - 1]) != 0
                        ? new Av1MotionVector(-mv.Y, -mv.X)
                        : mv;
                    diffMv0[diffCount[0]++] = adjusted;
                }
            }
            else
            {
                Av1MotionVector inverted = new(-mv.Y, -mv.X);
                if (diffCount[0] < 2)
                {
                    diffMv0[diffCount[0]++] = (sign0 ^ signBias[candidateReference - 1]) != 0 ? inverted : mv;
                }

                if (diffCount[1] < 2)
                {
                    diffMv1[diffCount[1]++] = (sign1 ^ signBias[candidateReference - 1]) != 0 ? inverted : mv;
                }
            }
        }
    }

    // Adds one projected motion-field cell as a compound temporal candidate: the cell's vector is
    // projected once per component by that reference's distance, precision-fixed, and merged as a pair.
    private static void AddCompoundTemporalCandidate(
        Av1TemporalMvContext temporal,
        Av1CompoundMotionVectorStack stack,
        in Av1TemporalMvBlock cell,
        int referenceFrame0,
        int referenceFrame1)
    {
        if (cell.Reference == 0)
        {
            return;
        }

        Av1MotionVector mv0 = Av1MotionVectorProjection.Project(cell.Mv, temporal.PocDiff[referenceFrame0 - 1], cell.Reference);
        mv0 = Av1MotionVectorPrecision.Fix(mv0, temporal.AllowHighPrecisionMv, temporal.ForceIntegerMv);
        Av1MotionVector mv1 = Av1MotionVectorProjection.Project(cell.Mv, temporal.PocDiff[referenceFrame1 - 1], cell.Reference);
        mv1 = Av1MotionVectorPrecision.Fix(mv1, temporal.AllowHighPrecisionMv, temporal.ForceIntegerMv);
        stack.AddTemporalCandidate(mv0, mv1);
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
