// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Formats.Av1;

/// <summary>
/// One displayed frame of a decoded AV1 stream: the reconstructed planes of either a shown frame or a
/// previously decoded frame re-emitted by <c>show_existing_frame</c>.
/// </summary>
/// <param name="Luma">The luma plane.</param>
/// <param name="ChromaU">The chroma U plane.</param>
/// <param name="ChromaV">The chroma V plane.</param>
internal sealed record Av1DisplayFrame(Av1Plane Luma, Av1Plane ChromaU, Av1Plane ChromaV);
