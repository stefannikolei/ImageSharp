// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Formats.Av1.Prediction;

/// <summary>
/// The warped (affine) motion compensation path: shear-parameter derivation
/// (<c>dav1d_get_shear_params</c>), the global-motion block vector (<c>get_gmv_2d</c>) and the 8x8
/// warp kernel (<c>warp_affine_8x8</c>) with its block driver (<c>warp_affine</c> in
/// <c>recon_tmpl.c</c>), for 8-bit samples.
/// </summary>
internal static class Av1WarpedMotion
{
    // dav1d div_lut: reciprocal multipliers for the shear-parameter division.
    private static readonly ushort[] DivLut =
    [
        16384, 16320, 16257, 16194, 16132, 16070, 16009, 15948, 15888, 15828, 15768,
        15709, 15650, 15592, 15534, 15477, 15420, 15364, 15308, 15252, 15197, 15142,
        15087, 15033, 14980, 14926, 14873, 14821, 14769, 14717, 14665, 14614, 14564,
        14513, 14463, 14413, 14364, 14315, 14266, 14218, 14170, 14122, 14075, 14028,
        13981, 13935, 13888, 13843, 13797, 13752, 13707, 13662, 13618, 13574, 13530,
        13487, 13443, 13400, 13358, 13315, 13273, 13231, 13190, 13148, 13107, 13066,
        13026, 12985, 12945, 12906, 12866, 12827, 12788, 12749, 12710, 12672, 12633,
        12596, 12558, 12520, 12483, 12446, 12409, 12373, 12336, 12300, 12264, 12228,
        12193, 12157, 12122, 12087, 12053, 12018, 11984, 11950, 11916, 11882, 11848,
        11815, 11782, 11749, 11716, 11683, 11651, 11619, 11586, 11555, 11523, 11491,
        11460, 11429, 11398, 11367, 11336, 11305, 11275, 11245, 11215, 11185, 11155,
        11125, 11096, 11067, 11038, 11009, 10980, 10951, 10923, 10894, 10866, 10838,
        10810, 10782, 10755, 10727, 10700, 10673, 10645, 10618, 10592, 10565, 10538,
        10512, 10486, 10460, 10434, 10408, 10382, 10356, 10331, 10305, 10280, 10255,
        10230, 10205, 10180, 10156, 10131, 10107, 10082, 10058, 10034, 10010, 9986,
        9963, 9939, 9916, 9892, 9869, 9846, 9823, 9800, 9777, 9754, 9732,
        9709, 9687, 9664, 9642, 9620, 9598, 9576, 9554, 9533, 9511, 9489,
        9468, 9447, 9425, 9404, 9383, 9362, 9341, 9321, 9300, 9279, 9259,
        9239, 9218, 9198, 9178, 9158, 9138, 9118, 9098, 9079, 9059, 9039,
        9020, 9001, 8981, 8962, 8943, 8924, 8905, 8886, 8867, 8849, 8830,
        8812, 8793, 8775, 8756, 8738, 8720, 8702, 8684, 8666, 8648, 8630,
        8613, 8595, 8577, 8560, 8542, 8525, 8508, 8490, 8473, 8456, 8439,
        8422, 8405, 8389, 8372, 8355, 8339, 8322, 8306, 8289, 8273, 8257,
        8240, 8224, 8208, 8192,
    ];

