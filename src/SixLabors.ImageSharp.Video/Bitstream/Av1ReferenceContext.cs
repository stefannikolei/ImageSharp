// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Derives the neighbour-dependent contexts for the single-reference frame selection and the
/// interpolation-filter syntax, a port of the reference decoder's <c>av1_get_*_ref_ctx</c> and
/// <c>get_filter_ctx</c> functions (<c>env.h</c>).
/// </summary>
internal static class Av1ReferenceContext
{
    private const int SwitchableFilters = 3;

    /// <summary>
    /// Computes the six single-reference bit contexts (indexed by reference CDF bit position) consumed
    /// by <see cref="Av1ReferenceFrameReader.ReadSingleReference"/>.
    /// </summary>
    /// <param name="above">The above neighbour.</param>
    /// <param name="left">The left neighbour.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <returns>The six reference bit contexts.</returns>
    public static int[] ComputeSingleReferenceContexts(
        in Av1ReferenceNeighbour above,
        in Av1ReferenceNeighbour left,
        bool haveTop,
        bool haveLeft)
    {
        return
        [
            ReferenceContext(above, left, haveTop, haveLeft),
            BackwardReferenceContext(above, left, haveTop, haveLeft),
            ForwardReferenceContext(above, left, haveTop, haveLeft),
            ForwardReference1Context(above, left, haveTop, haveLeft),
            ForwardReference2Context(above, left, haveTop, haveLeft),
            BackwardReference1Context(above, left, haveTop, haveLeft),
        ];
    }

    /// <summary>Computes the compound-flag context (dav1d <c>get_comp_ctx</c>).</summary>
    /// <param name="a">The above neighbour.</param>
    /// <param name="l">The left neighbour.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <returns>The context.</returns>
    public static int ComputeCompoundContext(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        if (haveTop)
        {
            if (haveLeft)
            {
                if (a.IsCompound)
                {
                    return l.IsCompound ? 4 : 2 + ((uint)l.Reference0 >= 4u ? 1 : 0);
                }

                if (l.IsCompound)
                {
                    return 2 + ((uint)a.Reference0 >= 4u ? 1 : 0);
                }

                return (l.Reference0 >= 4 ? 1 : 0) ^ (a.Reference0 >= 4 ? 1 : 0);
            }

            return a.IsCompound ? 3 : a.Reference0 >= 4 ? 1 : 0;
        }

        if (haveLeft)
        {
            return l.IsCompound ? 3 : l.Reference0 >= 4 ? 1 : 0;
        }

        return 1;
    }

    /// <summary>Computes the compound-direction (bidirectional vs unidirectional) context
    /// (dav1d <c>get_comp_dir_ctx</c>).</summary>
    /// <param name="a">The above neighbour.</param>
    /// <param name="l">The left neighbour.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <returns>The context.</returns>
    public static int ComputeCompoundDirectionContext(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        static bool HasUniCompound(in Av1ReferenceNeighbour n) => (n.Reference0 < 4) == (n.Reference1 < 4);

        if (haveTop && haveLeft)
        {
            if (a.IsIntra && l.IsIntra)
            {
                return 2;
            }

            if (a.IsIntra || l.IsIntra)
            {
                Av1ReferenceNeighbour edge = a.IsIntra ? l : a;
                if (!edge.IsCompound)
                {
                    return 2;
                }

                return 1 + (2 * (HasUniCompound(edge) ? 1 : 0));
            }

            bool aComp = a.IsCompound;
            bool lComp = l.IsCompound;
            int aRef0 = a.Reference0;
            int lRef0 = l.Reference0;

            if (!aComp && !lComp)
            {
                return 1 + (2 * ((aRef0 >= 4) == (lRef0 >= 4) ? 1 : 0));
            }

            if (!aComp || !lComp)
            {
                Av1ReferenceNeighbour edge = aComp ? a : l;
                if (!HasUniCompound(edge))
                {
                    return 1;
                }

                return 3 + ((aRef0 >= 4) == (lRef0 >= 4) ? 1 : 0);
            }

            bool aUni = HasUniCompound(a);
            bool lUni = HasUniCompound(l);
            if (!aUni && !lUni)
            {
                return 0;
            }

            if (!aUni || !lUni)
            {
                return 2;
            }

            return 3 + ((aRef0 == 4) == (lRef0 == 4) ? 1 : 0);
        }

        if (haveTop || haveLeft)
        {
            Av1ReferenceNeighbour edge = haveLeft ? l : a;
            if (edge.IsIntra || !edge.IsCompound)
            {
                return 2;
            }

            return 4 * (HasUniCompound(edge) ? 1 : 0);
        }

        return 2;
    }

