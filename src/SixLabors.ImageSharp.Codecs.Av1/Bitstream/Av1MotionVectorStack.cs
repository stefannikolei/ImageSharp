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
