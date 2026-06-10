// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Av1.Transform;

/// <summary>
/// The 2D transform types defined by AV1 (specification section 6.10.x). Each type names the
/// vertical transform followed by the horizontal transform. The numeric order matches the AV1
/// reference.
/// </summary>
internal enum Av1TransformType
{
    /// <summary>DCT vertical, DCT horizontal.</summary>
    DctDct,

    /// <summary>ADST vertical, DCT horizontal.</summary>
    AdstDct,

    /// <summary>DCT vertical, ADST horizontal.</summary>
    DctAdst,

    /// <summary>ADST vertical, ADST horizontal.</summary>
    AdstAdst,

    /// <summary>FLIPADST vertical, DCT horizontal.</summary>
    FlipAdstDct,

    /// <summary>DCT vertical, FLIPADST horizontal.</summary>
    DctFlipAdst,

    /// <summary>FLIPADST vertical, FLIPADST horizontal.</summary>
    FlipAdstFlipAdst,

    /// <summary>ADST vertical, FLIPADST horizontal.</summary>
    AdstFlipAdst,

    /// <summary>FLIPADST vertical, ADST horizontal.</summary>
    FlipAdstAdst,

    /// <summary>Identity vertical, identity horizontal.</summary>
    Identity,

    /// <summary>DCT vertical, identity horizontal.</summary>
    VerticalDct,

    /// <summary>Identity vertical, DCT horizontal.</summary>
    HorizontalDct,

    /// <summary>ADST vertical, identity horizontal.</summary>
    VerticalAdst,

    /// <summary>Identity vertical, ADST horizontal.</summary>
    HorizontalAdst,

    /// <summary>FLIPADST vertical, identity horizontal.</summary>
    VerticalFlipAdst,

    /// <summary>Identity vertical, FLIPADST horizontal.</summary>
    HorizontalFlipAdst,
}
