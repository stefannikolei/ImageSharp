// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The complete set of adaptive CDF contexts a frame decodes with (the reference decoder's
/// <c>CdfContext</c>). A frame with <c>primary_ref_frame</c> set to NONE starts from the default tables
/// (the coefficient defaults depend on the base quantizer); otherwise it starts from a copy of the state
/// its primary reference saved at its frame end, so adaptation carries across frames.
/// </summary>
internal sealed class Av1FrameCdfSet
{
    private Av1FrameCdfSet()
    {
    }

    /// <summary>Gets the mode-info CDFs (partition, skip, intra modes, transform size/type).</summary>
    public Av1ModeInfoCdfContext ModeInfo { get; private set; } = default!;

    /// <summary>Gets the coefficient CDFs.</summary>
    public Av1CoefficientCdfContext Coefficient { get; private set; } = default!;

    /// <summary>Gets the inter mode CDFs (is-inter, references, prediction modes).</summary>
    public Av1InterModeCdfContext InterMode { get; private set; } = default!;

    /// <summary>Gets the motion-vector CDFs.</summary>
    public Av1MotionVectorCdfContext MotionVector { get; private set; } = default!;

    /// <summary>Gets the interpolation-filter CDFs.</summary>
    public Av1InterpolationFilterCdfContext Filter { get; private set; } = default!;

    /// <summary>Gets the motion-mode CDFs.</summary>
    public Av1MotionModeCdfContext MotionMode { get; private set; } = default!;

    /// <summary>Gets the inter transform-type CDFs.</summary>
    public Av1InterTransformTypeCdfContext InterTransformType { get; private set; } = default!;

    /// <summary>Gets the variable-transform partition-split CDFs, indexed by category then context.</summary>
    public ushort[][][] TransformPartition { get; private set; } = default!;

    /// <summary>Creates a set initialized from the default tables.</summary>
    /// <param name="baseQIndex">The frame's base quantizer index (selects the coefficient defaults).</param>
    /// <returns>A fresh, mutable CDF set.</returns>
    public static Av1FrameCdfSet CreateDefault(int baseQIndex) => new()
    {
        ModeInfo = Av1ModeInfoCdfContext.CreateDefault(),
        Coefficient = Av1CoefficientCdfContext.CreateDefault(GetQuantizerContext(baseQIndex)),
        InterMode = Av1InterModeCdfContext.CreateDefault(),
        MotionVector = Av1MotionVectorCdfContext.CreateDefault(),
        Filter = Av1InterpolationFilterCdfContext.CreateDefault(),
        MotionMode = Av1MotionModeCdfContext.CreateDefault(),
        InterTransformType = Av1InterTransformTypeCdfContext.CreateDefault(),
        TransformPartition = Av1CdfTables.Copy(Av1DefaultTransformPartitionCdf.Split),
    };

    /// <summary>Creates a deep copy of this set (loading a primary reference's saved state).</summary>
    /// <returns>An independent copy.</returns>
    public Av1FrameCdfSet Clone() => new()
    {
        ModeInfo = this.ModeInfo.Clone(),
        Coefficient = this.Coefficient.Clone(),
        InterMode = this.InterMode.Clone(),
        MotionVector = this.MotionVector.Clone(),
        Filter = this.Filter.Clone(),
        MotionMode = this.MotionMode.Clone(),
        InterTransformType = this.InterTransformType.Clone(),
        TransformPartition = Av1CdfTables.Copy(this.TransformPartition),
    };

