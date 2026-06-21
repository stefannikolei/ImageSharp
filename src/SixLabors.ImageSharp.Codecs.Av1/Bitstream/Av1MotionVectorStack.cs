// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Builds the single-reference dynamic reference (motion-vector candidate) list, a port of the
/// reference decoder's <c>add_spatial_candidate</c> merge and the inter-mode context derivation used
/// by <c>dav1d_refmvs_find</c>. Candidates are merged by motion-vector equality with accumulated
/// weights; the resulting nearest- and reference-match counts drive the new-mv / ref-mv contexts.
/// </summary>
internal sealed class Av1MotionVectorStack
{
    private const int MaxCandidates = 8;

    private readonly int[] mvY = new int[MaxCandidates];
    private readonly int[] mvX = new int[MaxCandidates];
    private readonly int[] weight = new int[MaxCandidates];

    /// <summary>Gets the number of candidates currently in the list.</summary>
    public int Count { get; private set; }

    /// <summary>Gets a value indicating whether a matching neighbour coded a new motion vector.</summary>
    public bool HaveNewMv { get; private set; }

    /// <summary>Gets a value indicating whether a neighbour matched the reference frame.</summary>
    public bool HaveReferenceMv { get; private set; }

    /// <summary>Gets the candidate at the given index.</summary>
    /// <param name="index">The candidate index.</param>
    /// <returns>The candidate motion vector and weight.</returns>
    public Av1MotionVectorCandidate this[int index]
        => new(new Av1MotionVector(this.mvY[index], this.mvX[index]), this.weight[index]);

    /// <summary>
    /// Adds a single-reference spatial neighbour to the candidate list, merging by motion-vector
    /// equality and accumulating the weight, matching <c>add_spatial_candidate</c>.
    /// </summary>
    /// <param name="block">The neighbour block.</param>
    /// <param name="candidateWeight">The candidate weight contribution.</param>
    /// <param name="referenceFrame">The one-based reference frame index being predicted.</param>
    /// <param name="globalMv">The global-motion vector for the reference.</param>
    /// <param name="globalMvValid">Whether the global-motion vector is valid (non-translation).</param>
    public void AddSpatialCandidate(
        in Av1RefMvsBlock block,
        int candidateWeight,
        int referenceFrame,
        Av1MotionVector globalMv,
        bool globalMvValid)
    {
        if (block.IsIntra)
        {
            return;
        }

        for (int n = 0; n < 2; n++)
        {
            int neighbourReference = n == 0 ? block.Reference0 : block.Reference1;
            if (neighbourReference != referenceFrame)
            {
                continue;
            }

            Av1MotionVector candidate = (block.IsGlobalMv && globalMvValid)
                ? globalMv
                : (n == 0 ? block.MotionVector0 : block.MotionVector1);

            this.HaveReferenceMv = true;
            this.HaveNewMv |= block.IsNewMv;

            int last = this.Count;
            for (int m = 0; m < last; m++)
            {
                if (this.mvY[m] == candidate.Y && this.mvX[m] == candidate.X)
                {
                    this.weight[m] += candidateWeight;
                    return;
                }
            }

            if (last < MaxCandidates)
            {
                this.mvY[last] = candidate.Y;
                this.mvX[last] = candidate.X;
                this.weight[last] = candidateWeight;
                this.Count = last + 1;
            }

            return;
        }
    }

    /// <summary>
    /// Scans a neighbour row, adding spatial candidates with position-dependent weights, a port of
    /// <c>scan_row</c>. The supplied span starts at the block's column in 4x4 resolution.
    /// </summary>
    /// <param name="row">The neighbour row blocks, indexed from the block column.</param>
    /// <param name="bw4">The block width in 4x4 units.</param>
    /// <param name="w4">The clamped block width used for scanning.</param>
    /// <param name="maxRows">The maximum number of rows the weight may span.</param>
    /// <param name="step">The minimum scan step.</param>
    /// <param name="referenceFrame">The one-based reference frame index being predicted.</param>
    /// <param name="globalMv">The global-motion vector for the reference.</param>
    /// <param name="globalMvValid">Whether the global-motion vector is valid.</param>
    /// <returns>The number of rows the scan covered.</returns>
    public int ScanRow(
        ReadOnlySpan<Av1RefMvsBlock> row,
        int bw4,
        int w4,
        int maxRows,
        int step,
        int referenceFrame,
        Av1MotionVector globalMv,
        bool globalMvValid)
    {
        Av1RefMvsBlock candidate = row[0];
        int candidateWidth = candidate.BlockSize.GetWidth4();
        int len = Math.Max(step, Math.Min(bw4, candidateWidth));

        if (bw4 <= candidateWidth)
        {
            int weight = bw4 == 1 ? 2 : Math.Max(2, Math.Min(2 * maxRows, candidate.BlockSize.GetHeight4()));
            this.AddSpatialCandidate(candidate, len * weight, referenceFrame, globalMv, globalMvValid);
            return weight >> 1;
        }

        for (int x = 0;;)
        {
            this.AddSpatialCandidate(candidate, len * 2, referenceFrame, globalMv, globalMvValid);
            x += len;
            if (x >= w4)
            {
                return 1;
            }

            candidate = row[x];
            candidateWidth = candidate.BlockSize.GetWidth4();
            len = Math.Max(step, candidateWidth);
        }
    }