    /// <summary>Computes the unidirectional-compound second-reference context
    /// (dav1d <c>av1_get_uni_p1_ctx</c>).</summary>
    /// <param name="a">The above neighbour.</param>
    /// <param name="l">The left neighbour.</param>
    /// <param name="haveTop">Whether an above neighbour is available.</param>
    /// <param name="haveLeft">Whether a left neighbour is available.</param>
    /// <returns>The context.</returns>
    public static int ComputeUniP1Context(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        Span<int> cnt = stackalloc int[3];
        Accumulate(a, haveTop, cnt);
        Accumulate(l, haveLeft, cnt);
        cnt[1] += cnt[2];
        return cnt[0] == cnt[1] ? 1 : cnt[0] < cnt[1] ? 0 : 2;

        static void Accumulate(in Av1ReferenceNeighbour n, bool have, Span<int> cnt)
        {
            if (have && !n.IsIntra)
            {
                if ((uint)(n.Reference0 - 1) < 3u)
                {
                    cnt[n.Reference0 - 1]++;
                }

                if (n.IsCompound && (uint)(n.Reference1 - 1) < 3u)
                {
                    cnt[n.Reference1 - 1]++;
                }
            }
        }
    }

    /// <summary>
    /// Computes the interpolation-filter context for one filter direction, a port of
    /// <c>get_filter_ctx</c>.
    /// </summary>
    /// <param name="above">The above neighbour.</param>
    /// <param name="left">The left neighbour.</param>
    /// <param name="isCompound">Whether the current block uses compound prediction.</param>
    /// <param name="direction">The filter direction (0 = horizontal, 1 = vertical).</param>
    /// <param name="reference">The current block's zero-based reference index.</param>
    /// <returns>The filter context.</returns>
    public static int ComputeFilterContext(
        in Av1ReferenceNeighbour above,
        in Av1ReferenceNeighbour left,
        bool isCompound,
        int direction,
        int reference)
    {
        int comp = isCompound ? 1 : 0;
        int aboveFilter = (above.Reference0 == reference || above.Reference1 == reference)
            ? (direction == 0 ? above.Filter0 : above.Filter1)
            : SwitchableFilters;
        int leftFilter = (left.Reference0 == reference || left.Reference1 == reference)
            ? (direction == 0 ? left.Filter0 : left.Filter1)
            : SwitchableFilters;

        if (aboveFilter == leftFilter)
        {
            return (comp * 4) + aboveFilter;
        }

        if (aboveFilter == SwitchableFilters)
        {
            return (comp * 4) + leftFilter;
        }

        if (leftFilter == SwitchableFilters)
        {
            return (comp * 4) + aboveFilter;
        }

        return (comp * 4) + SwitchableFilters;
    }

    private static int ReferenceContext(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        int cnt0 = 0;
        int cnt1 = 0;
        if (haveTop && !a.IsIntra)
        {
            Increment(ref cnt0, ref cnt1, a.Reference0 >= 4);
            if (a.IsCompound)
            {
                Increment(ref cnt0, ref cnt1, a.Reference1 >= 4);
            }
        }

        if (haveLeft && !l.IsIntra)
        {
            Increment(ref cnt0, ref cnt1, l.Reference0 >= 4);
            if (l.IsCompound)
            {
                Increment(ref cnt0, ref cnt1, l.Reference1 >= 4);
            }
        }

        return Compare(cnt0, cnt1);
    }

