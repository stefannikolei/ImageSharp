// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The above/left neighbour-context store for inter block decoding (the inter-relevant fields of the
/// reference decoder's <c>BlockContext</c>). For every 4x4 unit it tracks the intra flag, reference
/// indices, compound flag, interpolation filters and skip-mode flag. The above arrays span the frame
/// width; the left arrays span a super-block-row height and are reset per super-block row. Each decoded
/// block writes its state back via <see cref="Write"/>; the context-derivation helpers read it.
/// </summary>
internal sealed class Av1InterNeighbourContext
{
    private const int FilterUnset = 3;

    private readonly byte[] aboveIntra;
    private readonly byte[] leftIntra;
    private readonly sbyte[] aboveReference0;
    private readonly sbyte[] leftReference0;
    private readonly sbyte[] aboveReference1;
    private readonly sbyte[] leftReference1;
    private readonly byte[] aboveCompound;
    private readonly byte[] leftCompound;
    private readonly byte[] aboveFilter0;
    private readonly byte[] leftFilter0;
    private readonly byte[] aboveFilter1;
    private readonly byte[] leftFilter1;
    private readonly byte[] aboveSkipMode;
    private readonly byte[] leftSkipMode;

    /// <summary>Initializes a new instance of the <see cref="Av1InterNeighbourContext"/> class.</summary>
    /// <param name="columns4">The frame width in 4x4 units.</param>
    /// <param name="rows4">The frame height in 4x4 units.</param>
    public Av1InterNeighbourContext(int columns4, int rows4)
    {
        this.aboveIntra = new byte[columns4];
        this.leftIntra = new byte[rows4];
        this.aboveReference0 = new sbyte[columns4];
        this.leftReference0 = new sbyte[rows4];
        this.aboveReference1 = new sbyte[columns4];
        this.leftReference1 = new sbyte[rows4];
        this.aboveCompound = new byte[columns4];
        this.leftCompound = new byte[rows4];
        this.aboveFilter0 = new byte[columns4];
        this.leftFilter0 = new byte[rows4];
        this.aboveFilter1 = new byte[columns4];
        this.leftFilter1 = new byte[rows4];
        this.aboveSkipMode = new byte[columns4];
        this.leftSkipMode = new byte[rows4];

        this.ClearAbove(0, columns4);
        this.ClearLeft();
    }

    /// <summary>Gets the above neighbour state at the given 4x4 column.</summary>
    /// <param name="column">The 4x4 column.</param>
    /// <returns>The above neighbour.</returns>
    public Av1ReferenceNeighbour GetAbove(int column) => new(
        this.aboveIntra[column] != 0,
        this.aboveReference0[column],
        this.aboveReference1[column],
        this.aboveCompound[column] != 0,
        this.aboveFilter0[column],
        this.aboveFilter1[column],
        this.aboveCompound[column]);

    /// <summary>Gets the left neighbour state at the given 4x4 row.</summary>
    /// <param name="row">The 4x4 row.</param>
    /// <returns>The left neighbour.</returns>
    public Av1ReferenceNeighbour GetLeft(int row) => new(
        this.leftIntra[row] != 0,
        this.leftReference0[row],
        this.leftReference1[row],
        this.leftCompound[row] != 0,
        this.leftFilter0[row],
        this.leftFilter1[row],
        this.leftCompound[row]);

    /// <summary>Gets the above intra flag at the given 4x4 column (0 or 1).</summary>
    /// <param name="column">The 4x4 column.</param>
    /// <returns>The intra flag.</returns>
    public int AboveIntra(int column) => this.aboveIntra[column];

    /// <summary>Gets the left intra flag at the given 4x4 row (0 or 1).</summary>
    /// <param name="row">The 4x4 row.</param>
    /// <returns>The intra flag.</returns>
    public int LeftIntra(int row) => this.leftIntra[row];

    /// <summary>Gets the above skip-mode flag at the given 4x4 column (0 or 1).</summary>
    /// <param name="column">The 4x4 column.</param>
    /// <returns>The skip-mode flag.</returns>
    public int AboveSkipMode(int column) => this.aboveSkipMode[column];