    // dav1d_mc_warp_filter: the 8-tap warp interpolation filters, indexed by 64 + (frac >> 10).
    private static readonly sbyte[][] WarpFilter =
    [
        [0, 0, 127, 1, 0, 0, 0, 0],
        [0, -1, 127, 2, 0, 0, 0, 0],
        [1, -3, 127, 4, - 1, 0, 0, 0],
        [1, -4, 126, 6, -2, 1, 0, 0],
        [1, -5, 126, 8, - 3, 1, 0, 0],
        [1, -6, 125, 11, -4, 1, 0, 0],
        [1, -7, 124, 13, - 4, 1, 0, 0],
        [2, -8, 123, 15, -5, 1, 0, 0],
        [2, -9, 122, 18, - 6, 1, 0, 0],
        [2, -10, 121, 20, -6, 1, 0, 0],
        [2, -11, 120, 22, - 7, 2, 0, 0],
        [2, -12, 119, 25, -8, 2, 0, 0],
        [3, -13, 117, 27, - 8, 2, 0, 0],
        [3, -13, 116, 29, -9, 2, 0, 0],
        [3, -14, 114, 32, -10, 3, 0, 0],
        [3, -15, 113, 35, -10, 2, 0, 0],
        [3, -15, 111, 37, -11, 3, 0, 0],
        [3, -16, 109, 40, -11, 3, 0, 0],
        [3, -16, 108, 42, -12, 3, 0, 0],
        [4, -17, 106, 45, -13, 3, 0, 0],
        [4, -17, 104, 47, -13, 3, 0, 0],
        [4, -17, 102, 50, -14, 3, 0, 0],
        [4, -17, 100, 52, -14, 3, 0, 0],
        [4, -18, 98, 55, -15, 4, 0, 0],
        [4, -18, 96, 58, -15, 3, 0, 0],
        [4, -18, 94, 60, -16, 4, 0, 0],
        [4, -18, 91, 63, -16, 4, 0, 0],
        [4, -18, 89, 65, -16, 4, 0, 0],
        [4, -18, 87, 68, -17, 4, 0, 0],
        [4, -18, 85, 70, -17, 4, 0, 0],
        [4, -18, 82, 73, -17, 4, 0, 0],
        [4, -18, 80, 75, -17, 4, 0, 0],
        [4, -18, 78, 78, -18, 4, 0, 0],
        [4, -17, 75, 80, -18, 4, 0, 0],
        [4, -17, 73, 82, -18, 4, 0, 0],
        [4, -17, 70, 85, -18, 4, 0, 0],
        [4, -17, 68, 87, -18, 4, 0, 0],
        [4, -16, 65, 89, -18, 4, 0, 0],
        [4, -16, 63, 91, -18, 4, 0, 0],
        [4, -16, 60, 94, -18, 4, 0, 0],
        [3, -15, 58, 96, -18, 4, 0, 0],
        [4, -15, 55, 98, -18, 4, 0, 0],
        [3, -14, 52, 100, -17, 4, 0, 0],
        [3, -14, 50, 102, -17, 4, 0, 0],
        [3, -13, 47, 104, -17, 4, 0, 0],
        [3, -13, 45, 106, -17, 4, 0, 0],
        [3, -12, 42, 108, -16, 3, 0, 0],
        [3, -11, 40, 109, -16, 3, 0, 0],
        [3, -11, 37, 111, -15, 3, 0, 0],
        [2, -10, 35, 113, -15, 3, 0, 0],
        [3, -10, 32, 114, -14, 3, 0, 0],
        [2, - 9, 29, 116, -13, 3, 0, 0],
        [2, -8, 27, 117, -13, 3, 0, 0],
        [2, - 8, 25, 119, -12, 2, 0, 0],
        [2, -7, 22, 120, -11, 2, 0, 0],
        [1, - 6, 20, 121, -10, 2, 0, 0],
        [1, -6, 18, 122, - 9, 2, 0, 0],
        [1, - 5, 15, 123, - 8, 2, 0, 0],
        [1, -4, 13, 124, - 7, 1, 0, 0],
        [1, - 4, 11, 125, - 6, 1, 0, 0],
        [1, -3, 8, 126, - 5, 1, 0, 0],
        [1, - 2, 6, 126, - 4, 1, 0, 0],
        [0, -1, 4, 127, - 3, 1, 0, 0],
        [0, 0, 2, 127, - 1, 0, 0, 0],
        [0, 0, 0, 127, 1, 0, 0, 0],
        [0, 0, -1, 127, 2, 0, 0, 0],
        [0, 1, -3, 127, 4, -2, 1, 0],
        [0, 1, -5, 127, 6, -2, 1, 0],
        [0, 2, -6, 126, 8, -3, 1, 0],
        [-1, 2, -7, 126, 11, -4, 2, -1],
        [-1, 3, -8, 125, 13, -5, 2, -1],
        [-1, 3, -10, 124, 16, -6, 3, -1],
        [-1, 4, -11, 123, 18, -7, 3, -1],
        [-1, 4, -12, 122, 20, -7, 3, -1],
        [-1, 4, -13, 121, 23, -8, 3, -1],
        [-2, 5, -14, 120, 25, -9, 4, -1],
        [-1, 5, -15, 119, 27, -10, 4, -1],
        [-1, 5, -16, 118, 30, -11, 4, -1],
        [-2, 6, -17, 116, 33, -12, 5, -1],
        [-2, 6, -17, 114, 35, -12, 5, -1],
        [-2, 6, -18, 113, 38, -13, 5, -1],
        [-2, 7, -19, 111, 41, -14, 6, -2],
        [-2, 7, -19, 110, 43, -15, 6, -2],
        [-2, 7, -20, 108, 46, -15, 6, -2],
        [-2, 7, -20, 106, 49, -16, 6, -2],
        [-2, 7, -21, 104, 51, -16, 7, -2],
        [-2, 7, -21, 102, 54, -17, 7, -2],
        [-2, 8, -21, 100, 56, -18, 7, -2],
        [-2, 8, -22, 98, 59, -18, 7, -2],
        [-2, 8, -22, 96, 62, -19, 7, -2],
        [-2, 8, -22, 94, 64, -19, 7, -2],
        [-2, 8, -22, 91, 67, -20, 8, -2],
        [-2, 8, -22, 89, 69, -20, 8, -2],
        [-2, 8, -22, 87, 72, -21, 8, -2],
        [-2, 8, -21, 84, 74, -21, 8, -2],
        [-2, 8, -22, 82, 77, -21, 8, -2],
        [-2, 8, -21, 79, 79, -21, 8, -2],
        [-2, 8, -21, 77, 82, -22, 8, -2],
        [-2, 8, -21, 74, 84, -21, 8, -2],
        [-2, 8, -21, 72, 87, -22, 8, -2],
        [-2, 8, -20, 69, 89, -22, 8, -2],
        [-2, 8, -20, 67, 91, -22, 8, -2],
        [-2, 7, -19, 64, 94, -22, 8, -2],
        [-2, 7, -19, 62, 96, -22, 8, -2],
        [-2, 7, -18, 59, 98, -22, 8, -2],
        [-2, 7, -18, 56, 100, -21, 8, -2],
        [-2, 7, -17, 54, 102, -21, 7, -2],
        [-2, 7, -16, 51, 104, -21, 7, -2],
        [-2, 6, -16, 49, 106, -20, 7, -2],
        [-2, 6, -15, 46, 108, -20, 7, -2],
        [-2, 6, -15, 43, 110, -19, 7, -2],
        [-2, 6, -14, 41, 111, -19, 7, -2],
        [-1, 5, -13, 38, 113, -18, 6, -2],
        [-1, 5, -12, 35, 114, -17, 6, -2],
        [-1, 5, -12, 33, 116, -17, 6, -2],
        [-1, 4, -11, 30, 118, -16, 5, -1],
        [-1, 4, -10, 27, 119, -15, 5, -1],
        [-1, 4, -9, 25, 120, -14, 5, -2],
        [-1, 3, -8, 23, 121, -13, 4, -1],
        [-1, 3, -7, 20, 122, -12, 4, -1],
        [-1, 3, -7, 18, 123, -11, 4, -1],
        [-1, 3, -6, 16, 124, -10, 3, -1],
        [-1, 2, -5, 13, 125, -8, 3, -1],
        [-1, 2, -4, 11, 126, -7, 2, -1],
        [0, 1, -3, 8, 126, -6, 2, 0],
        [0, 1, -2, 6, 127, -5, 1, 0],
        [0, 1, -2, 4, 127, -3, 1, 0],
        [0, 0, 0, 2, 127, -1, 0, 0],
        [0, 0, 0, 1, 127, 0, 0, 0],
        [0, 0, 0, -1, 127, 2, 0, 0],
        [0, 0, 1, -3, 127, 4, -1, 0],
        [0, 0, 1, -4, 126, 6, -2, 1],
        [0, 0, 1, -5, 126, 8, -3, 1],
        [0, 0, 1, -6, 125, 11, -4, 1],
        [0, 0, 1, -7, 124, 13, -4, 1],
        [0, 0, 2, -8, 123, 15, -5, 1],
        [0, 0, 2, -9, 122, 18, -6, 1],
        [0, 0, 2, -10, 121, 20, -6, 1],
        [0, 0, 2, -11, 120, 22, -7, 2],
        [0, 0, 2, -12, 119, 25, -8, 2],
        [0, 0, 3, -13, 117, 27, -8, 2],
        [0, 0, 3, -13, 116, 29, -9, 2],
        [0, 0, 3, -14, 114, 32, -10, 3],
        [0, 0, 3, -15, 113, 35, -10, 2],
        [0, 0, 3, -15, 111, 37, -11, 3],
        [0, 0, 3, -16, 109, 40, -11, 3],
        [0, 0, 3, -16, 108, 42, -12, 3],
        [0, 0, 4, -17, 106, 45, -13, 3],
        [0, 0, 4, -17, 104, 47, -13, 3],
        [0, 0, 4, -17, 102, 50, -14, 3],
        [0, 0, 4, -17, 100, 52, -14, 3],
        [0, 0, 4, -18, 98, 55, -15, 4],
        [0, 0, 4, -18, 96, 58, -15, 3],
        [0, 0, 4, -18, 94, 60, -16, 4],
        [0, 0, 4, -18, 91, 63, -16, 4],
        [0, 0, 4, -18, 89, 65, -16, 4],
        [0, 0, 4, -18, 87, 68, -17, 4],
        [0, 0, 4, -18, 85, 70, -17, 4],
        [0, 0, 4, -18, 82, 73, -17, 4],
        [0, 0, 4, -18, 80, 75, -17, 4],
        [0, 0, 4, -18, 78, 78, -18, 4],
        [0, 0, 4, -17, 75, 80, -18, 4],
        [0, 0, 4, -17, 73, 82, -18, 4],
        [0, 0, 4, -17, 70, 85, -18, 4],
        [0, 0, 4, -17, 68, 87, -18, 4],
        [0, 0, 4, -16, 65, 89, -18, 4],
        [0, 0, 4, -16, 63, 91, -18, 4],
        [0, 0, 4, -16, 60, 94, -18, 4],
        [0, 0, 3, -15, 58, 96, -18, 4],
        [0, 0, 4, -15, 55, 98, -18, 4],
        [0, 0, 3, -14, 52, 100, -17, 4],
        [0, 0, 3, -14, 50, 102, -17, 4],
        [0, 0, 3, -13, 47, 104, -17, 4],
        [0, 0, 3, -13, 45, 106, -17, 4],
        [0, 0, 3, -12, 42, 108, -16, 3],
        [0, 0, 3, -11, 40, 109, -16, 3],
        [0, 0, 3, -11, 37, 111, -15, 3],
        [0, 0, 2, -10, 35, 113, -15, 3],
        [0, 0, 3, -10, 32, 114, -14, 3],
        [0, 0, 2, -9, 29, 116, -13, 3],
        [0, 0, 2, -8, 27, 117, -13, 3],
        [0, 0, 2, -8, 25, 119, -12, 2],
        [0, 0, 2, -7, 22, 120, -11, 2],
        [0, 0, 1, -6, 20, 121, -10, 2],
        [0, 0, 1, -6, 18, 122, -9, 2],
        [0, 0, 1, -5, 15, 123, -8, 2],
        [0, 0, 1, -4, 13, 124, -7, 1],
        [0, 0, 1, -4, 11, 125, -6, 1],
        [0, 0, 1, -3, 8, 126, -5, 1],
        [0, 0, 1, -2, 6, 126, -4, 1],
        [0, 0, 0, -1, 4, 127, -3, 1],
        [0, 0, 0, 0, 2, 127, -1, 0],
        [0, 0, 0, 0, 2, 127, -1, 0],
    ];

