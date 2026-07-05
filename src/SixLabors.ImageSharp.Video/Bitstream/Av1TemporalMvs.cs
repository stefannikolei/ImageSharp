// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A single cell of an 8x8-granularity temporal motion field: the motion vector and the one-based
/// reference it points at (zero marks an invalid/empty cell), the reference decoder's
/// <c>refmvs_temporal_block</c>.
/// </summary>
internal readonly record struct Av1TemporalMvBlock(Av1MotionVector Mv, int Reference);

/// <summary>
/// The temporal motion field a decoded frame saves for later frames' <c>use_ref_frame_mvs</c> prediction
/// (the reference decoder's <c>save_tmvs</c>): one cell per 8x8 luma block, sampled from the frame's
/// 4x4 motion-vector grid, keeping only motion vectors that point at a past reference.
/// </summary>
internal sealed class Av1TemporalMvs
{
    private Av1TemporalMvs(Av1TemporalMvBlock[] blocks, int stride8, int rows8)
    {
        this.Blocks = blocks;
        this.Stride8 = stride8;
        this.Rows8 = rows8;
    }

    /// <summary>Gets the motion-field cells, indexed by <c>y8 * Stride8 + x8</c>.</summary>
    public Av1TemporalMvBlock[] Blocks { get; }

    /// <summary>Gets the field width in 8x8 units.</summary>
    public int Stride8 { get; }

    /// <summary>Gets the field height in 8x8 units.</summary>
    public int Rows8 { get; }

    /// <summary>
    /// Samples a decoded inter frame's motion-vector grid into a temporal motion field. Each 8x8 cell
    /// takes the grid block at its bottom-right 4x4 position (dav1d passes <c>rt-&gt;r + 6</c>, one row
    /// past the +5 base, so <c>rr[(y &amp; 15) * 2]</c> lands on row 2y+1); only single-reference motion
    /// vectors that point at a past reference and are shorter than 512 pels qualify.
    /// </summary>
    /// <param name="grid">The frame's 4x4 motion-vector grid.</param>
    /// <param name="orderHintBits">The sequence's order-hint bit count.</param>
    /// <param name="orderHint">The frame's order hint.</param>
    /// <param name="referenceOrderHints">The order hints of the frame's seven references, by name.</param>
    /// <returns>The saved motion field.</returns>
    public static Av1TemporalMvs Save(Av1MotionVectorGrid grid, int orderHintBits, int orderHint, int[] referenceOrderHints)
    {
        int stride8 = (grid.Columns4 + 1) >> 1;
        int rows8 = (grid.Rows4 + 1) >> 1;
        Av1TemporalMvBlock[] blocks = new Av1TemporalMvBlock[stride8 * rows8];

        // dav1d mfmv_sign: whether each reference lies in the past.
        Span<bool> isPast = stackalloc bool[7];
        for (int i = 0; i < 7; i++)
        {
            isPast[i] = GetOrderHintDifference(orderHintBits, referenceOrderHints[i], orderHint) < 0;
        }

        for (int y8 = 0; y8 < rows8; y8++)
        {
            int row4 = Math.Min((y8 * 2) + 1, grid.Rows4 - 1);
            for (int x8 = 0; x8 < stride8; x8++)
            {
                int col4 = Math.Min((x8 * 2) + 1, grid.Columns4 - 1);
                Av1RefMvsBlock candidate = grid[row4, col4];

                // A compound block prefers its second (typically backward) reference when that
                // reference lies in the past (dav1d save_tmvs).
                int reference1 = candidate.Reference1;
                Av1MotionVector mv1 = candidate.MotionVector1;
                if (reference1 > 0 && isPast[reference1 - 1] && (Math.Abs(mv1.Y) | Math.Abs(mv1.X)) < 4096)
                {
                    blocks[(y8 * stride8) + x8] = new Av1TemporalMvBlock(mv1, reference1);
                    continue;
                }

                int reference = candidate.Reference0;
                Av1MotionVector mv = candidate.MotionVector0;
                if (reference > 0 && isPast[reference - 1] && (Math.Abs(mv.Y) | Math.Abs(mv.X)) < 4096)
                {
                    blocks[(y8 * stride8) + x8] = new Av1TemporalMvBlock(mv, reference);
                }
            }
        }

        return new Av1TemporalMvs(blocks, stride8, rows8);
    }

