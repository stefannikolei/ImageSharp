// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// End-to-end round-trip validation of the single-reference inter mode-info decoder
/// (<see cref="Av1InterModeInfoDecoder"/>). A test-only encoder writes the exact reference / mode / MV
/// residual / filter symbol sequence the decoder reads, over a controlled reference grid and neighbour
/// context; the decoder must recover the reference, mode, motion vector and filters, and write the
/// block back into the grid and neighbour state.
/// </summary>
public class Av1InterModeInfoDecoderTests
{
    private const int Bx4 = 8;
    private const int By4 = 8;

    private static Av1InterModeInfoOptions Options() => new(
        new Av1TileBounds(0, 32, 0, 32),
        imageWidth4: 32,
        imageHeight4: 32,
        allowHighPrecisionMv: true,
        forceIntegerMv: false,
        filterSwitchable: true,
        dualFilter: false,
        fixedFilter: 0,
        globalMotion: CreateIdentityGlobalMotion(),
        signBias: new int[7]);

    private static Formats.Av1.Obu.Av1WarpedMotionParams[] CreateIdentityGlobalMotion()
    {
        Formats.Av1.Obu.Av1WarpedMotionParams[] models = new Formats.Av1.Obu.Av1WarpedMotionParams[7];
        Array.Fill(models, Formats.Av1.Obu.Av1WarpedMotionParams.Identity);
        return models;
    }

    private static (Av1MotionVectorGrid Grid, Av1InterNeighbourContext Neighbours) BuildScene()
    {
        Av1MotionVectorGrid grid = new(32, 32);
        Av1RefMvsBlock intra = new(default, default, 0, -1, Av1BlockSize.Block8x8, false, false, isIntra: true);
        grid.Fill(0, 0, 32, 32, intra);

        // A matching top neighbour (reference 0, one-based 1) at row 7, columns 8-9 with MV (0, 32).
        Av1RefMvsBlock top = new(new Av1MotionVector(0, 32), default, 1, -1, Av1BlockSize.Block8x8, false, false, isIntra: false);
        grid.Fill(7, 8, 2, 1, top);

        Av1InterNeighbourContext neighbours = new(32, 32);
        neighbours.Write(7, 8, 2, 1, isIntra: false, reference0: 0, reference1: -1, isCompound: false, filter0: 1, filter1: 1, skipMode: false);
        return (grid, neighbours);
    }

