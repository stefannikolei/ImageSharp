// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the single-reference inter prediction mode and dynamic-reference-list index, a port of the
/// reference decoder's inter-mode parsing (<c>decode.c</c>, the new/global/ref-mv flag cascade with
/// DRL bits). The reader consumes only the entropy-coded syntax; selecting the predictor motion vector
/// from the candidate list and reading any new-motion-vector residual is left to the caller.
/// </summary>
internal static class Av1InterModeReader
{
    // Dynamic-reference-list indices (specification: NEAREST_DRL .. NEARISH_DRL).
    private const int NearestDrl = 0;
    private const int NearerDrl = 1;
    private const int NearDrl = 2;

    // The candidate weight threshold used to derive dynamic-reference-list contexts.
    private const int WeightThreshold = 640;

    /// <summary>
    /// Reads the inter prediction mode and dynamic-reference-list index.
    /// </summary>
    /// <param name="decoder">The tile symbol decoder.</param>
    /// <param name="cdf">The tile's adaptive inter-mode CDFs.</param>
    /// <param name="context">The mode context returned by the MV prediction process.</param>
    /// <param name="candidateCount">The number of candidates in the dynamic reference list.</param>
    /// <param name="candidates">The dynamic reference list (used for DRL contexts).</param>
    /// <param name="forceGlobalMv">
    /// Whether segmentation forces the global-motion mode (segment skip or global-mv feature).
    /// </param>
    /// <returns>The decoded inter prediction mode and dynamic-reference-list index.</returns>
    public static (Av1InterPredictionMode Mode, int DynamicReferenceIndex) ReadMode(
        Av1SymbolDecoder decoder,
        Av1InterModeCdfContext cdf,
        int context,
        int candidateCount,
        ReadOnlySpan<Av1MotionVectorCandidate> candidates,
        bool forceGlobalMv)
    {
        if (forceGlobalMv || decoder.ReadSymbol(cdf.NewMv[context & 7]) != 0)
        {
            if (forceGlobalMv || decoder.ReadSymbol(cdf.GlobalMv[(context >> 3) & 1]) == 0)
            {
                return (Av1InterPredictionMode.GlobalMv, NearestDrl);
            }

            if (decoder.ReadSymbol(cdf.RefMv[(context >> 4) & 15]) != 0)
            {
                int drl = NearerDrl;
                if (candidateCount > 2)
                {
                    drl += decoder.ReadSymbol(cdf.DrlBit[GetDrlContext(candidates, 1)]);
                    if (drl == NearDrl && candidateCount > 3)
                    {
                        drl += decoder.ReadSymbol(cdf.DrlBit[GetDrlContext(candidates, 2)]);
                    }
                }

                return (Av1InterPredictionMode.NearMv, drl);
            }

            return (Av1InterPredictionMode.NearestMv, NearestDrl);
        }
        else
        {
            int drl = NearestDrl;
            if (candidateCount > 1)
            {
                drl += decoder.ReadSymbol(cdf.DrlBit[GetDrlContext(candidates, 0)]);
                if (drl == NearerDrl && candidateCount > 2)
                {
                    drl += decoder.ReadSymbol(cdf.DrlBit[GetDrlContext(candidates, 1)]);
                }
            }

            return (Av1InterPredictionMode.NewMv, drl);
        }
    }

    /// <summary>
    /// Derives the dynamic-reference-list bit context from two adjacent candidate weights, matching the
    /// reference decoder's <c>get_drl_context</c>.
    /// </summary>
    /// <param name="candidates">The dynamic reference list.</param>
    /// <param name="referenceIndex">The candidate index to evaluate.</param>
    /// <returns>The DRL bit context.</returns>
    public static int GetDrlContext(ReadOnlySpan<Av1MotionVectorCandidate> candidates, int referenceIndex)
    {
        bool nextBelow = candidates[referenceIndex + 1].Weight < WeightThreshold;
        if (candidates[referenceIndex].Weight >= WeightThreshold)
        {
            return nextBelow ? 1 : 0;
        }

        return nextBelow ? 2 : 0;
    }
}