    /// <summary>
    /// Derives the shear parameters (alpha, beta, gamma, delta) of a warped-motion model and reports
    /// whether the model is too sheared to warp (dav1d <c>get_shear_params</c> returning nonzero).
    /// </summary>
    /// <param name="matrix">The six-entry warp matrix.</param>
    /// <param name="shear">Receives alpha, beta, gamma and delta.</param>
    /// <returns><see langword="true"/> when the model cannot be applied as a warp.</returns>
    public static bool TryGetShearParams(ReadOnlySpan<int> matrix, Span<short> shear)
    {
        if (matrix[2] <= 0)
        {
            return true;
        }

        shear[0] = ClipWmp(matrix[2] - 0x10000);
        shear[1] = ClipWmp(matrix[3]);

        int y = ApplySign(ResolveDivisor((uint)Math.Abs(matrix[2]), out int shift), matrix[2]);
        long v1 = (long)matrix[4] * 0x10000 * y;
        int rnd = (1 << shift) >> 1;
        shear[2] = ClipWmp(ApplySign64((int)((Math.Abs(v1) + rnd) >> shift), v1));
        long v2 = (long)matrix[3] * matrix[4] * y;
        shear[3] = ClipWmp(matrix[5] - ApplySign64((int)((Math.Abs(v2) + rnd) >> shift), v2) - 0x10000);

        return (4 * Math.Abs(shear[0])) + (7 * Math.Abs(shear[1])) >= 0x10000 ||
               (4 * Math.Abs(shear[2])) + (4 * Math.Abs(shear[3])) >= 0x10000;
    }

