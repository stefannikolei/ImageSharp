// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The 4x4-unit boundaries of a tile, used to bound neighbour scans during motion-vector prediction.
/// </summary>
internal readonly struct Av1TileBounds
{
    /// <summary>Initializes a new instance of the <see cref="Av1TileBounds"/> struct.</summary>
    /// <param name="columnStart">The tile's first 4x4 column (inclusive).</param>
    /// <param name="columnEnd">The tile's last 4x4 column (exclusive).</param>
    /// <param name="rowStart">The tile's first 4x4 row (inclusive).</param>
    /// <param name="rowEnd">The tile's last 4x4 row (exclusive).</param>
    public Av1TileBounds(int columnStart, int columnEnd, int rowStart, int rowEnd)
    {
        this.ColumnStart = columnStart;
        this.ColumnEnd = columnEnd;
        this.RowStart = rowStart;
        this.RowEnd = rowEnd;
    }

    /// <summary>Gets the tile's first 4x4 column (inclusive).</summary>
    public int ColumnStart { get; }

    /// <summary>Gets the tile's last 4x4 column (exclusive).</summary>
    public int ColumnEnd { get; }

    /// <summary>Gets the tile's first 4x4 row (inclusive).</summary>
    public int RowStart { get; }

    /// <summary>Gets the tile's last 4x4 row (exclusive).</summary>
    public int RowEnd { get; }
}
