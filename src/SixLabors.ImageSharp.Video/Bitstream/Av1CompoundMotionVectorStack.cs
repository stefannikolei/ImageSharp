// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Builds the compound (two-reference) dynamic reference list: motion-vector PAIRS merged by
/// pair equality with accumulated weights, a port of the compound half of the reference decoder's
/// <c>add_spatial_candidate</c> / <c>dav1d_refmvs_find</c>. A neighbour contributes only when its
/// reference pair matches the block's pair exactly.
/// </summary>
internal sealed class Av1CompoundMotionVectorStack
{
    private const int MaxCandidates = 8;

    private readonly int[] mv0Y = new int[MaxCandidates];
    private readonly int[] mv0X = new int[MaxCandidates];
    private readonly int[] mv1Y = new int[MaxCandidates];
    private readonly int[] mv1X = new int[MaxCandidates];
    private readonly int[] weight = new int[MaxCandidates];

    /// <summary>Gets the number of candidates currently in the list.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the first motion vector of the candidate at the given index.</summary>
    /// <param name="index">The candidate index.</param>
    /// <returns>The first motion vector.</returns>
    public Av1MotionVector Mv0(int index) => new(this.mv0Y[index], this.mv0X[index]);

    /// <summary>Gets the second motion vector of the candidate at the given index.</summary>
    /// <param name="index">The candidate index.</param>
    /// <returns>The second motion vector.</returns>
    public Av1MotionVector Mv1(int index) => new(this.mv1Y[index], this.mv1X[index]);

    /// <summary>Gets the weight of the candidate at the given index.</summary>
    /// <param name="index">The candidate index.</param>
    /// <returns>The weight.</returns>
    public int Weight(int index) => this.weight[index];

    /// <summary>
    /// Adds a compound spatial neighbour whose reference pair matches, merging by pair equality.
    /// </summary>
    /// <param name="block">The neighbour block.</param>
    /// <param name="candidateWeight">The candidate weight contribution.</param>
    /// <param name="reference0">The one-based first reference being predicted.</param>
    /// <param name="reference1">The one-based second reference being predicted.</param>
    /// <param name="globalMv0">The global-motion vector of the first reference.</param>
    /// <param name="globalMv0Valid">Whether the first global vector substitutes (non-translation model).</param>
    /// <param name="globalMv1">The global-motion vector of the second reference.</param>
    /// <param name="globalMv1Valid">Whether the second global vector substitutes.</param>
    /// <param name="haveNewMv">Set when a matching neighbour coded a new motion vector.</param>
    /// <param name="haveReferenceMv">Set when a neighbour matched the reference pair.</param>
    public void AddSpatialCandidate(
        in Av1RefMvsBlock block,
        int candidateWeight,
        int reference0,
        int reference1,
        Av1MotionVector globalMv0,
        bool globalMv0Valid,
        Av1MotionVector globalMv1,
        bool globalMv1Valid,
        ref bool haveNewMv,
        ref bool haveReferenceMv)
    {
        if (block.IsIntra)
        {
            return;
        }

        if (block.Reference0 != reference0 || block.Reference1 != reference1)
        {
            return;
        }

        Av1MotionVector candidate0 = (block.IsGlobalMv && globalMv0Valid) ? globalMv0 : block.MotionVector0;
        Av1MotionVector candidate1 = (block.IsGlobalMv && globalMv1Valid) ? globalMv1 : block.MotionVector1;

        haveReferenceMv = true;
        haveNewMv |= block.IsNewMv;

        int last = this.Count;
        for (int m = 0; m < last; m++)
        {
            if (this.mv0Y[m] == candidate0.Y && this.mv0X[m] == candidate0.X &&
                this.mv1Y[m] == candidate1.Y && this.mv1X[m] == candidate1.X)
            {
                this.weight[m] += candidateWeight;
                return;
            }
        }

        if (last < MaxCandidates)
        {
            this.Set(last, candidate0, candidate1, candidateWeight);
            this.Count = last + 1;
        }
    }

