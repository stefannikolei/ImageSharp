// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The intra-edge availability flags a block receives from the partition tree (a port of dav1d's
/// <c>enum EdgeFlags</c>): whether the block's above-right and left-bottom reference samples are
/// available, with separate bits per chroma layout.
/// </summary>
internal static class Av1IntraEdgeFlags
{
    public const byte I444TopHasRight = 1 << 0;
    public const byte I422TopHasRight = 1 << 1;
    public const byte I420TopHasRight = 1 << 2;
    public const byte I444LeftHasBottom = 1 << 3;
    public const byte I422LeftHasBottom = 1 << 4;
    public const byte I420LeftHasBottom = 1 << 5;
    public const byte AllTopHasRight = I444TopHasRight | I422TopHasRight | I420TopHasRight;
    public const byte AllLeftHasBottom = I444LeftHasBottom | I422LeftHasBottom | I420LeftHasBottom;
    public const byte AllTrAndBl = AllTopHasRight | AllLeftHasBottom;
}

/// <summary>
/// A node of the intra-edge availability tree (dav1d's <c>EdgeNode</c>): the flags a block receives
/// for each partition shape at this level. <see cref="O"/> applies to a NONE partition,
/// <see cref="H"/>/<see cref="V"/> to the two halves of a horizontal/vertical split (and the wide
/// parts of the T-shapes).
/// </summary>
internal class Av1EdgeNode
{
    /// <summary>Gets or sets the flags for a NONE partition.</summary>
    public byte O { get; set; }

    /// <summary>Gets the flags for the top/bottom halves of a horizontal split.</summary>
    public byte[] H { get; } = new byte[2];

    /// <summary>Gets the flags for the left/right halves of a vertical split.</summary>
    public byte[] V { get; } = new byte[2];
}

/// <summary>A leaf node covering an 8x8 block whose SPLIT partition produces 4x4 blocks.</summary>
internal sealed class Av1EdgeTip : Av1EdgeNode
{
    /// <summary>Gets the flags of the top-right, bottom-left and bottom-right 4x4 quadrants (the
    /// top-left quadrant always has both edges).</summary>
    public byte[] Split { get; } = new byte[3];
}

/// <summary>A branch node whose SPLIT partition recurses into four child nodes.</summary>
internal sealed class Av1EdgeBranch : Av1EdgeNode
{
    /// <summary>Gets or sets the flags of the second block of an H4 partition (the third gets all
    /// left-bottom edges, the fourth <see cref="Av1EdgeNode.H"/>[1]).</summary>
    public byte H4 { get; set; }

    /// <summary>Gets or sets the flags of the second block of a V4 partition.</summary>
    public byte V4 { get; set; }

    /// <summary>Gets the four SPLIT child nodes in raster order.</summary>
    public Av1EdgeNode[] Children { get; } = new Av1EdgeNode[4];
}

/// <summary>
/// The static intra-edge availability trees for 128x128 and 64x64 superblocks (a port of dav1d's
/// <c>dav1d_init_intra_edge_tree</c>). The partition recursion walks the tree alongside the block
/// tree and hands each block its edge flags.
/// </summary>
internal static class Av1IntraEdgeTree
{
    /// <summary>Gets the tree rooted at a 128x128 superblock.</summary>
    public static Av1EdgeNode Root128 { get; } = BuildNode(depth: 0, topHasRight: true, leftHasBottom: false, tipDepth: 4);

    /// <summary>Gets the tree rooted at a 64x64 superblock.</summary>
    public static Av1EdgeNode Root64 { get; } = BuildNode(depth: 1, topHasRight: true, leftHasBottom: false, tipDepth: 4);

    // dav1d init_mode_node: a branch for levels down to 16x16, whose children at 8x8 are tips.
    private static Av1EdgeNode BuildNode(int depth, bool topHasRight, bool leftHasBottom, int tipDepth)
    {
        byte flags = (byte)((topHasRight ? Av1IntraEdgeFlags.AllTopHasRight : 0)
                          | (leftHasBottom ? Av1IntraEdgeFlags.AllLeftHasBottom : 0));
        if (depth == tipDepth)
        {
            Av1EdgeTip tip = new();
            InitEdges(tip, isTip: true, flags);
            return tip;
        }

        Av1EdgeBranch branch = new();
        InitEdges(branch, isTip: false, flags);
        if (depth == tipDepth - 1)
        {
            branch.H4 |= (byte)(flags & Av1IntraEdgeFlags.I420TopHasRight);
            branch.V4 |= (byte)(flags & (Av1IntraEdgeFlags.I420LeftHasBottom | Av1IntraEdgeFlags.I422LeftHasBottom));
        }

        for (int n = 0; n < 4; n++)
        {
            branch.Children[n] = BuildNode(
                depth + 1,
                topHasRight: !(n == 3 || (n == 1 && !topHasRight)),
                leftHasBottom: n == 0 || (n == 2 && leftHasBottom),
                tipDepth);
        }

        return branch;
    }

    // dav1d init_edges.
    private static void InitEdges(Av1EdgeNode node, bool isTip, byte flags)
    {
        node.O = flags;
        node.H[0] = (byte)(flags | Av1IntraEdgeFlags.AllLeftHasBottom);
        node.V[0] = (byte)(flags | Av1IntraEdgeFlags.AllTopHasRight);

        if (isTip)
        {
            Av1EdgeTip tip = (Av1EdgeTip)node;
            node.H[1] = (byte)(flags & (Av1IntraEdgeFlags.AllLeftHasBottom | Av1IntraEdgeFlags.I420TopHasRight));
            node.V[1] = (byte)(flags & (Av1IntraEdgeFlags.AllTopHasRight
                                      | Av1IntraEdgeFlags.I420LeftHasBottom
                                      | Av1IntraEdgeFlags.I422LeftHasBottom));
            tip.Split[0] = (byte)((flags & Av1IntraEdgeFlags.AllTopHasRight) | Av1IntraEdgeFlags.I422LeftHasBottom);
            tip.Split[1] = (byte)(flags | Av1IntraEdgeFlags.I444TopHasRight);
            tip.Split[2] = (byte)(flags & (Av1IntraEdgeFlags.I420TopHasRight
                                         | Av1IntraEdgeFlags.I420LeftHasBottom
                                         | Av1IntraEdgeFlags.I422LeftHasBottom));
        }
        else
        {
            Av1EdgeBranch branch = (Av1EdgeBranch)node;
            node.H[1] = (byte)(flags & Av1IntraEdgeFlags.AllLeftHasBottom);
            node.V[1] = (byte)(flags & Av1IntraEdgeFlags.AllTopHasRight);
            branch.H4 = Av1IntraEdgeFlags.AllLeftHasBottom;
            branch.V4 = Av1IntraEdgeFlags.AllTopHasRight;
        }
    }
}
