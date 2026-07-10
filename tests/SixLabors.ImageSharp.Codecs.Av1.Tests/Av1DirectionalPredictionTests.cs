// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the directional intra predictor against dav1d 1.4.1's ipred_z1/z2/z3, including the intra
/// edge filtering and 2x upsampling. Reference outputs were produced by dav1d's own predictors for
/// random reference edges (zone 1: angles below 90 degrees; zone 2: 90-180; zone 3: above 180).
/// </summary>
public class Av1DirectionalPredictionTests
{
    [Fact]
    public void Zone1_Size4_MatchesDav1d()
    {
        ushort[] above = [243, 151, 248, 104, 128, 5, 144, 225];
        ushort[] left = [105, 254, 217, 18, 170, 149, 135, 30];
        ushort[] reference = [151, 248, 104, 128, 248, 104, 128, 5, 104, 128, 5, 144, 128, 5, 144, 225];
        ushort[] dst = new ushort[4 * 4];
        Av1DirectionalPrediction.Predict(above, left, 58, 4, 4, 3, 0, true, false, true, true, 256, 256, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "D45 zone-1 prediction differs from dav1d.");
    }
    [Fact]
    public void Zone1_Size8_MatchesDav1d()
    {
        ushort[] above = [108, 44, 54, 168, 235, 2, 104, 72, 165, 199, 95, 21, 240, 4, 164, 120];
        ushort[] left = [148, 156, 103, 191, 182, 65, 13, 82, 255, 78, 52, 130, 144, 109, 132, 186];
        ushort[] reference = [63, 80, 156, 160, 86, 71, 103, 150, 80, 156, 160, 86, 71, 103, 150, 165, 156, 160, 86, 71, 103, 150, 165, 103, 160, 86, 71, 103, 150, 165, 103, 94, 86, 71, 103, 150, 165, 103, 94, 126, 71, 103, 150, 165, 103, 94, 126, 103, 103, 150, 165, 103, 94, 126, 103, 113, 150, 165, 103, 94, 126, 103, 113, 131];
        ushort[] dst = new ushort[8 * 8];
        Av1DirectionalPrediction.Predict(above, left, 106, 8, 8, 3, 0, true, false, true, true, 256, 256, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "D45 zone-1 prediction differs from dav1d.");
    }
    [Fact]
    public void Zone1_Size16_MatchesDav1d()
    {
        ushort[] above = [83, 23, 23, 139, 205, 183, 96, 149, 77, 190, 110, 132, 227, 208, 4, 57, 98, 147, 181, 30, 21, 8, 9, 71, 12, 1, 66, 74, 126, 46, 59, 246];
        ushort[] left = [3, 18, 11, 250, 241, 159, 194, 217, 5, 197, 113, 234, 120, 181, 199, 66, 100, 19, 3, 80, 212, 97, 90, 169, 209, 231, 111, 226, 45, 158, 214, 6];
        ushort[] reference = [73, 82, 118, 147, 157, 142, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 82, 118, 147, 157, 142, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 118, 147, 157, 142, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 147, 157, 142, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 157, 142, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 142, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 127, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 130, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 129, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 146, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 167, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 53, 156, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 53, 72, 133, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 53, 72, 77, 108, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 53, 72, 77, 98, 84, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 53, 72, 77, 98, 134, 99, 117, 104, 77, 39, 22, 26, 24, 30, 38, 53, 72, 77, 98, 134, 174];
        ushort[] dst = new ushort[16 * 16];
        Av1DirectionalPrediction.Predict(above, left, 184, 16, 16, 3, 0, true, false, true, true, 256, 256, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "D45 zone-1 prediction differs from dav1d.");
    }
    [Fact]
    public void Zone1_Size32_MatchesDav1d()
    {
        ushort[] above = [186, 227, 32, 61, 136, 2, 142, 211, 240, 230, 253, 31, 83, 172, 164, 182, 199, 89, 50, 40, 34, 206, 92, 22, 90, 140, 156, 205, 145, 33, 169, 80, 224, 23, 212, 140, 136, 143, 114, 147, 89, 117, 128, 238, 153, 8, 227, 182, 0, 226, 170, 50, 74, 120, 51, 7, 96, 87, 247, 251, 136, 140, 50, 8];
        ushort[] left = [177, 116, 85, 244, 151, 223, 46, 191, 177, 177, 64, 240, 111, 208, 12, 139, 103, 153, 4, 95, 80, 195, 193, 7, 214, 0, 195, 121, 243, 176, 108, 123, 119, 38, 58, 235, 134, 172, 92, 146, 164, 205, 181, 4, 224, 62, 119, 181, 82, 229, 240, 32, 160, 22, 33, 52, 142, 5, 130, 43, 18, 244, 78, 16];
        ushort[] reference = [145, 120, 86, 72, 104, 136, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 120, 86, 72, 104, 136, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 86, 72, 104, 136, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 72, 104, 136, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 104, 136, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 136, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 177, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 220, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 211, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 169, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 142, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 124, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 131, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 165, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 169, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 144, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 112, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 74, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 68, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 88, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 91, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 96, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 94, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 94, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 125, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 155, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 148, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 175, 136, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 175, 187, 122, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 175, 187, 169, 117, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 175, 187, 169, 114, 125, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 175, 187, 169, 114, 68, 129, 142, 139, 143, 146, 134, 129, 120, 119, 132, 151, 145, 144, 150, 123, 132, 152, 128, 121, 117, 89, 68, 66, 64, 85, 140, 175, 187, 169, 114, 68, 35];
        ushort[] dst = new ushort[32 * 32];
        Av1DirectionalPrediction.Predict(above, left, 208, 32, 32, 3, 0, true, false, true, true, 256, 256, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "D45 zone-1 prediction differs from dav1d.");
    }