    /// <summary>Scans a neighbour row (compound variant of <c>scan_row</c>).</summary>
    /// <param name="row">The neighbour row blocks, indexed from the block column.</param>
    /// <param name="bw4">The block width in 4x4 units.</param>
    /// <param name="w4">The clamped block width used for scanning.</param>
    /// <param name="maxRows">The maximum number of rows the weight may span.</param>
    /// <param name="step">The minimum scan step.</param>
    /// <param name="reference0">The one-based first reference.</param>
    /// <param name="reference1">The one-based second reference.</param>
    /// <param name="globalMv0">The first reference's global vector.</param>
    /// <param name="globalMv0Valid">Whether it substitutes.</param>
    /// <param name="globalMv1">The second reference's global vector.</param>
    /// <param name="globalMv1Valid">Whether it substitutes.</param>
    /// <param name="haveNewMv">Set when a matching neighbour coded a new motion vector.</param>
    /// <param name="haveReferenceMv">Set when a neighbour matched.</param>
    /// <returns>The number of rows the scan covered.</returns>
    public int ScanRow(
        ReadOnlySpan<Av1RefMvsBlock> row,
        int bw4,
        int w4,
        int maxRows,
        int step,
        int reference0,
        int reference1,
        Av1MotionVector globalMv0,
        bool globalMv0Valid,
        Av1MotionVector globalMv1,
        bool globalMv1Valid,
        ref bool haveNewMv,
        ref bool haveReferenceMv)
    {
        Av1RefMvsBlock candidate = row[0];
        int candidateWidth = candidate.BlockSize.GetWidth4();
        int len = Math.Max(step, Math.Min(bw4, candidateWidth));

        if (bw4 <= candidateWidth)
        {
            int rowWeight = bw4 == 1 ? 2 : Math.Max(2, Math.Min(2 * maxRows, candidate.BlockSize.GetHeight4()));
            this.AddSpatialCandidate(candidate, len * rowWeight, reference0, reference1, globalMv0, globalMv0Valid, globalMv1, globalMv1Valid, ref haveNewMv, ref haveReferenceMv);
            return rowWeight >> 1;
        }

        for (int x = 0;;)
        {
            this.AddSpatialCandidate(candidate, len * 2, reference0, reference1, globalMv0, globalMv0Valid, globalMv1, globalMv1Valid, ref haveNewMv, ref haveReferenceMv);
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

    /// <summary>Scans a neighbour column (compound variant of <c>scan_col</c>).</summary>
    /// <param name="column">The neighbour column blocks, indexed from the block row.</param>
    /// <param name="bh4">The block height in 4x4 units.</param>
    /// <param name="h4">The clamped block height used for scanning.</param>
    /// <param name="maxColumns">The maximum number of columns the weight may span.</param>
    /// <param name="step">The minimum scan step.</param>
    /// <param name="reference0">The one-based first reference.</param>
    /// <param name="reference1">The one-based second reference.</param>
    /// <param name="globalMv0">The first reference's global vector.</param>
    /// <param name="globalMv0Valid">Whether it substitutes.</param>
    /// <param name="globalMv1">The second reference's global vector.</param>
    /// <param name="globalMv1Valid">Whether it substitutes.</param>
    /// <param name="haveNewMv">Set when a matching neighbour coded a new motion vector.</param>
    /// <param name="haveReferenceMv">Set when a neighbour matched.</param>
    /// <returns>The number of columns the scan covered.</returns>
    public int ScanColumn(
        ReadOnlySpan<Av1RefMvsBlock> column,
        int bh4,
        int h4,
        int maxColumns,
        int step,
        int reference0,
        int reference1,
        Av1MotionVector globalMv0,
        bool globalMv0Valid,
        Av1MotionVector globalMv1,
        bool globalMv1Valid,
        ref bool haveNewMv,
        ref bool haveReferenceMv)
    {
        Av1RefMvsBlock candidate = column[0];
        int candidateHeight = candidate.BlockSize.GetHeight4();
        int len = Math.Max(step, Math.Min(bh4, candidateHeight));

        if (bh4 <= candidateHeight)
        {
            int columnWeight = bh4 == 1 ? 2 : Math.Max(2, Math.Min(2 * maxColumns, candidate.BlockSize.GetWidth4()));
            this.AddSpatialCandidate(candidate, len * columnWeight, reference0, reference1, globalMv0, globalMv0Valid, globalMv1, globalMv1Valid, ref haveNewMv, ref haveReferenceMv);
            return columnWeight >> 1;
        }

        for (int y = 0;;)
        {
            this.AddSpatialCandidate(candidate, len * 2, reference0, reference1, globalMv0, globalMv0Valid, globalMv1, globalMv1Valid, ref haveNewMv, ref haveReferenceMv);
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

    /// <summary>Adds a projected temporal motion-vector pair, merging by pair equality with weight 2.</summary>
    /// <param name="motionVector0">The first projected vector.</param>
    /// <param name="motionVector1">The second projected vector.</param>
    public void AddTemporalCandidate(Av1MotionVector motionVector0, Av1MotionVector motionVector1)
    {
        int last = this.Count;
        for (int m = 0; m < last; m++)
        {
            if (this.mv0Y[m] == motionVector0.Y && this.mv0X[m] == motionVector0.X &&
                this.mv1Y[m] == motionVector1.Y && this.mv1X[m] == motionVector1.X)
            {
                this.weight[m] += 2;
                return;
            }
        }

        if (last < MaxCandidates)
        {
            this.Set(last, motionVector0, motionVector1, 2);
            this.Count = last + 1;
        }
    }

    /// <summary>Adds the nearest-candidate weight bonus to the first candidates.</summary>
    /// <param name="nearestCount">The number of candidates found by the direct scans.</param>
    public void ApplyNearestWeightBonus(int nearestCount)
    {
        for (int n = 0; n < nearestCount; n++)
        {
            this.weight[n] += 640;
        }
    }

    /// <summary>Sorts the nearest and secondary candidate ranges by descending weight.</summary>
    /// <param name="nearestCount">The number of nearest candidates.</param>
    public void Sort(int nearestCount)
    {
        this.BubbleSort(0, nearestCount);
        this.BubbleSort(nearestCount, this.Count);
    }

    /// <summary>
    /// Fills the list to two entries from the compound extended candidates (the merge step of dav1d's
    /// compound extended-candidate fill): each component's missing vectors come first from the
    /// different-reference list, then from the block-centre global vector; if the single existing
    /// candidate equals the first extended pair, the second extended pair replaces it.
    /// </summary>
    /// <param name="sameMv0">The same-reference first-component vectors (two slots).</param>
    /// <param name="sameMv1">The same-reference second-component vectors (two slots).</param>
    /// <param name="sameCount">The per-component same-reference counts.</param>
    /// <param name="diffMv0">The different-reference first-component vectors (two slots).</param>
    /// <param name="diffMv1">The different-reference second-component vectors (two slots).</param>
    /// <param name="diffCount">The per-component different-reference counts.</param>
    /// <param name="globalMv0">The first reference's block-centre global vector.</param>
    /// <param name="globalMv1">The second reference's block-centre global vector.</param>
    public void FillCompoundExtended(
        Span<Av1MotionVector> sameMv0,
        Span<Av1MotionVector> sameMv1,
        Span<int> sameCount,
        Span<Av1MotionVector> diffMv0,
        Span<Av1MotionVector> diffMv1,
        Span<int> diffCount,
        Av1MotionVector globalMv0,
        Av1MotionVector globalMv1)
    {
        for (int n = 0; n < 2; n++)
        {
            Span<Av1MotionVector> same = n == 0 ? sameMv0 : sameMv1;
            Span<Av1MotionVector> diff = n == 0 ? diffMv0 : diffMv1;
            int m = sameCount[n];
            if (m >= 2)
            {
                continue;
            }

            int l = diffCount[n];
            if (l != 0)
            {
                same[m] = diff[0];
                if (++m == 2)
                {
                    continue;
                }

                if (l == 2)
                {
                    same[1] = diff[1];
                    continue;
                }
            }

            Av1MotionVector global = n == 0 ? globalMv0 : globalMv1;
            do
            {
                same[m] = global;
            }
            while (++m < 2);
        }

        int count = this.Count;
        if (count == 1 && this.mv0Y[0] == sameMv0[0].Y && this.mv0X[0] == sameMv0[0].X &&
            this.mv1Y[0] == sameMv1[0].Y && this.mv1X[0] == sameMv1[0].X)
        {
            this.Set(1, sameMv0[1], sameMv1[1], 2);
        }
        else
        {
            for (int n = count; n < 2; n++)
            {
                this.Set(n, sameMv0[n - count], sameMv1[n - count], 2);
            }
        }

        this.Count = 2;
    }

    /// <summary>Clamps every candidate pair to the motion-vector range around the block.</summary>
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
            this.mv0X[n] = Math.Clamp(this.mv0X[n], left, right);
            this.mv0Y[n] = Math.Clamp(this.mv0Y[n], top, bottom);
            this.mv1X[n] = Math.Clamp(this.mv1X[n], left, right);
            this.mv1Y[n] = Math.Clamp(this.mv1Y[n], top, bottom);
        }
    }

    /// <summary>Composes the compound inter-mode context from the new-mv and ref-mv contexts.</summary>
    /// <param name="refMvContext">The reference-mv context.</param>
    /// <param name="newMvContext">The new-mv context.</param>
    /// <returns>The composed compound mode context.</returns>
    public static int ComposeCompoundContext(int refMvContext, int newMvContext) => (refMvContext >> 1) switch
    {
        0 => Math.Min(newMvContext, 1),
        1 => 1 + Math.Min(newMvContext, 3),
        _ => Math.Clamp(3 + newMvContext, 4, 7),
    };

    private void Set(int index, Av1MotionVector candidate0, Av1MotionVector candidate1, int candidateWeight)
    {
        this.mv0Y[index] = candidate0.Y;
        this.mv0X[index] = candidate0.X;
        this.mv1Y[index] = candidate1.Y;
        this.mv1X[index] = candidate1.X;
        this.weight[index] = candidateWeight;
    }

    private void BubbleSort(int start, int end)
    {
        int len = end;
        while (len > start)
        {
            int last = start;
            for (int n = start + 1; n < len; n++)
            {
                if (this.weight[n - 1] < this.weight[n])
                {
                    this.Swap(n - 1, n);
                    last = n;
                }
            }

            len = last;
        }
    }

    private void Swap(int a, int b)
    {
        (this.mv0Y[a], this.mv0Y[b]) = (this.mv0Y[b], this.mv0Y[a]);
        (this.mv0X[a], this.mv0X[b]) = (this.mv0X[b], this.mv0X[a]);
        (this.mv1Y[a], this.mv1Y[b]) = (this.mv1Y[b], this.mv1Y[a]);
        (this.mv1X[a], this.mv1X[b]) = (this.mv1X[b], this.mv1X[a]);
        (this.weight[a], this.weight[b]) = (this.weight[b], this.weight[a]);
    }
}