    /// <summary>
    /// Scans a neighbour column, adding spatial candidates with position-dependent weights, a port of
    /// <c>scan_col</c>. The supplied span starts at the block's row in 4x4 resolution.
    /// </summary>
    /// <param name="column">The neighbour column blocks, indexed from the block row.</param>
    /// <param name="bh4">The block height in 4x4 units.</param>
    /// <param name="h4">The clamped block height used for scanning.</param>
    /// <param name="maxColumns">The maximum number of columns the weight may span.</param>
    /// <param name="step">The minimum scan step.</param>
    /// <param name="referenceFrame">The one-based reference frame index being predicted.</param>
    /// <param name="globalMv">The global-motion vector for the reference.</param>
    /// <param name="globalMvValid">Whether the global-motion vector is valid.</param>
    /// <returns>The number of columns the scan covered.</returns>
    public int ScanColumn(
        ReadOnlySpan<Av1RefMvsBlock> column,
        int bh4,
        int h4,
        int maxColumns,
        int step,
        int referenceFrame,
        Av1MotionVector globalMv,
        bool globalMvValid)
    {
        Av1RefMvsBlock candidate = column[0];
        int candidateHeight = candidate.BlockSize.GetHeight4();
        int len = Math.Max(step, Math.Min(bh4, candidateHeight));

        if (bh4 <= candidateHeight)
        {
            int weight = bh4 == 1 ? 2 : Math.Max(2, Math.Min(2 * maxColumns, candidate.BlockSize.GetWidth4()));
            this.AddSpatialCandidate(candidate, len * weight, referenceFrame, globalMv, globalMvValid);
            return weight >> 1;
        }

        for (int y = 0;;)
        {
            this.AddSpatialCandidate(candidate, len * 2, referenceFrame, globalMv, globalMvValid);
            y += len;
            if (y >= h4)
            {
                return 1;
            }

            candidate = column[y];
            candidateHeight = candidate.BlockSize.GetHeight4();
            len = Math.Max(step, candidateHeight);
        }
    }

    /// <summary>
    /// Adds the sign-adjusted non-self references of a neighbour as minimal-weight extended candidates,
    /// matching <c>add_single_extended_candidate</c>. Used to fill the list towards two entries.
    /// </summary>
    /// <param name="block">The neighbour block.</param>
    /// <param name="sign">The sign bias of the reference frame being predicted.</param>
    /// <param name="signBias">The per-reference sign bias, indexed by zero-based reference.</param>
    public void AddSingleExtendedCandidate(in Av1RefMvsBlock block, int sign, ReadOnlySpan<int> signBias)
    {
        for (int n = 0; n < 2; n++)
        {
            int neighbourReference = n == 0 ? block.Reference0 : block.Reference1;
            if (neighbourReference <= 0)
            {
                break;
            }

            Av1MotionVector candidate = n == 0 ? block.MotionVector0 : block.MotionVector1;
            int candidateY = candidate.Y;
            int candidateX = candidate.X;
            if ((sign ^ signBias[neighbourReference - 1]) != 0)
            {
                candidateY = -candidateY;
                candidateX = -candidateX;
            }

            int last = this.Count;
            int m = 0;
            while (m < last && !(this.mvY[m] == candidateY && this.mvX[m] == candidateX))
            {
                m++;
            }

            if (m == last && last < MaxCandidates)
            {
                this.mvY[m] = candidateY;
                this.mvX[m] = candidateX;
                this.weight[m] = 2;
                this.Count = last + 1;
            }
        }
    }

    /// <summary>
    /// Fills the candidate slots below index two with the global-motion vector without changing the
    /// reported candidate count, matching the predictor fill at the end of the single-reference path.
    /// </summary>
    /// <param name="globalMv">The global-motion vector predictor.</param>
    public void FillGlobalPredictors(Av1MotionVector globalMv)
    {
        for (int n = this.Count; n < 2; n++)
        {
            this.mvY[n] = globalMv.Y;
            this.mvX[n] = globalMv.X;
        }
    }