    /// <summary>
    /// Computes the motion vector a global-motion model implies at a block's centre
    /// (dav1d <c>get_gmv_2d</c> for the non-translation types; translation reads the matrix directly).
    /// </summary>
    /// <param name="model">The global-motion model.</param>
    /// <param name="bx4">The block column in 4x4 units.</param>
    /// <param name="by4">The block row in 4x4 units.</param>
    /// <param name="bw4">The block width in 4x4 units.</param>
    /// <param name="bh4">The block height in 4x4 units.</param>
    /// <param name="allowHighPrecisionMv">Whether eighth-pel motion vectors are allowed.</param>
    /// <param name="forceIntegerMv">Whether motion vectors are forced to whole pels.</param>
    /// <returns>The block's global motion vector.</returns>
    public static Av1MotionVector GetGlobalMv(Av1WarpedMotionParams model, int bx4, int by4, int bw4, int bh4, bool allowHighPrecisionMv, bool forceIntegerMv)
    {
        int[] matrix = model.Matrix;
        Av1MotionVector result;
        switch (model.Type)
        {
            case Av1WarpModelType.Identity:
                result = default;
                break;
            case Av1WarpModelType.Translation:
                result = new Av1MotionVector(matrix[0] >> 13, matrix[1] >> 13);
                break;
            default:
            {
                int x = (bx4 * 4) + (bw4 * 2) - 1;
                int y = (by4 * 4) + (bh4 * 2) - 1;
                int xc = ((matrix[2] - (1 << 16)) * x) + (matrix[3] * y) + matrix[0];
                int yc = ((matrix[5] - (1 << 16)) * y) + (matrix[4] * x) + matrix[1];
                int shift = 16 - (3 - (allowHighPrecisionMv ? 0 : 1));
                int round = (1 << shift) >> 1;
                int precisionShift = allowHighPrecisionMv ? 0 : 1;
                result = new Av1MotionVector(
                    ApplySign(((Math.Abs(yc) + round) >> shift) << precisionShift, yc),
                    ApplySign(((Math.Abs(xc) + round) >> shift) << precisionShift, xc));
                break;
            }
        }

        return forceIntegerMv ? Av1MotionVectorPrecision.Fix(result, allowHighPrecision: false, forceInteger: true) : result;
    }