    private static int ForwardReferenceContext(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        // An unwritten neighbour (the inter-frame reset state) carries reference -1 and counts for
        // neither bucket.
        int[] cnt = new int[4];
        if (haveTop && !a.IsIntra)
        {
            if (a.Reference0 is >= 0 and < 4)
            {
                cnt[a.Reference0]++;
            }

            if (a.IsCompound && a.Reference1 is >= 0 and < 4)
            {
                cnt[a.Reference1]++;
            }
        }

        if (haveLeft && !l.IsIntra)
        {
            if (l.Reference0 is >= 0 and < 4)
            {
                cnt[l.Reference0]++;
            }

            if (l.IsCompound && l.Reference1 is >= 0 and < 4)
            {
                cnt[l.Reference1]++;
            }
        }

        cnt[0] += cnt[1];
        cnt[2] += cnt[3];
        return Compare(cnt[0], cnt[2]);
    }

    private static int ForwardReference1Context(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
        => BoundedContext(a, l, haveTop, haveLeft, lower: 0);

    private static int ForwardReference2Context(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
        => BoundedContext(a, l, haveTop, haveLeft, lower: 2);

    private static int BackwardReference1Context(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        int cnt0 = 0;
        int cnt1 = 0;
        Accumulate(a, haveTop, ref cnt0, ref cnt1);
        Accumulate(l, haveLeft, ref cnt0, ref cnt1);
        return Compare(cnt0, cnt1);

        static void Accumulate(in Av1ReferenceNeighbour n, bool have, ref int c0, ref int c1)
        {
            if (have && !n.IsIntra)
            {
                AddBackward(n.Reference0, ref c0, ref c1);
                if (n.IsCompound)
                {
                    AddBackward(n.Reference1, ref c0, ref c1);
                }
            }
        }

        static void AddBackward(int reference, ref int c0, ref int c1)
        {
            if (reference == 4)
            {
                c0++;
            }
            else if (reference == 5)
            {
                c1++;
            }
        }
    }

    private static int BackwardReferenceContext(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft)
    {
        int[] cnt = new int[3];
        Accumulate(a, haveTop, cnt);
        Accumulate(l, haveLeft, cnt);
        cnt[1] += cnt[0];
        return cnt[2] == cnt[1] ? 1 : cnt[1] < cnt[2] ? 0 : 2;

        static void Accumulate(in Av1ReferenceNeighbour n, bool have, int[] cnt)
        {
            if (have && !n.IsIntra)
            {
                if (n.Reference0 >= 4)
                {
                    cnt[n.Reference0 - 4]++;
                }

                if (n.IsCompound && n.Reference1 >= 4)
                {
                    cnt[n.Reference1 - 4]++;
                }
            }
        }
    }

    private static int BoundedContext(in Av1ReferenceNeighbour a, in Av1ReferenceNeighbour l, bool haveTop, bool haveLeft, int lower)
    {
        int cnt0 = 0;
        int cnt1 = 0;
        Accumulate(a, haveTop, lower, ref cnt0, ref cnt1);
        Accumulate(l, haveLeft, lower, ref cnt0, ref cnt1);
        return Compare(cnt0, cnt1);

        static void Accumulate(in Av1ReferenceNeighbour n, bool have, int lower, ref int c0, ref int c1)
        {
            if (have && !n.IsIntra)
            {
                Add(n.Reference0, lower, ref c0, ref c1);
                if (n.IsCompound)
                {
                    Add(n.Reference1, lower, ref c0, ref c1);
                }
            }
        }

        static void Add(int reference, int lower, ref int c0, ref int c1)
        {
            int offset = reference - lower;
            if ((uint)offset < 2)
            {
                Increment(ref c0, ref c1, offset == 1);
            }
        }
    }

    private static void Increment(ref int cnt0, ref int cnt1, bool selectSecond)
    {
        if (selectSecond)
        {
            cnt1++;
        }
        else
        {
            cnt0++;
        }
    }

    private static int Compare(int cnt0, int cnt1)
        => cnt0 == cnt1 ? 1 : cnt0 < cnt1 ? 0 : 2;
}