    /// <summary>Order-hint difference with wrap-around (dav1d <c>get_poc_diff</c>).</summary>
    /// <param name="orderHintBits">The sequence's order-hint bit count.</param>
    /// <param name="a">The first order hint.</param>
    /// <param name="b">The second order hint.</param>
    /// <returns>The signed difference <paramref name="a"/> - <paramref name="b"/>.</returns>
    public static int GetOrderHintDifference(int orderHintBits, int a, int b)
    {
        if (orderHintBits == 0)
        {
            return 0;
        }

        int mask = 1 << (orderHintBits - 1);
        int diff = a - b;
        return (diff & (mask - 1)) - (diff & mask);
    }
}

/// <summary>
/// The per-frame temporal motion-vector prediction state (the reference decoder's motion-field
/// projection): the projected 8x8 field the motion-vector finder samples, plus the order-hint distances
/// used to scale candidates. Created when the frame header carries <c>use_ref_frame_mvs</c>; the
/// projected field is <see langword="null"/> when no eligible motion-field reference exists (the
/// global-motion-vector context still changes in that case).
/// </summary>
internal sealed class Av1TemporalMvContext
{
    private const int MaxProjectionDistance = 31;

    private Av1TemporalMvContext(Av1TemporalMvBlock[]? projected, int stride8, int rows8, int[] pocDiff, bool allowHighPrecisionMv, bool forceIntegerMv)
    {
        this.Projected = projected;
        this.Stride8 = stride8;
        this.Rows8 = rows8;
        this.PocDiff = pocDiff;
        this.AllowHighPrecisionMv = allowHighPrecisionMv;
        this.ForceIntegerMv = forceIntegerMv;
    }

    /// <summary>Gets the projected motion field (cells with reference 0 are invalid), or null.</summary>
    public Av1TemporalMvBlock[]? Projected { get; }

    /// <summary>Gets the field width in 8x8 units.</summary>
    public int Stride8 { get; }

    /// <summary>Gets the field height in 8x8 units.</summary>
    public int Rows8 { get; }

    /// <summary>Gets the clipped order-hint distance from the current frame to each reference, by name.</summary>
    public int[] PocDiff { get; }

    /// <summary>Gets a value indicating whether eighth-pel motion vectors are allowed.</summary>
    public bool AllowHighPrecisionMv { get; }

    /// <summary>Gets a value indicating whether motion vectors are forced to whole pels.</summary>
    public bool ForceIntegerMv { get; }

