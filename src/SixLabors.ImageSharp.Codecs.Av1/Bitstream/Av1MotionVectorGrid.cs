// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The frame-wide 4x4-resolution motion-vector reference grid (the reference decoder's
/// <c>refmvs_block</c> storage). Every decoded block writes its motion information into the 4x4 cells it
/// covers via <see cref="Fill"/>; the MV prediction process reads neighbouring cells to assemble
/// candidate lists. A full-frame grid is kept for indexing simplicity, which is functionally equivalent
/// to the reference decoder's sliding super-block-row buffer.
/// </summary>
internal sealed class Av1MotionVectorGrid
{
    private readonly Av1RefMvsBlock[] cells;

    /// <summary>Initializes a new instance of the <see cref="Av1MotionVectorGrid"/> class.</summary>
    /// <param name="columns4">The frame width in 4x4 units.</param>
    /// <param name="rows4">The frame height in 4x4 units.</param>
    public Av1MotionVectorGrid(int columns4, int rows4)
    {
        this.Columns4 = columns4;
        this.Rows4 = rows4;
        this.cells = new Av1RefMvsBlock[columns4 * rows4];
    }

    /// <summary>Gets the frame width in 4x4 units.</summary>
    public int Columns4 { get; }

    /// <summary>Gets the frame height in 4x4 units.</summary>
    public int Rows4 { get; }

    /// <summary>Gets the grid cell at the given 4x4 position.</summary>
    /// <param name="row">The 4x4 row.</param>
    /// <param name="column">The 4x4 column.</param>
    /// <returns>The stored block.</returns>
    public Av1RefMvsBlock this[int row, int column] => this.cells[(row * this.Columns4) + column];

    /// <summary>
    /// Returns a read-only span over a single grid row starting at the given column.
    /// </summary>
    /// <param name="row">The 4x4 row.</param>
    /// <param name="column">The starting 4x4 column.</param>
    /// <returns>The row span from the column to the right frame edge.</returns>
    public ReadOnlySpan<Av1RefMvsBlock> Row(int row, int column)
        => this.cells.AsSpan((row * this.Columns4) + column, this.Columns4 - column);

    /// <summary>
    /// Writes a decoded block's motion information into every 4x4 cell it covers.
    /// </summary>
    /// <param name="row">The block's top 4x4 row.</param>
    /// <param name="column">The block's left 4x4 column.</param>
    /// <param name="width4">The block width in 4x4 units.</param>
    /// <param name="height4">The block height in 4x4 units.</param>
    /// <param name="block">The block to store.</param>
    public void Fill(int row, int column, int width4, int height4, in Av1RefMvsBlock block)
    {
        int rowEnd = Math.Min(row + height4, this.Rows4);
        int columnEnd = Math.Min(column + width4, this.Columns4);
        for (int y = row; y < rowEnd; y++)
        {
            int offset = y * this.Columns4;
            for (int x = column; x < columnEnd; x++)
            {
                this.cells[offset + x] = block;
            }
        }
    }
}
