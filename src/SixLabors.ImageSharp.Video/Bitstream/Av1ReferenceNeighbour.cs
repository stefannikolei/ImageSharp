// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// A neighbouring block's reference state used to derive the inter reference and filter contexts
/// (the relevant fields of the reference decoder's <c>BlockContext</c>). Reference indices are
/// zero-based (LAST = 0 .. ALTREF = 6); an intra neighbour is flagged via <see cref="IsIntra"/>.
/// </summary>
internal readonly struct Av1ReferenceNeighbour
{
    /// <summary>Initializes a new instance of the <see cref="Av1ReferenceNeighbour"/> struct.</summary>
    /// <param name="isIntra">Whether the neighbour is an intra block.</param>
    /// <param name="reference0">The neighbour's first zero-based reference index.</param>
    /// <param name="reference1">The neighbour's second zero-based reference index.</param>
    /// <param name="isCompound">Whether the neighbour uses compound prediction.</param>
    /// <param name="filter0">The neighbour's horizontal interpolation filter (3 = unset).</param>
    /// <param name="filter1">The neighbour's vertical interpolation filter (3 = unset).</param>
    /// <param name="compoundType">The neighbour's compound type (0 = none, 2 = average, 3 = seg,
    /// 4 = wedge).</param>
    public Av1ReferenceNeighbour(bool isIntra, int reference0, int reference1, bool isCompound, int filter0, int filter1, int compoundType = 0)
    {
        this.IsIntra = isIntra;
        this.Reference0 = reference0;
        this.Reference1 = reference1;
        this.IsCompound = isCompound || compoundType != 0;
        this.CompoundType = compoundType;
        this.Filter0 = filter0;
        this.Filter1 = filter1;
    }

    /// <summary>Gets a value indicating whether the neighbour is an intra block.</summary>
    public bool IsIntra { get; }

    /// <summary>Gets the neighbour's first zero-based reference index.</summary>
    public int Reference0 { get; }

    /// <summary>Gets the neighbour's second zero-based reference index.</summary>
    public int Reference1 { get; }

    /// <summary>Gets a value indicating whether the neighbour uses compound prediction.</summary>
    public bool IsCompound { get; }

    /// <summary>Gets the neighbour's compound type (dav1d <c>CompInterType</c>: 0 = none,
    /// 2 = average, 3 = seg, 4 = wedge).</summary>
    public int CompoundType { get; }

    /// <summary>Gets the neighbour's horizontal interpolation filter (3 = unset).</summary>
    public int Filter0 { get; }

    /// <summary>Gets the neighbour's vertical interpolation filter (3 = unset).</summary>
    public int Filter1 { get; }
}