    /// <summary>Gets the left skip-mode flag at the given 4x4 row (0 or 1).</summary>
    /// <param name="row">The 4x4 row.</param>
    /// <returns>The skip-mode flag.</returns>
    public int LeftSkipMode(int row) => this.leftSkipMode[row];

    /// <summary>
    /// Writes a decoded block's inter state into the above and left neighbour arrays.
    /// </summary>
    /// <param name="row">The block's top 4x4 row.</param>
    /// <param name="column">The block's left 4x4 column.</param>
    /// <param name="width4">The block width in 4x4 units.</param>
    /// <param name="height4">The block height in 4x4 units.</param>
    /// <param name="isIntra">Whether the block is intra.</param>
    /// <param name="reference0">The block's first zero-based reference (-1 for intra).</param>
    /// <param name="reference1">The block's second zero-based reference (-1 for none).</param>
    /// <param name="isCompound">Whether the block uses compound prediction.</param>
    /// <param name="filter0">The block's horizontal interpolation filter.</param>
    /// <param name="filter1">The block's vertical interpolation filter.</param>
    /// <param name="skipMode">Whether the block uses skip mode.</param>
    public void Write(
        int row,
        int column,
        int width4,
        int height4,
        bool isIntra,
        int reference0,
        int reference1,
        bool isCompound,
        int filter0,
        int filter1,
        bool skipMode,
        int compoundType = -1)
    {
        byte intra = (byte)(isIntra ? 1 : 0);
        byte compound = (byte)(compoundType >= 0 ? compoundType : isCompound ? 2 : 0);
        byte skip = (byte)(skipMode ? 1 : 0);

        int columnEnd = Math.Min(column + width4, this.aboveIntra.Length);
        for (int x = column; x < columnEnd; x++)
        {
            this.aboveIntra[x] = intra;
            this.aboveReference0[x] = (sbyte)reference0;
            this.aboveReference1[x] = (sbyte)reference1;
            this.aboveCompound[x] = compound;
            this.aboveFilter0[x] = (byte)filter0;
            this.aboveFilter1[x] = (byte)filter1;
            this.aboveSkipMode[x] = skip;
        }

        int rowEnd = Math.Min(row + height4, this.leftIntra.Length);
        for (int y = row; y < rowEnd; y++)
        {
            this.leftIntra[y] = intra;
            this.leftReference0[y] = (sbyte)reference0;
            this.leftReference1[y] = (sbyte)reference1;
            this.leftCompound[y] = compound;
            this.leftFilter0[y] = (byte)filter0;
            this.leftFilter1[y] = (byte)filter1;
            this.leftSkipMode[y] = skip;
        }
    }

    /// <summary>Resets the left neighbour arrays at the start of a super-block row (the reference
    /// decoder's <c>reset_context</c> for inter frames: not-intra, no reference, unset filter, so an
    /// unwritten row is overlappable but never matches a reference).</summary>
    public void ClearLeft()
    {
        Array.Clear(this.leftIntra);
        Array.Fill(this.leftReference0, (sbyte)-1);
        Array.Fill(this.leftReference1, (sbyte)-1);
        Array.Fill(this.leftFilter0, (byte)FilterUnset);
        Array.Fill(this.leftFilter1, (byte)FilterUnset);
        Array.Clear(this.leftCompound);
        Array.Clear(this.leftSkipMode);
    }

    /// <summary>Resets a column range of the above neighbour arrays at the start of a tile (the same
    /// inter-frame reset values as <see cref="ClearLeft"/>).</summary>
    /// <param name="column">The first 4x4 column of the range.</param>
    /// <param name="count">The number of columns.</param>
    public void ClearAbove(int column, int count)
    {
        Array.Clear(this.aboveIntra, column, count);
        Array.Fill(this.aboveReference0, (sbyte)-1, column, count);
        Array.Fill(this.aboveReference1, (sbyte)-1, column, count);
        Array.Fill(this.aboveFilter0, (byte)FilterUnset, column, count);
        Array.Fill(this.aboveFilter1, (byte)FilterUnset, column, count);
        Array.Clear(this.aboveCompound, column, count);
        Array.Clear(this.aboveSkipMode, column, count);
    }
}
