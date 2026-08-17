// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Default (quantizer-independent) inter-mode CDFs ported from dav1d 1.4.1's <c>default_cdf.m</c>,
/// in the inverse-CDF layout (boundaries, a terminal 0 and an adaptation counter). These drive the
/// single-reference inter block decode: the is-inter flag, the inter prediction mode (NEW / GLOBAL /
/// NEAREST / NEAR) and the dynamic reference list, plus the compound and reference-frame selection.
/// </summary>
internal static class Av1DefaultInterModeCdf
{
    /// <summary>The is-inter flag CDFs, indexed by context [0, 3].</summary>
    public static readonly ushort[][] IsInter =
    [
        [31962, 0, 0],
        [16106, 0, 0],
        [12582, 0, 0],
        [6230, 0, 0],
    ];

    /// <summary>The skip-mode flag CDFs, indexed by context [0, 2].</summary>
    public static readonly ushort[][] SkipMode =
    [
        [147, 0, 0],
        [12060, 0, 0],
        [24641, 0, 0],
    ];

    /// <summary>The new-mv flag CDFs, indexed by context [0, 5].</summary>
    public static readonly ushort[][] NewMv =
    [
        [8733, 0, 0],
        [16138, 0, 0],
        [17429, 0, 0],
        [24382, 0, 0],
        [20546, 0, 0],
        [28092, 0, 0],
    ];

    /// <summary>The global-mv flag CDFs, indexed by context [0, 1].</summary>
    public static readonly ushort[][] GlobalMv =
    [
        [30593, 0, 0],
        [31714, 0, 0],
    ];

    /// <summary>The ref-mv flag CDFs, indexed by context [0, 5].</summary>
    public static readonly ushort[][] RefMv =
    [
        [8794, 0, 0],
        [8580, 0, 0],
        [14920, 0, 0],
        [4146, 0, 0],
        [8456, 0, 0],
        [12845, 0, 0],
    ];

    /// <summary>The dynamic-reference-list bit CDFs, indexed by context [0, 2].</summary>
    public static readonly ushort[][] DrlBit =
    [
        [19664, 0, 0],
        [8208, 0, 0],
        [13823, 0, 0],
    ];

    /// <summary>The compound (is-compound) flag CDFs, indexed by context [0, 4].</summary>
    public static readonly ushort[][] Compound =
    [
        [5940, 0, 0],
        [8733, 0, 0],
        [20737, 0, 0],
        [22128, 0, 0],
        [29867, 0, 0],
    ];

    /// <summary>The single-reference selection CDFs, indexed by bit position [0, 5] then context [0, 2].</summary>
    public static readonly ushort[][][] SingleReference =
    [
        [[27871, 0, 0], [15795, 0, 0], [3024, 0, 0]],
        [[31213, 0, 0], [16017, 0, 0], [2489, 0, 0]],
        [[28532, 0, 0], [13121, 0, 0], [1574, 0, 0]],
        [[24118, 0, 0], [7995, 0, 0], [873, 0, 0]],
        [[31864, 0, 0], [21754, 0, 0], [5893, 0, 0]],
        [[31324, 0, 0], [17681, 0, 0], [2464, 0, 0]],
    ];

    /// <summary>The compound reference-direction (bidirectional vs unidirectional) CDFs.</summary>
    public static readonly ushort[][] CompoundDirection = [
        [31570, 0, 0],
        [30698, 0, 0],
        [23602, 0, 0],
        [25269, 0, 0],
        [10293, 0, 0],
    ];

    /// <summary>The compound forward-reference CDFs (three trees, three contexts each).</summary>
    public static readonly ushort[][][] CompoundForwardReference = [
        [
            [27822, 0, 0],
            [12877, 0, 0],
            [2037, 0, 0],
        ],
        [
            [23300, 0, 0],
            [10327, 0, 0],
            [1709, 0, 0],
        ],
        [
            [31265, 0, 0],
            [17608, 0, 0],
            [5224, 0, 0],
        ],
    ];

    /// <summary>The compound backward-reference CDFs (two trees, three contexts each).</summary>
    public static readonly ushort[][][] CompoundBackwardReference = [
        [
            [30533, 0, 0],
            [15586, 0, 0],
            [2162, 0, 0],
        ],
        [
            [31345, 0, 0],
            [17593, 0, 0],
            [2279, 0, 0],
        ],
    ];

    /// <summary>The unidirectional compound reference CDFs (three trees, three contexts each).</summary>
    public static readonly ushort[][][] CompoundUniReference = [
        [
            [27484, 0, 0],
            [9616, 0, 0],
            [994, 0, 0],
        ],
        [
            [28903, 0, 0],
            [18595, 0, 0],
            [7648, 0, 0],
        ],
        [
            [29640, 0, 0],
            [17498, 0, 0],
            [6058, 0, 0],
        ],
    ];

