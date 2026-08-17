// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Deep-copy helpers for the jagged inverse-CDF tables held by the per-tile CDF contexts. Used both to
/// materialise mutable contexts from the shared default tables and to snapshot a frame's adapted state
/// for inheritance by a later frame's <c>primary_ref_frame</c>.
/// </summary>
internal static class Av1CdfTables
{
    public static ushort[] Copy(ushort[] table) => (ushort[])table.Clone();

    public static ushort[][] Copy(ushort[][] table)
    {
        ushort[][] result = new ushort[table.Length][];
        for (int i = 0; i < table.Length; i++)
        {
            result[i] = (ushort[])table[i].Clone();
        }

        return result;
    }

    public static ushort[][][] Copy(ushort[][][] table)
    {
        ushort[][][] result = new ushort[table.Length][][];
        for (int i = 0; i < table.Length; i++)
        {
            result[i] = Copy(table[i]);
        }

        return result;
    }

    // The frame-end save zeroes every CDF's adaptation counter (the trailing array element), so the next
    // frame inheriting the state adapts at the initial (fastest) rate again — dav1d's cdf_thread_update.
    public static void ResetCounter(ushort[] table) => table[^1] = 0;

    public static void ResetCounters(ushort[]?[] table)
    {
        foreach (ushort[]? entry in table)
        {
            if (entry is not null)
            {
                ResetCounter(entry);
            }
        }
    }

    public static void ResetCounters(ushort[][][] table)
    {
        foreach (ushort[][] entry in table)
        {
            ResetCounters(entry);
        }
    }
}