    /// <summary>
    /// Clamps every candidate motion vector to the allowed range for the block position, matching the
    /// reference decoder's candidate clamping.
    /// </summary>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="bw4">The block width in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="bh4">The block height in 4x4 units.</param>
    /// <param name="imageWidth4">The frame width in 4x4 units.</param>
    /// <param name="imageHeight4">The frame height in 4x4 units.</param>
    public void Clamp(int bx4, int bw4, int by4, int bh4, int imageWidth4, int imageHeight4)
    {
        int left = -(bx4 + bw4 + 4) * 4 * 8;
        int right = (imageWidth4 - bx4 + 4) * 4 * 8;
        int top = -(by4 + bh4 + 4) * 4 * 8;
        int bottom = (imageHeight4 - by4 + 4) * 4 * 8;
        for (int n = 0; n < this.Count; n++)
        {
            this.mvX[n] = Math.Clamp(this.mvX[n], left, right);
            this.mvY[n] = Math.Clamp(this.mvY[n], top, bottom);
        }
    }

    /// <summary>
    /// Adds the nearest-neighbour weight bonus (640) to the first <paramref name="nearestCount"/>
    /// candidates, matching the weight bump applied before temporal candidates in the reference decoder.
    /// </summary>
    /// <param name="nearestCount">The number of direct-neighbour candidates.</param>
    public void ApplyNearestWeightBonus(int nearestCount)
    {
        for (int n = 0; n < nearestCount; n++)
        {
            this.weight[n] += 640;
        }
    }

    /// <summary>
    /// Sorts the first <paramref name="nearestCount"/> candidates and the remaining candidates each by
    /// descending weight using the reference decoder's stable bubble sort.
    /// </summary>
    /// <param name="nearestCount">The boundary between nearest and secondary candidates.</param>
    public void Sort(int nearestCount)
    {
        BubbleSort(this.mvY, this.mvX, this.weight, 0, nearestCount);
        BubbleSort(this.mvY, this.mvX, this.weight, nearestCount, this.Count);
    }

    /// <summary>
    /// Derives the new-mv and ref-mv contexts from the nearest- and reference-match counts, matching the
    /// context build-up in <c>dav1d_refmvs_find</c>.
    /// </summary>
    /// <param name="nearestMatch">The number of direct-neighbour matches (0-2).</param>
    /// <param name="referenceMatchCount">The total reference-match count.</param>
    /// <param name="haveNewMv">Whether a matching neighbour coded a new motion vector.</param>
    /// <returns>The new-mv and ref-mv contexts.</returns>
    public static (int NewMvContext, int RefMvContext) DeriveContexts(
        int nearestMatch,
        int referenceMatchCount,
        bool haveNewMv)
    {
        int newMvFlag = haveNewMv ? 1 : 0;
        return nearestMatch switch
        {
            0 => (referenceMatchCount > 0 ? 1 : 0, Math.Min(2, referenceMatchCount)),
            1 => (3 - newMvFlag, Math.Min(referenceMatchCount * 3, 4)),
            _ => (5 - newMvFlag, 5),
        };
    }

    /// <summary>
    /// Composes the final single-reference mode context, matching
    /// <c>(refmv_ctx &lt;&lt; 4) | (globalmv_ctx &lt;&lt; 3) | newmv_ctx</c>.
    /// </summary>
    /// <param name="refMvContext">The ref-mv context.</param>
    /// <param name="globalMvContext">The global-mv context.</param>
    /// <param name="newMvContext">The new-mv context.</param>
    /// <returns>The composed mode context.</returns>
    public static int ComposeContext(int refMvContext, int globalMvContext, int newMvContext)
        => (refMvContext << 4) | (globalMvContext << 3) | newMvContext;

    private static void BubbleSort(int[] y, int[] x, int[] w, int start, int end)
    {
        int len = end - start;
        while (len > 0)
        {
            int last = 0;
            for (int n = 1; n < len; n++)
            {
                if (w[start + n - 1] < w[start + n])
                {
                    (y[start + n - 1], y[start + n]) = (y[start + n], y[start + n - 1]);
                    (x[start + n - 1], x[start + n]) = (x[start + n], x[start + n - 1]);
                    (w[start + n - 1], w[start + n]) = (w[start + n], w[start + n - 1]);
                    last = n;
                }
            }

            len = last;
        }
    }
}