    [Fact]
    public void Zone2_Size8_MatchesDav1d()
    {
        // Mode 5 (113 degrees), edge filter on, no upsampling.
        ushort[] above = [238, 231, 97, 94, 243, 95, 48, 228, 155, 72, 46, 21, 202, 231, 80, 7];
        ushort[] left = [32, 30, 18, 97, 123, 15, 237, 167, 225, 100, 119, 150, 255, 2, 43, 234];
        ushort[] reference = [235, 241, 154, 81, 188, 168, 51, 150, 232, 240, 210, 91, 120, 224, 81, 75, 137, 236, 237, 133, 86, 208, 141, 50, 48, 234, 241, 189, 85, 147, 204, 68, 36, 189, 237, 234, 112, 91, 229, 114, 68, 66, 235, 243, 168, 79, 173, 185, 88, 32, 231, 238, 225, 95, 102, 237, 95, 53, 95, 236, 240, 147, 82, 194];
        ushort[] dst = new ushort[8 * 8];
        Av1DirectionalPrediction.Predict(above, left, 231, 8, 8, 5, 0, true, false, true, true, 32, 32, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Zone-2 prediction differs from dav1d.");
    }

    [Fact]
    public void Zone2_Size4_Upsample_MatchesDav1d()
    {
        // Mode 5 (113 degrees), edge filter on; the small block upsamples the above edge.
        ushort[] above = [124, 12, 228, 94, 61, 176, 40, 118];
        ushort[] left = [137, 25, 163, 91, 220, 24, 10, 135];
        ushort[] reference = [108, 51, 138, 162, 82, 103, 46, 211, 85, 114, 36, 171, 90, 92, 83, 80];
        ushort[] dst = new ushort[4 * 4];
        Av1DirectionalPrediction.Predict(above, left, 71, 4, 4, 5, 0, true, false, true, true, 16, 16, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Zone-2 upsample prediction differs from dav1d.");
    }

    [Fact]
    public void Zone3_Size4_Upsample_MatchesDav1d()
    {
        // Mode 7 (203 degrees), edge filter on; the small block upsamples the left edge.
        ushort[] above = [119, 141, 245, 95, 164, 246, 227, 144];
        ushort[] left = [36, 154, 211, 158, 254, 26, 218, 143];
        ushort[] reference = [80, 133, 175, 200, 187, 205, 196, 173, 187, 166, 189, 231, 208, 242, 190, 94];
        ushort[] dst = new ushort[4 * 4];
        Av1DirectionalPrediction.Predict(above, left, 85, 4, 4, 7, 0, true, false, true, true, 16, 16, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Zone-3 upsample prediction differs from dav1d.");
    }

    [Fact]
    public void Zone3_Size8_MatchesDav1d()
    {
        // Mode 7 (203 degrees), edge filter on.
        ushort[] above = [59, 90, 194, 115, 148, 14, 115, 59, 4, 51, 80, 155, 193, 207, 166, 183];
        ushort[] left = [37, 173, 144, 81, 57, 35, 48, 138, 161, 55, 148, 17, 176, 197, 49, 244];
        ushort[] reference = [87, 149, 172, 161, 137, 110, 84, 70, 171, 152, 127, 100, 78, 65, 58, 47, 117, 91, 73, 63, 54, 43, 36, 35, 68, 60, 50, 41, 35, 36, 47, 81, 46, 38, 35, 40, 58, 95, 134, 156, 35, 44, 71, 109, 143, 162, 161, 118, 85, 124, 151, 162, 149, 101, 60, 91, 158, 161, 131, 86, 66, 106, 144, 96];
        ushort[] dst = new ushort[8 * 8];
        Av1DirectionalPrediction.Predict(above, left, 209, 8, 8, 7, 0, true, false, true, true, 32, 32, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Zone-3 prediction differs from dav1d.");
    }

    [Fact]
    public void Zone1_Rect4x8_MatchesDav1d()
    {
        // 4x8 block, mode 3 (45 degrees), edge filter on.
        ushort[] above = Av1TestData.Widen(Convert.FromBase64String("III8/ebxwmsw+Q7H"));
        ushort[] left = Av1TestData.Widen(Convert.FromBase64String("3QHkiHU0og8LDQTD"));
        ushort[] reference = Av1TestData.Widen(Convert.FromBase64String("WH7H737H7+PH7+O47+O4geO4gWu4gWtrgWtra2tra2s="));
        ushort[] dst = new ushort[4 * 8];
        Av1DirectionalPrediction.Predict(above, left, 68, 4, 8, 3, 0, true, false, true, true, 16, 32, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Rectangular zone-1 prediction differs from dav1d.");
    }

    [Fact]
    public void Zone2_Rect8x4_MatchesDav1d()
    {
        // 8x4 block, mode 5 (113 degrees), edge filter on.
        ushort[] above = Av1TestData.Widen(Convert.FromBase64String("Liu4Vp2AbBJR3Mm+"));
        ushort[] left = Av1TestData.Widen(Convert.FromBase64String("44kSDrruo8LYVFp4"));
        ushort[] reference = Av1TestData.Widen(Convert.FromBase64String("JiaBg3uReDcfK0OqYJp+XmspKJVyiItziyIoW5xpl3w="));
        ushort[] dst = new ushort[8 * 4];
        Av1DirectionalPrediction.Predict(above, left, 28, 8, 4, 5, 0, true, false, true, true, 32, 16, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Rectangular zone-2 prediction differs from dav1d.");
    }

    [Fact]
    public void Zone3_Rect16x8_MatchesDav1d()
    {
        // 16x8 block, mode 7 (203 degrees), edge filter on.
        ushort[] above = Av1TestData.Widen(Convert.FromBase64String("Qr3yIQbwhHdi8PPLTXZNxwcgURWaD4ny"));
        ushort[] left = Av1TestData.Widen(Convert.FromBase64String("xtrK40S7MRJF/W+E35rXxbPQdqwOj1On"));
        ushort[] reference = Av1TestData.Widen(Convert.FromBase64String("v8vR1dO/raSekX1pX1RGNtLWzLipopyJdmZbUEAwQV7Fsaagl4JuYldKOjBLaXyNpJ2PemleVEQ0O1ZygpOan4hzZVpOPi5FYHiImJyhoJxhVkk4MlBrfo6ZnqKem6CoQzI9WnOElZugoJ2bo6u0vkdleYuYnaGfnJ6mrrfBwb8="));
        ushort[] dst = new ushort[16 * 8];
        Av1DirectionalPrediction.Predict(above, left, 121, 16, 8, 7, 0, true, false, true, true, 64, 32, dst);
        Assert.True(dst.AsSpan().SequenceEqual(reference), "Rectangular zone-3 prediction differs from dav1d.");
    }
}
