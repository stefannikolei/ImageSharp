// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// Locates the per-tile compressed data within a tile group (specification section 5.11.1,
/// <c>tile_group_obu</c>). Each tile's byte range is the input to a fresh symbol decoder.
/// </summary>
internal readonly struct ObuTileGroup
{
    private readonly (int Offset, int Length)[] tiles;

    private ObuTileGroup((int Offset, int Length)[] tiles, int firstTile, int lastTile)
    {
        this.tiles = tiles;
        this.FirstTile = firstTile;
        this.LastTile = lastTile;
    }

    /// <summary>Gets the index of the first tile present in this group.</summary>
    public int FirstTile { get; }

    /// <summary>Gets the index of the last tile present in this group.</summary>
    public int LastTile { get; }

    /// <summary>Gets the number of tiles present in this group.</summary>
    public int Count => this.tiles.Length;

    /// <summary>
    /// Parses a tile group, returning the byte ranges of each tile's compressed data.
    /// </summary>
    /// <param name="data">The tile group bytes, starting at the (byte-aligned) tile group syntax.</param>
    /// <param name="frameHeader">The active frame header.</param>
    /// <returns>The parsed <see cref="ObuTileGroup"/>.</returns>
    public static ObuTileGroup Parse(ReadOnlySpan<byte> data, in ObuFrameHeader frameHeader)
    {
        int numTiles = ((frameHeader.TileColumnStarts?.Length ?? 2) - 1) * ((frameHeader.TileRowStarts?.Length ?? 2) - 1);

        Av1BitStreamReader reader = new(data);
        int tgStart = 0;
        int tgEnd = numTiles - 1;

        bool tileStartAndEndPresent = false;
        if (numTiles > 1)
        {
            tileStartAndEndPresent = reader.ReadBoolean();
        }

        if (numTiles > 1 && tileStartAndEndPresent)
        {
            int tileBits = frameHeader.TileColumnsLog2 + frameHeader.TileRowsLog2;
            tgStart = (int)reader.ReadLiteral(tileBits);
            tgEnd = (int)reader.ReadLiteral(tileBits);
        }

        // byte_alignment().
        int offset = (reader.BitPosition + 7) >> 3;

        int tileCount = tgEnd - tgStart + 1;
        (int Offset, int Length)[] tiles = new (int, int)[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            bool lastTile = i == tileCount - 1;
            int tileSize;
            if (lastTile)
            {
                tileSize = data.Length - offset;
            }
            else
            {
                int sizeMinus1 = (int)ReadLittleEndian(data, offset, frameHeader.TileSizeBytes);
                offset += frameHeader.TileSizeBytes;
                tileSize = sizeMinus1 + 1;
            }

            tiles[i] = (offset, tileSize);
            offset += tileSize;
        }

        return new ObuTileGroup(tiles, tgStart, tgEnd);
    }

    /// <summary>
    /// Gets the compressed data range of the tile at the given position within this group.
    /// </summary>
    /// <param name="index">The zero-based index within this group.</param>
    /// <returns>The (offset, length) byte range.</returns>
    public (int Offset, int Length) GetTile(int index) => this.tiles[index];

    private static uint ReadLittleEndian(ReadOnlySpan<byte> data, int offset, int byteCount)
    {
        uint value = 0;
        for (int i = 0; i < byteCount; i++)
        {
            value |= (uint)data[offset + i] << (8 * i);
        }

        return value;
    }
}