    /// <summary>The compound inter-mode CDFs (eight symbols), indexed by the mode context.</summary>
    public static readonly ushort[][] CompoundInterMode = [
        [25008, 18945, 16960, 15127, 13612, 12102, 5877, 0, 0],
        [22038, 13316, 11623, 10019, 8729, 7637, 4044, 0, 0],
        [22104, 12547, 11180, 9862, 8473, 7381, 4332, 0, 0],
        [19470, 15784, 12297, 8586, 7701, 7032, 6346, 0, 0],
        [13864, 9443, 7526, 5336, 4870, 4510, 2010, 0, 0],
        [22043, 15314, 12644, 9948, 8573, 7600, 6722, 0, 0],
        [15643, 8495, 6954, 5276, 4554, 4064, 2176, 0, 0],
        [19722, 9554, 8263, 6826, 5333, 4326, 3438, 0, 0],
    ];

    /// <summary>The masked-vs-unmasked compound CDFs, indexed by the mask context.</summary>
    /// <summary>dav1d <c>jnt_comp</c>: plain average vs distance-weighted average, by context.</summary>
    public static readonly ushort[][] JntComp = [
        [14524, 0, 0], [19903, 0, 0], [25715, 0, 0],
        [19509, 0, 0], [23434, 0, 0], [28124, 0, 0],
    ];

    public static readonly ushort[][] MaskComp = [
        [6161, 0, 0],
        [9877, 0, 0],
        [13928, 0, 0],
        [8174, 0, 0],
        [12834, 0, 0],
        [10094, 0, 0],
    ];

    /// <summary>The wedge-vs-segmented compound CDFs, indexed by the wedge block-size context.</summary>
    public static readonly ushort[][] WedgeComp = [
        [9337, 0, 0],
        [19597, 0, 0],
        [21298, 0, 0],
        [22998, 0, 0],
        [23668, 0, 0],
        [24535, 0, 0],
        [26596, 0, 0],
        [20948, 0, 0],
        [25067, 0, 0],
    ];

    /// <summary>The wedge-index CDFs (16 symbols), indexed by the wedge block-size context.</summary>
    public static readonly ushort[][] WedgeIdx = [
        [30330, 28328, 26169, 24105, 21763, 19894, 17017, 14674, 12409, 10406, 8641, 7066, 5016, 3318, 1597, 0, 0],
        [31962, 29502, 26763, 26030, 25550, 25401, 24997, 18180, 16445, 15401, 14316, 13346, 9929, 6641, 3139, 0, 0],
        [29989, 29030, 28085, 25555, 24993, 24751, 24113, 18411, 14829, 11436, 8248, 5298, 3312, 2239, 1112, 0, 0],
        [31084, 29143, 27093, 25660, 23466, 21494, 18339, 15624, 13605, 11807, 9884, 8297, 6049, 4054, 1891, 0, 0],
        [31626, 29277, 26491, 25454, 24679, 24413, 23745, 19144, 17399, 16038, 14654, 13455, 10247, 6756, 3218, 0, 0],
        [30026, 28573, 27041, 24733, 23788, 23432, 22622, 18644, 15498, 12235, 9334, 6796, 4824, 3198, 1352, 0, 0],
        [31041, 28820, 26667, 24972, 22927, 20424, 17002, 13824, 12130, 10730, 8805, 7457, 5780, 4002, 1756, 0, 0],
        [32614, 31781, 30843, 30717, 30680, 30657, 30617, 9735, 9065, 8484, 7783, 7084, 5509, 3885, 1857, 0, 0],
        [31633, 31446, 31275, 30133, 30072, 30031, 29998, 11752, 9833, 7711, 5517, 3595, 2679, 1808, 835, 0, 0],
    ];

    /// <summary>The inter-intra flag CDFs, indexed by the y-mode size group.</summary>
    public static readonly ushort[][] InterIntra = [
        [16384, 0, 0],
        [5881, 0, 0],
        [5171, 0, 0],
        [2531, 0, 0],
    ];

    /// <summary>The inter-intra mode CDFs (four symbols), indexed by the y-mode size group.</summary>
    public static readonly ushort[][] InterIntraMode = [
        [24576, 16384, 8192, 0, 0],
        [30893, 21686, 5436, 0, 0],
        [30295, 22772, 6380, 0, 0],
        [28530, 21231, 6842, 0, 0],
    ];

    /// <summary>The inter-intra wedge flag CDFs, indexed by the wedge block-size context.</summary>
    public static readonly ushort[][] InterIntraWedge = [
        [12732, 0, 0],
        [7811, 0, 0],
        [6064, 0, 0],
        [5238, 0, 0],
        [3204, 0, 0],
        [3324, 0, 0],
        [5896, 0, 0],
    ];
}
