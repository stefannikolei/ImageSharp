// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the eight-slot reference-frame store update logic: a key frame (refresh mask 0xFF) populates
/// every slot, and the order hints are reported per slot.
/// </summary>
public class Av1ReferenceFrameStoreTests
{
    [Fact]
    public void Update_KeyFrame_PopulatesAllSlots()
    {
        Av1Plane luma = new(64, 64);
        Av1Plane u = new(32, 32);
        Av1Plane v = new(32, 32);
        luma[0, 0] = 42;
        Av1ReferenceFrame frame = new(0, luma, u, v);

        Av1ReferenceFrameStore store = new();
        store.Update(frame, 0xFF);

        for (int i = 0; i < 8; i++)
        {
            Assert.NotNull(store[i]);
            Assert.Equal(0, store[i]!.OrderHint);
            Assert.Equal(64, store[i]!.Luma.Width);
            Assert.Equal(42, store[i]!.Luma[0, 0]);
        }

        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0 }, store.GetOrderHints());
    }

    [Fact]
    public void Update_PartialMask_OnlyRefreshesSelectedSlots()
    {
        Av1ReferenceFrameStore store = new();
        Av1Plane luma0 = new(16, 16);
        store.Update(new Av1ReferenceFrame(0, luma0, null, null), 0xFF);

        Av1Plane luma1 = new(16, 16);
        store.Update(new Av1ReferenceFrame(5, luma1, null, null), 0b0000_1001); // slots 0 and 3

        Assert.Equal(5, store[0]!.OrderHint);
        Assert.Equal(0, store[1]!.OrderHint);
        Assert.Equal(0, store[2]!.OrderHint);
        Assert.Equal(5, store[3]!.OrderHint);
        Assert.Equal(0, store[4]!.OrderHint);
    }
}