    /// <summary>
    /// Zeroes every CDF's adaptation counter, part of saving the frame-end state (the reference
    /// decoder's <c>cdf_thread_update</c>): a frame inheriting the state keeps the adapted
    /// probabilities but restarts adaptation at the initial rate.
    /// </summary>
    public void ResetCounters()
    {
        Av1ModeInfoCdfContext m = this.ModeInfo;
        Av1CdfTables.ResetCounters(m.Skip);
        Av1CdfTables.ResetCounters(m.SegId);
        Av1CdfTables.ResetCounters(m.SegPred);
        Av1CdfTables.ResetCounter(m.DeltaQ);
        Av1CdfTables.ResetCounters(m.DeltaLf);
        Av1CdfTables.ResetCounters(m.Partition);
        Av1CdfTables.ResetCounters(m.KeyFrameYMode);
        Av1CdfTables.ResetCounters(m.YMode);
        Av1CdfTables.ResetCounters(m.UvMode);
        Av1CdfTables.ResetCounters(m.UseFilterIntra);
        Av1CdfTables.ResetCounter(m.FilterIntraMode);
        Av1CdfTables.ResetCounters(m.TransformDepth);
        Av1CdfTables.ResetCounters(m.TransformTypeIntra1);
        Av1CdfTables.ResetCounters(m.TransformTypeIntra2);
        Av1CdfTables.ResetCounters(m.AngleDelta);
        Av1CdfTables.ResetCounter(m.CflSign);
        Av1CdfTables.ResetCounters(m.CflAlpha);
        Av1CdfTables.ResetCounter(m.RestoreWiener);
        Av1CdfTables.ResetCounter(m.RestoreSgrProj);
        Av1CdfTables.ResetCounter(m.RestoreSwitchable);

        Av1CoefficientCdfContext c = this.Coefficient;
        Av1CdfTables.ResetCounters(c.Skip);
        Av1CdfTables.ResetCounters(c.DcSign);
        Av1CdfTables.ResetCounters(c.EobHighBit);
        Av1CdfTables.ResetCounters(c.BaseToken);
        Av1CdfTables.ResetCounters(c.BaseRange);
        Av1CdfTables.ResetCounters(c.EobBaseToken);
        Av1CdfTables.ResetCounters(c.EobBin);

        Av1InterModeCdfContext i = this.InterMode;
        Av1CdfTables.ResetCounters(i.IsInter);
        Av1CdfTables.ResetCounters(i.SkipMode);
        Av1CdfTables.ResetCounters(i.NewMv);
        Av1CdfTables.ResetCounters(i.GlobalMv);
        Av1CdfTables.ResetCounters(i.RefMv);
        Av1CdfTables.ResetCounters(i.DrlBit);
        Av1CdfTables.ResetCounters(i.Compound);
        Av1CdfTables.ResetCounters(i.SingleReference);
        Av1CdfTables.ResetCounters(i.CompoundDirection);
        Av1CdfTables.ResetCounters(i.CompoundForwardReference);
        Av1CdfTables.ResetCounters(i.CompoundBackwardReference);
        Av1CdfTables.ResetCounters(i.CompoundUniReference);
        Av1CdfTables.ResetCounters(i.CompoundInterMode);

        Av1CdfTables.ResetCounter(this.MotionVector.Joint);
        foreach (Av1MotionVectorCdfContext.Component component in this.MotionVector.Components)
        {
            Av1CdfTables.ResetCounter(component.Sign);
            Av1CdfTables.ResetCounter(component.Classes);
            Av1CdfTables.ResetCounter(component.Class0);
            Av1CdfTables.ResetCounters(component.ClassN);
            Av1CdfTables.ResetCounters(component.Class0Fp);
            Av1CdfTables.ResetCounter(component.ClassNFp);
            Av1CdfTables.ResetCounter(component.Class0Hp);
            Av1CdfTables.ResetCounter(component.ClassNHp);
        }

        Av1CdfTables.ResetCounters(this.Filter.Filter);
        Av1CdfTables.ResetCounters(this.MotionMode.MotionMode);
        Av1CdfTables.ResetCounters(this.MotionMode.Obmc);
        Av1CdfTables.ResetCounters(this.InterTransformType.Set1);
        Av1CdfTables.ResetCounter(this.InterTransformType.Set2);
        Av1CdfTables.ResetCounters(this.InterTransformType.Set3);
        Av1CdfTables.ResetCounters(this.TransformPartition);
    }

    private static int GetQuantizerContext(int baseQIndex)
        => baseQIndex <= 20 ? 0 : baseQIndex <= 60 ? 1 : baseQIndex <= 120 ? 2 : 3;
}
