// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The eight-slot decoded-frame buffer used for inter prediction (specification section 7.20, reference
/// frame update process). A decoded frame is written into every slot selected by its
/// <c>refresh_frame_flags</c> mask; inter frames read references through their reference-frame indices.
/// </summary>
internal sealed class Av1ReferenceFrameStore
{
    private const int SlotCount = 8;

    private readonly Av1ReferenceFrame?[] slots = new Av1ReferenceFrame?[SlotCount];

    /// <summary>Gets the reference frame stored in the given slot, or <see langword="null"/> if empty.</summary>
    /// <param name="slot">The reference slot index (0-7).</param>
    /// <returns>The stored reference frame.</returns>
    public Av1ReferenceFrame? this[int slot] => this.slots[slot];

    /// <summary>Gets the order hints of all eight slots (zero for empty slots).</summary>
    /// <returns>The eight order hints, indexed by slot.</returns>
    public int[] GetOrderHints()
    {
        int[] hints = new int[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            hints[i] = this.slots[i]?.OrderHint ?? 0;
        }

        return hints;
    }

    /// <summary>Writes a decoded frame into every slot selected by the refresh mask.</summary>
    /// <param name="frame">The decoded reference frame.</param>
    /// <param name="refreshFrameFlags">The eight-bit slot refresh mask.</param>
    public void Update(Av1ReferenceFrame frame, int refreshFrameFlags)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if ((refreshFrameFlags & (1 << i)) != 0)
            {
                this.slots[i] = frame;
            }
        }
    }
}
