// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Obu;
using SixLabors.ImageSharp.Formats.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Av1.Bitstream;

/// <summary>
/// Decodes the tiles of an inter frame, reusing the shared reconstruction and post-filter pipeline of
/// <see cref="Av1TileDecoder"/> and overriding only the per-block prediction. Each block reads its skip
/// flag and is-inter flag; inter blocks then decode the reference, motion vector and filters
/// (<see cref="Av1InterModeInfoDecoder"/>), motion-compensate from the reference frame
/// (<see cref="Av1InterPredictor"/>) and add the residual through the shared transform-block loop. The
/// implemented subset is single-reference, error-resilient frames (default CDFs, no temporal motion
/// vectors, the <c>TX_MODE_LARGEST</c> transform mode and a fixed interpolation filter); other syntax
/// raises <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class Av1InterTileDecoder : Av1TileDecoder
{
    private readonly Av1Plane referenceLuma;
    private readonly Av1Plane? referenceChromaU;
    private readonly Av1Plane? referenceChromaV;

    private readonly Av1InterModeCdfContext interCdf = Av1InterModeCdfContext.CreateDefault();
    private readonly Av1MotionVectorCdfContext mvCdf = Av1MotionVectorCdfContext.CreateDefault();
    private readonly Av1InterpolationFilterCdfContext filterCdf = Av1InterpolationFilterCdfContext.CreateDefault();
    private readonly Av1MotionModeCdfContext motionModeCdf = Av1MotionModeCdfContext.CreateDefault();
    private readonly Av1MotionVectorGrid grid;
    private readonly Av1InterNeighbourContext interNeighbours;
    private readonly Av1InterModeInfoOptions options;
    private readonly int interpolationFilter;

    // The current block's prediction source: set true around an inter block so the Predict override
    // substitutes the motion-compensated samples instead of an intra prediction.
    private bool currentBlockIsInter;

    /// <summary>Initializes a new instance of the <see cref="Av1InterTileDecoder"/> class.</summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The inter frame header.</param>
    /// <param name="reference">The single reference frame (used for every reference slot in this subset).</param>
    public Av1InterTileDecoder(in ObuSequenceHeader sequenceHeader, in ObuFrameHeader frameHeader, Av1ReferenceFrame reference)
        : base(sequenceHeader, frameHeader)
    {
        if (frameHeader.PrimaryRefFrame != 7)
        {
            throw new NotSupportedException("Inter frames with a primary reference frame are not supported yet.");
        }

        if (frameHeader.TxMode == 2)
        {
            throw new NotSupportedException("The variable-transform (TX_MODE_SELECT) inter path is not supported yet.");
        }

        if (frameHeader.InterpolationFilter == 4)
        {
            throw new NotSupportedException("Switchable interpolation filters are not supported yet.");
        }

        if (frameHeader.ReferenceSelect)
        {
            throw new NotSupportedException("Compound (two-reference) prediction is not supported yet.");
        }

        this.referenceLuma = reference.Luma;
        this.referenceChromaU = reference.ChromaU;
        this.referenceChromaV = reference.ChromaV;
        this.interpolationFilter = frameHeader.InterpolationFilter;

        int columns4 = frameHeader.ModeInfoColumns;
        int rows4 = frameHeader.ModeInfoRows;
        this.grid = new Av1MotionVectorGrid(columns4, rows4);
        this.interNeighbours = new Av1InterNeighbourContext(columns4, rows4);
        this.options = new Av1InterModeInfoOptions(
            new Av1TileBounds(0, columns4, 0, rows4),
            columns4,
            rows4,
            allowHighPrecisionMv: frameHeader.AllowHighPrecisionMv,
            forceIntegerMv: false,
            filterSwitchable: false,
            dualFilter: false,
            fixedFilter: frameHeader.InterpolationFilter,
            globalMv: default,
            globalMvSubstitution: false,
            globalMvIsTranslation: false,
            signBias: new int[7]);
    }

    private protected override void DecodeBlock(int row, int col, Av1BlockSize bsize)
    {
        int width4 = bsize.GetWidth4();
        int height4 = bsize.GetHeight4();
        bool haveTop = row > 0;
        bool haveLeft = col > 0;

        int skip = this.ReadSkipFlag(row, col, width4, height4);

        int intraContext = Av1IsInterReader.GetIntraContext(
            this.interNeighbours.LeftIntra(row), this.interNeighbours.AboveIntra(col), haveLeft, haveTop);
        bool isInter = Av1IsInterReader.ReadIsInter(this.decoder, this.interCdf, intraContext);
        if (!isInter)
        {
            throw new NotSupportedException("Intra blocks inside inter frames are not supported yet.");
        }

        Av1InterBlockInfo info = Av1InterModeInfoDecoder.Decode(
            this.decoder,
            this.interCdf,
            this.mvCdf,
            this.filterCdf,
            this.motionModeCdf,
            this.grid,
            this.interNeighbours,
            col,
            row,
            bsize,
            this.options,
            haveTop,
            haveLeft,
            topRightAvailable: false,
            readMotionMode: false,
            allowWarp: false,
            skipMode: false);

        // Motion-compensate every plane from the reference frame into the output planes.
        this.MotionCompensate(this.luma, this.referenceLuma, row, col, width4, height4, info, 0, 0);
        bool hasChroma = this.sequenceHeader.NumPlanes > 1 &&
                         (width4 > this.subsamplingX || (col & 1) != 0) &&
                         (height4 > this.subsamplingY || (row & 1) != 0);
        if (hasChroma && this.referenceChromaU is not null && this.referenceChromaV is not null)
        {
            this.MotionCompensate(this.chromaU, this.referenceChromaU, row, col, width4, height4, info, this.subsamplingX, this.subsamplingY);
            this.MotionCompensate(this.chromaV, this.referenceChromaV, row, col, width4, height4, info, this.subsamplingX, this.subsamplingY);
        }

        // Add the residual through the shared transform-block loop, substituting the motion-compensated
        // prediction via the Predict override. A skipped block carries no residual.
        bool blockSkip = skip != 0;
        this.currentBlockIsInter = true;
        Av1TransformSize lumaTx = bsize.GetMaxTransformSize();
        this.DecodePlane(this.luma, this.lumaLevels, 0, row, col, bsize, lumaTx, 0, 0, -1, 0, blockSkip);
        if (hasChroma)
        {
            Av1TransformSize chromaTx = bsize.GetMaxChromaTransformSize(this.sequenceHeader);
            int chromaRow = row >> this.subsamplingY;
            int chromaCol = col >> this.subsamplingX;
            this.DecodePlane(this.chromaU, this.chromaULevels, 1, chromaRow, chromaCol, bsize, chromaTx, 0, 0, -1, 0, blockSkip);
            this.DecodePlane(this.chromaV, this.chromaVLevels, 2, chromaRow, chromaCol, bsize, chromaTx, 0, 0, -1, 0, blockSkip);
        }

        this.currentBlockIsInter = false;

        // Record the shared skip neighbour context for the next block's skip flag.
        Fill(this.aboveSkip, col, width4, (byte)skip);
        Fill(this.leftSkip, row, height4, (byte)skip);
    }

    private protected override void Predict(Av1Plane plane, int x, int y, int width, int height, int intraMode, int angleDelta, int filterIntraMode, int cflAlpha, byte[] prediction)
    {
        if (!this.currentBlockIsInter)
        {
            base.Predict(plane, x, y, width, height, intraMode, angleDelta, filterIntraMode, cflAlpha, prediction);
            return;
        }

        // The block is already motion-compensated into the plane; copy those samples as the prediction.
        for (int ry = 0; ry < height; ry++)
        {
            for (int rx = 0; rx < width; rx++)
            {
                byte sample = (x + rx < plane.Width && y + ry < plane.Height) ? plane[x + rx, y + ry] : (byte)0;
                prediction[(ry * width) + rx] = sample;
            }
        }
    }

    private void MotionCompensate(Av1Plane destination, Av1Plane reference, int row, int col, int width4, int height4, in Av1InterBlockInfo info, int subsamplingX, int subsamplingY)
    {
        int x = (col >> subsamplingX) * 4;
        int y = (row >> subsamplingY) * 4;
        int destinationOffset = (y * destination.Width) + x;
        Av1InterPredictor.Predict(
            destination.Samples,
            destinationOffset,
            destination.Width,
            reference.Samples,
            reference.Width,
            reference.Height,
            reference.Width,
            col,
            row,
            width4,
            height4,
            info.MotionVector,
            info.Filter0,
            info.Filter1,
            subsamplingX,
            subsamplingY);
    }
}