    /// <summary>
    /// Warps one plane of a block from a reference (dav1d's <c>warp_affine</c> driver over 8x8 tiles).
    /// </summary>
    /// <param name="destination">The destination plane.</param>
    /// <param name="reference">The reference plane.</param>
    /// <param name="bx4">The block column in luma 4x4 units.</param>
    /// <param name="by4">The block row in luma 4x4 units.</param>
    /// <param name="width4">The block width in luma 4x4 units.</param>
    /// <param name="height4">The block height in luma 4x4 units.</param>
    /// <param name="matrix">The warp matrix.</param>
    /// <param name="shear">The derived shear parameters (alpha, beta, gamma, delta).</param>
    /// <param name="subsamplingX">The plane's horizontal subsampling.</param>
    /// <param name="subsamplingY">The plane's vertical subsampling.</param>
    public static void WarpPlane(Bitstream.Av1Plane destination, Bitstream.Av1Plane reference, int bx4, int by4, int width4, int height4, ReadOnlySpan<int> matrix, ReadOnlySpan<short> shear, int subsamplingX, int subsamplingY)
    {
        int hMul = 4 >> subsamplingX;
        int vMul = 4 >> subsamplingY;
        int width = reference.CropWidth;
        int height = reference.CropHeight;
        int dstX0 = (bx4 * 4) >> subsamplingX;
        int dstY0 = (by4 * 4) >> subsamplingY;

        Span<byte> tile = stackalloc byte[15 * 15];
        for (int y = 0; y < height4 * vMul; y += 8)
        {
            long srcY = (by4 * 4) + ((y + 4) << subsamplingY);
            long mat3Y = ((long)matrix[3] * srcY) + matrix[0];
            long mat5Y = ((long)matrix[5] * srcY) + matrix[1];
            for (int x = 0; x < width4 * hMul; x += 8)
            {
                long srcX = (bx4 * 4) + ((x + 4) << subsamplingX);
                long mvx = (((long)matrix[2] * srcX) + mat3Y) >> subsamplingX;
                long mvy = (((long)matrix[4] * srcX) + mat5Y) >> subsamplingY;

                int dx = (int)(mvx >> 16) - 4;
                int mx = (((int)mvx & 0xffff) - (shear[0] * 4) - (shear[1] * 7)) & ~0x3f;
                int dy = (int)(mvy >> 16) - 4;
                int my = (((int)mvy & 0xffff) - (shear[2] * 4) - (shear[3] * 4)) & ~0x3f;

                // Gather the 15x15 source tile with edge replication at the visible frame bounds.
                for (int r = 0; r < 15; r++)
                {
                    int sy = Math.Clamp(dy - 3 + r, 0, height - 1);
                    int rowBase = sy * reference.Width;
                    for (int c = 0; c < 15; c++)
                    {
                        int sx = Math.Clamp(dx - 3 + c, 0, width - 1);
                        tile[(r * 15) + c] = reference.Samples[rowBase + sx];
                    }
                }

                Warp8x8(destination, dstX0 + x, dstY0 + y, tile, mx, my, shear);
            }
        }
    }