    [Fact]
    public void Decode_NewMvSingleReference_RoundTrips()
    {
        Av1InterModeInfoOptions options = Options();
        (Av1MotionVectorGrid grid, Av1InterNeighbourContext neighbours) = BuildScene();

        // Derive the plan the encoder must follow (these reads do not consume entropy).
        Av1ReferenceNeighbour above = neighbours.GetAbove(Bx4);
        Av1ReferenceNeighbour left = neighbours.GetLeft(By4);
        int[] referenceContexts = Av1ReferenceContext.ComputeSingleReferenceContexts(above, left, haveTop: true, haveLeft: true);

        Av1MotionVectorStack planStack = new();
        (int count, int modeContext) = Av1MotionVectorFinder.Find(
            grid, planStack, Bx4, By4, Av1BlockSize.Block8x8, referenceFrame: 1, options.Bounds,
            topRightAvailable: false, options.ImageWidth4, options.ImageHeight4, globalMv: default, globalMvSubstitution: false, options.SignBias);
        int horizontalContext = Av1ReferenceContext.ComputeFilterContext(above, left, isCompound: false, direction: 0, reference: 0);

        Av1MotionVector predictor = planStack[0].MotionVector; // count == 1, no precision change for these values
        Av1MotionVector target = new(predictor.Y, predictor.X + 32);

        // Encode the symbol sequence.
        Av1SymbolEncoder encoder = new();
        Av1InterModeCdfContext interCdf = Av1InterModeCdfContext.CreateDefault();
        Av1MotionVectorCdfContext mvCdf = Av1MotionVectorCdfContext.CreateDefault();
        Av1InterpolationFilterCdfContext filterCdf = Av1InterpolationFilterCdfContext.CreateDefault();

        // Reference 0: lower half (bit 0), refs 0/1 (bit 0), ref 0 (bit 0).
        encoder.WriteSymbol(0, interCdf.SingleReference[0][referenceContexts[0]]);
        encoder.WriteSymbol(0, interCdf.SingleReference[2][referenceContexts[2]]);
        encoder.WriteSymbol(0, interCdf.SingleReference[3][referenceContexts[3]]);

        // NEWMV (newmv flag = 0); count == 1 so no DRL bits.
        encoder.WriteSymbol(0, interCdf.NewMv[modeContext & 7]);

        // MV residual: horizontal-only diff of +32.
        encoder.WriteSymbol(1, mvCdf.Joint); // MV_JOINT_H
        EncodeComponentDiff(encoder, mvCdf.Components[1], target.X - predictor.X);

        // Switchable filter (single direction), choose SHARP (2).
        encoder.WriteSymbol(2, filterCdf.Filter[0][horizontalContext]);

        byte[] payload = encoder.Finish();

        // Decode.
        Av1SymbolDecoder decoder = new(payload);
        Av1InterModeCdfContext decInterCdf = Av1InterModeCdfContext.CreateDefault();
        Av1MotionVectorCdfContext decMvCdf = Av1MotionVectorCdfContext.CreateDefault();
        Av1InterpolationFilterCdfContext decFilterCdf = Av1InterpolationFilterCdfContext.CreateDefault();
        Av1MotionModeCdfContext decMotionCdf = Av1MotionModeCdfContext.CreateDefault();

        Av1InterBlockInfo info = Av1InterModeInfoDecoder.Decode(
            decoder, decInterCdf, decMvCdf, decFilterCdf, decMotionCdf, grid, neighbours,
            Bx4, By4, Av1BlockSize.Block8x8, options,
            haveTop: true, haveLeft: true, topRightAvailable: false,
            readMotionMode: false, skipMode: false);

        Assert.Equal(0, info.Reference);
        Assert.Equal(Av1InterPredictionMode.NewMv, info.Mode);
        Assert.Equal(0, info.DynamicReferenceIndex);
        Assert.Equal(target.Y, info.MotionVector.Y);
        Assert.Equal(target.X, info.MotionVector.X);
        Assert.Equal(2, info.Filter0);
        Assert.Equal(2, info.Filter1);
        Assert.Equal(Av1MotionMode.Translation, info.MotionMode);

        // The block was written back into the grid and neighbour context.
        Av1RefMvsBlock written = grid[By4, Bx4];
        Assert.False(written.IsIntra);
        Assert.Equal(1, written.Reference0); // one-based
        Assert.True(written.IsNewMv);
        Assert.Equal(target.X, written.MotionVector0.X);

        Av1ReferenceNeighbour writtenNeighbour = neighbours.GetAbove(Bx4);
        Assert.False(writtenNeighbour.IsIntra);
        Assert.Equal(0, writtenNeighbour.Reference0);
        Assert.Equal(2, writtenNeighbour.Filter0);
    }

    private static void EncodeComponentDiff(Av1SymbolEncoder encoder, Av1MotionVectorCdfContext.Component component, int diff)
    {
        int sign = diff < 0 ? 1 : 0;
        int magnitude = Math.Abs(diff) - 1;
        int hp = magnitude & 1;
        int fp = (magnitude >> 1) & 3;
        int up = magnitude >> 3;
        int classIndex = up < 2 ? 0 : (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)up));

        encoder.WriteSymbol(sign, component.Sign);
        encoder.WriteSymbol(classIndex, component.Classes);
        if (classIndex == 0)
        {
            encoder.WriteSymbol(up, component.Class0);
            encoder.WriteSymbol(fp, component.Class0Fp[up]);
            encoder.WriteSymbol(hp, component.Class0Hp);
        }
        else
        {
            for (int n = 0; n < classIndex; n++)
            {
                encoder.WriteSymbol((up >> n) & 1, component.ClassN[n]);
            }

            encoder.WriteSymbol(fp, component.ClassNFp);
            encoder.WriteSymbol(hp, component.ClassNHp);
        }
    }
}
