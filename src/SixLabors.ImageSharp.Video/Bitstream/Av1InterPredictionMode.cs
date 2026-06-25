// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// The single-reference inter prediction modes (specification section 6.10.24, <c>YMode</c> inter
/// modes), matching the reference decoder's <c>InterPredMode</c> ordering.
/// </summary>
internal enum Av1InterPredictionMode
{
    /// <summary>Uses the nearest motion-vector predictor from the candidate list.</summary>
    NearestMv = 0,

    /// <summary>Uses a near motion-vector predictor selected by the dynamic reference list.</summary>
    NearMv = 1,

    /// <summary>Uses the frame's global-motion vector.</summary>
    GlobalMv = 2,

    /// <summary>Codes a new motion vector as a residual from a predictor.</summary>
    NewMv = 3,
}
