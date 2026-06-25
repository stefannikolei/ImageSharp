// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Holds the mutable, adaptive motion-vector CDFs for a single tile (the joint CDF and the two
/// per-component CDF sets), initialized from the default tables. Each tile keeps its own copy so
/// adaptation does not leak across tiles.
/// </summary>
internal sealed class Av1MotionVectorCdfContext
{
    private Av1MotionVectorCdfContext()
    {
    }

    /// <summary>Gets the motion-vector joint CDF (4 symbols).</summary>
    public ushort[] Joint { get; private set; } = default!;

    /// <summary>Gets the two per-component CDF sets (index 0 = row, index 1 = column).</summary>
    public Component[] Components { get; private set; } = default!;

    /// <summary>
    /// Creates a motion-vector CDF context initialized from the default tables.
    /// </summary>
    /// <returns>A fresh, mutable motion-vector CDF context.</returns>
    public static Av1MotionVectorCdfContext CreateDefault() => new()
    {
        Joint = (ushort[])Av1DefaultMotionVectorCdf.Joint.Clone(),
        Components = [Component.CreateDefault(), Component.CreateDefault()],
    };

    /// <summary>
    /// The adaptive CDFs for a single motion-vector component (row or column).
    /// </summary>
    public sealed class Component
    {
        private Component()
        {
        }

        /// <summary>Gets the sign bit CDF.</summary>
        public ushort[] Sign { get; private set; } = default!;

        /// <summary>Gets the magnitude-class CDF (11 symbols).</summary>
        public ushort[] Classes { get; private set; } = default!;

        /// <summary>Gets the class-0 integer bit CDF.</summary>
        public ushort[] Class0 { get; private set; } = default!;

        /// <summary>Gets the class-N integer bit CDFs, indexed by bit position [0, 9].</summary>
        public ushort[][] ClassN { get; private set; } = default!;

        /// <summary>Gets the class-0 fractional-pel CDFs, indexed by class-0 integer bit.</summary>
        public ushort[][] Class0Fp { get; private set; } = default!;

        /// <summary>Gets the class-N fractional-pel CDF (4 symbols).</summary>
        public ushort[] ClassNFp { get; private set; } = default!;

        /// <summary>Gets the class-0 high-precision bit CDF.</summary>
        public ushort[] Class0Hp { get; private set; } = default!;

        /// <summary>Gets the class-N high-precision bit CDF.</summary>
        public ushort[] ClassNHp { get; private set; } = default!;

        /// <summary>Creates a component initialized from the default tables.</summary>
        /// <returns>A fresh, mutable component CDF set.</returns>
        public static Component CreateDefault() => new()
        {
            Sign = (ushort[])Av1DefaultMotionVectorCdf.Sign.Clone(),
            Classes = (ushort[])Av1DefaultMotionVectorCdf.Classes.Clone(),
            Class0 = (ushort[])Av1DefaultMotionVectorCdf.Class0.Clone(),
            ClassN = Clone(Av1DefaultMotionVectorCdf.ClassN),
            Class0Fp = Clone(Av1DefaultMotionVectorCdf.Class0Fp),
            ClassNFp = (ushort[])Av1DefaultMotionVectorCdf.ClassNFp.Clone(),
            Class0Hp = (ushort[])Av1DefaultMotionVectorCdf.Class0Hp.Clone(),
            ClassNHp = (ushort[])Av1DefaultMotionVectorCdf.ClassNHp.Clone(),
        };

        private static ushort[][] Clone(ushort[][] group)
        {
            ushort[][] result = new ushort[group.Length][];
            for (int i = 0; i < group.Length; i++)
            {
                result[i] = (ushort[])group[i].Clone();
            }

            return result;
        }
    }
}
