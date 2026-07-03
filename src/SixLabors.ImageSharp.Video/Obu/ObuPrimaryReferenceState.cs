// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Obu;

/// <summary>
/// The per-reference-slot header state a frame with <c>primary_ref_frame</c> inherits instead of the
/// specification defaults (part of the reference decoder's saved frame context): today the loop-filter
/// reference and mode deltas, which the header's <c>loop_filter_params</c> only optionally updates.
/// </summary>
/// <param name="LoopFilterRefDeltas">The eight per-reference loop-filter level deltas.</param>
/// <param name="LoopFilterModeDeltas">The two per-mode loop-filter level deltas.</param>
/// <param name="GlobalMotion">The seven per-reference global-motion models (the parameter deltas of a
/// frame with a primary reference are coded against these).</param>
internal sealed record ObuPrimaryReferenceState(int[] LoopFilterRefDeltas, int[] LoopFilterModeDeltas, Av1WarpedMotionParams[] GlobalMotion)
{
    /// <summary>Creates the specification-default state (<c>setup_past_independence</c>).</summary>
    /// <returns>The default state.</returns>
    public static ObuPrimaryReferenceState CreateDefault() => new(
        [1, 0, 0, 0, -1, 0, -1, -1],
        [0, 0],
        [Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity, Av1WarpedMotionParams.Identity]);
}