    // dav1d warp_affine_8x8_c for 8-bit: horizontal pass into a 15x8 intermediate at
    // 7 - intermediate_bits(4) = 3 bits of downshift, vertical pass at 7 + 4 = 11.
    private static void Warp8x8(Bitstream.Av1Plane destination, int dstX, int dstY, ReadOnlySpan<byte> src, int mx, int my, ReadOnlySpan<short> shear)
    {
        Span<int> mid = stackalloc int[15 * 8];
        for (int y = 0; y < 15; y++, mx += shear[1])
        {
            int tmx = mx;
            int srcBase = (y * 15) + 3;
            for (int x = 0; x < 8; x++, tmx += shear[0])
            {
                sbyte[] filter = WarpFilter[64 + ((tmx + 512) >> 10)];
                int sum = (1 << 3) >> 1;
                for (int t = 0; t < 8; t++)
                {
                    sum += filter[t] * src[srcBase + x - 3 + t];
                }

                mid[(y * 8) + x] = sum >> 3;
            }
        }

        for (int y = 0; y < 8; y++, my += shear[3])
        {
            int tmy = my;
            int dstBase = ((dstY + y) * destination.Width) + dstX;
            for (int x = 0; x < 8; x++, tmy += shear[2])
            {
                sbyte[] filter = WarpFilter[64 + ((tmy + 512) >> 10)];
                int sum = (1 << 11) >> 1;
                for (int t = 0; t < 8; t++)
                {
                    sum += filter[t] * mid[((y + t) * 8) + x];
                }

                destination.Samples[dstBase + x] = (byte)Math.Clamp(sum >> 11, 0, 255);
            }
        }
    }

    private static short ClipWmp(int v)
    {
        int cv = Math.Clamp(v, short.MinValue, short.MaxValue);
        return (short)(ApplySign((Math.Abs(cv) + 32) >> 6, cv) * (1 << 6));
    }

    private static int ResolveDivisor(uint d, out int shift)
    {
        shift = 31 - System.Numerics.BitOperations.LeadingZeroCount(d);
        int e = (int)(d - (1u << shift));
        int f = shift > 8 ? (e + (1 << (shift - 9))) >> (shift - 8) : e << (8 - shift);
        shift += 14;
        return DivLut[f];
    }

    private static int ApplySign(int value, int sign) => sign < 0 ? -value : value;

    private static int ApplySign64(int value, long sign) => sign < 0 ? -value : value;
}