    /// <summary>
    /// Builds the frame's temporal prediction state: selects up to three motion-field references
    /// (dav1d's <c>refmvs_init_frame</c>) and projects their saved fields onto the current frame
    /// (<c>load_tmvs</c>). Returns <see langword="null"/> when the header does not use temporal motion
    /// vectors.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The inter frame header.</param>
    /// <param name="references">The reference frames, indexed by the zero-based reference name.</param>
    /// <returns>The temporal state, or <see langword="null"/>.</returns>
    public static Av1TemporalMvContext? Create(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1ReferenceFrame?[] references)
    {
        int bits = sequenceHeader.OrderHintBits;
        if (!frameHeader.UseReferenceFrameMotionVectors || bits == 0)
        {
            return null;
        }

        int poc = frameHeader.OrderHint;
        int[] refPoc = new int[7];
        int[] pocDiff = new int[7];
        for (int i = 0; i < 7; i++)
        {
            refPoc[i] = references[i]?.OrderHint ?? 0;
            pocDiff[i] = Math.Clamp(Av1TemporalMvs.GetOrderHintDifference(bits, poc, refPoc[i]), -MaxProjectionDistance, MaxProjectionDistance);
        }

        // Motion-field reference selection: LAST (unless its ALTREF was the current GOLDEN), then up to
        // two future references (BWDREF, ALTREF2, ALTREF), topped up with LAST2.
        Span<int> fieldRefs = stackalloc int[3];
        int fieldCount = 0;
        int total = 2;
        Av1TemporalMvs? lastMvs = references[0]?.TemporalMvs;
        if (lastMvs is not null && references[0]!.ReferenceOrderHints[6] != refPoc[3])
        {
            fieldRefs[fieldCount++] = 0;
            total = 3;
        }

        if (references[4]?.TemporalMvs is not null && Av1TemporalMvs.GetOrderHintDifference(bits, refPoc[4], poc) > 0)
        {
            fieldRefs[fieldCount++] = 4;
        }

        if (references[5]?.TemporalMvs is not null && Av1TemporalMvs.GetOrderHintDifference(bits, refPoc[5], poc) > 0)
        {
            fieldRefs[fieldCount++] = 5;
        }

        if (fieldCount < total && references[6]?.TemporalMvs is not null && Av1TemporalMvs.GetOrderHintDifference(bits, refPoc[6], poc) > 0)
        {
            fieldRefs[fieldCount++] = 6;
        }

        if (fieldCount < total && references[1]?.TemporalMvs is not null)
        {
            fieldRefs[fieldCount++] = 1;
        }

        int stride8 = (frameHeader.ModeInfoColumns + 1) >> 1;
        int rows8 = (frameHeader.ModeInfoRows + 1) >> 1;
        Av1TemporalMvBlock[]? projected = fieldCount > 0 ? new Av1TemporalMvBlock[stride8 * rows8] : null;

        for (int n = 0; n < fieldCount; n++)
        {
            int fieldRef = fieldRefs[n];
            Av1ReferenceFrame reference = references[fieldRef]!;
            int refToCurrent = Av1TemporalMvs.GetOrderHintDifference(bits, refPoc[fieldRef], poc);
            if (Math.Abs(refToCurrent) > MaxProjectionDistance)
            {
                continue;
            }

            // Past references project forwards, future references backwards (dav1d ref2cur).
            refToCurrent = fieldRef < 4 ? -refToCurrent : refToCurrent;

            // The distance from the field reference to each of ITS references; zero disables a cell.
            Span<int> refToRef = stackalloc int[7];
            for (int m = 0; m < 7; m++)
            {
                int diff = Av1TemporalMvs.GetOrderHintDifference(bits, refPoc[fieldRef], reference.ReferenceOrderHints[m]);
                refToRef[m] = (uint)diff > MaxProjectionDistance ? 0 : diff;
            }

            ProjectField(projected!, stride8, rows8, reference.TemporalMvs!, fieldRef < 4 ? -1 : 1, refToCurrent, refToRef);
        }

        return new Av1TemporalMvContext(projected, stride8, rows8, pocDiff, frameHeader.AllowHighPrecisionMv, frameHeader.ForceIntegerMv);
    }

    // dav1d load_tmvs: walk the saved field and write each cell's motion vector into the projected
    // position it points at in the current frame, clamped to the source cell's 8x8-superblock band
    // vertically and to one superblock of slack horizontally.
    private static void ProjectField(Av1TemporalMvBlock[] projected, int stride8, int rows8, Av1TemporalMvs field, int refSign, int refToCurrent, ReadOnlySpan<int> refToRef)
    {
        int cols8 = Math.Min(field.Stride8, stride8);
        int fieldRows8 = Math.Min(field.Rows8, rows8);
        for (int y = 0; y < fieldRows8; y++)
        {
            int bandStart = y & ~7;
            int bandEnd = Math.Min(bandStart + 8, rows8);
            for (int x = 0; x < cols8; x++)
            {
                Av1TemporalMvBlock cell = field.Blocks[(y * field.Stride8) + x];
                if (cell.Reference == 0)
                {
                    continue;
                }

                int distance = refToRef[cell.Reference - 1];
                if (distance == 0)
                {
                    continue;
                }

                Av1MotionVector offset = Av1MotionVectorProjection.Project(cell.Mv, refToCurrent, distance);
                int posX = x + ApplySign(Math.Abs(offset.X) >> 6, offset.X, refSign);
                int posY = y + ApplySign(Math.Abs(offset.Y) >> 6, offset.Y, refSign);
                if (posY >= bandStart && posY < bandEnd &&
                    posX >= Math.Max((x & ~7) - 8, 0) && posX < Math.Min((x & ~7) + 16, stride8))
                {
                    projected[(posY * stride8) + posX] = new Av1TemporalMvBlock(cell.Mv, distance);
                }
            }
        }
    }

    // dav1d apply_sign(v, s ^ ref_sign): the offset direction flips for past references.
    private static int ApplySign(int value, int offsetComponent, int refSign)
        => (offsetComponent < 0 ? -refSign : refSign) < 0 ? -value : value;
}
