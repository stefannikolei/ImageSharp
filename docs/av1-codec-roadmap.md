# Roadmap: Pure-Managed AV1 Video Codec for ImageSharp

> Status: **early foundation** (Phase 0–1 in progress). This document tracks the long-term
> plan for adding AV1 (AOMedia Video 1) decoding to ImageSharp as a separate, opt-in package.

## Context

The goal is to extend ImageSharp with video capability by implementing the AV1 codec **purely
in managed C#** (no native interop), consistent with ImageSharp's fully-managed design. AV1 was
chosen over the newer AV2 because its bitstream specification is finalized and stable, whereas
AV2 (v1.0.0) is brand new and has no existing C# reference to validate against. Once AV1 is
mature, AV2 can be added as a second format in the same project.

### Why this is feasible

- ImageSharp is **100% managed** (no `DllImport`/P-Invoke); the only dependency is `System.IO.Hashing`.
- A mature, cross-platform **SIMD infrastructure** already exists
  (`src/ImageSharp/Common/Helpers/SimdUtils.HwIntrinsics.cs`, `Vector{128,256,512}Utilities.cs`)
  covering SSE2/AVX2/AVX-512/ARM AdvSimd with scalar fallbacks.
- The **WebP/VP8 decoder** (`src/ImageSharp/Formats/Webp/Lossy/`) is the direct codec ancestor of
  AV1 (boolean entropy coder, macroblocks, intra prediction, loop filter, YUV→RGB) and serves as a
  style reference. The **JPEG decoder** (`src/ImageSharp/Formats/Jpeg/`) is the reference for
  complex entropy → dequant → transform → colour pipelines including SIMD variants.
- The frame model already carries video semantics: `ImageFrameCollection<TPixel>`,
  `Image<TPixel>.Frames`, and `FormatConnectingFrameMetadata` (`Duration`, `BlendMode`, `DisposalMode`).

### Scope and effort

A full AV1 **decoder** (scalar + SIMD) is realistically **25k–60k LOC** and **6–9+ months**. An
encoder would roughly double that and is **out of scope**. The work is split into incrementally
shippable, independently testable phases.

## Project layout

```
src/SixLabors.ImageSharp.Codecs.Av1/        <- new project, ProjectReference to ImageSharp
tests/SixLabors.ImageSharp.Codecs.Av1.Tests/
```

Registration is **opt-in** via `Av1ConfigurationModule : IImageFormatConfigurationModule`; the
module is intentionally *not* added to `Configuration.CreateDefaultInstance()`, so the lean core
keeps no video dependency.

### Two layers

1. **Container / demuxer** — separates coded frames (temporal units) from the file stream. Entry
   points: **IVF** (trivial AV1 test container) and the **OBU low-overhead bitstream**; later
   **WebM/Matroska** and **MP4/ISOBMFF** (`av01`).
2. **AV1 decoder** — turns OBUs into decoded YUV frames mapped onto `ImageFrame<TPixel>`.

## Reuse of existing ImageSharp patterns

| Need | Existing pattern |
|---|---|
| Format definition | `IImageFormat` / `IImageFormat<TMeta,TFrameMeta>` (`Formats/Webp/WebpFormat.cs`) |
| Decoder entry | `SpecializedImageDecoder<T>` (`Formats/Webp/WebpDecoder.cs`) |
| Stateful core | `ImageDecoderCore` (`Formats/ImageDecoderCore.cs`) |
| Magic-byte detection | `IImageFormatDetector` (`Formats/Png/PngImageFormatDetector.cs`) |
| Registration | `IImageFormatConfigurationModule` (`Formats/Png/PngConfigurationModule.cs`) |
| Multi-frame + timing | `ImageFrameCollection<TPixel>`, `FormatConnectingFrameMetadata` |
| Boolean/arithmetic bit reading | `Formats/Webp/BitReader/Vp8BitReader.cs` |
| Transform/block SIMD | `Formats/Jpeg/Components/Block8x8F*.cs`, `FloatingPointDCT.cs` |
| YUV→RGB with SIMD variants | `Formats/Webp/Lossy/YuvConversion.cs` |
| Aligned buffers / memory | `Memory/Allocators/` (`IMemoryOwner<T>`, `UnmanagedBuffer`) |

### Known friction points (address early)

- **High bit depth:** AV1 supports 8/10/12-bit; ImageSharp pixels are 8- or 16-bit/channel. Map
  10/12-bit onto `Rgb48`/`Rgba64`, 8-bit onto `Rgba32`, via the generic `Decode<TPixel>()` path.
- **Colour:** native YUV 4:2:0/4:2:2/4:4:4 + monochrome; convert to RGB as in `YuvConversion.cs`;
  pass CICP/colour primaries through as metadata.
- **Video semantics:** inter prediction needs reference-frame buffers and decode/display reorder.
  These decoder-internal buffers live outside `ImageFrameCollection`; only finished display frames
  are added to it.

## Phase plan

- **Phase 0 — Project scaffolding & format plumbing.** New project + test project; `Av1Format`,
  `Av1Decoder`, `Av1ImageFormatDetector` (IVF `DKIF` magic), `Av1ConfigurationModule`,
  `Av1Constants`. Stub decode: detect format, parse headers, report dimensions via `Identify`.
- **Phase 1 — Container demuxer + OBU parsing.** IVF reader; OBU framing (`obu_header`, `leb128`
  size, OBU types); `Av1BitStreamReader`; sequence-header parsing for dimensions / profile.
- **Phase 2 — Entropy decoder.** AV1 multi-symbol arithmetic (MSAC) decoder with CDF adaptation;
  tile/symbol context models.
- **Phase 3 — Dequant + transforms.** Transform family (DCT/ADST/FLIPADST/Identity) × sizes 4..64;
  inverse transforms; quantisation/dequant (scalar first).
- **Phase 4 — Intra prediction + reconstruction + loop filters.** Directional/DC/Paeth/Smooth/CfL;
  reconstruction; deblocking, CDEF, loop restoration. **Milestone: first fully decoded keyframe**
  from the raw coded stream. (Real `.avif` adds an ISOBMFF/HEIF container step on top of this.)
- **Phase 5 — Inter prediction (video).** Reference-frame management, motion vectors, 4/8-tap
  subpel filters, compound/OBMC/warped motion, decode→display reorder. **Milestone: multi-frame
  video decode** into `ImageFrameCollection` with `Duration`.
- **Phase 6 — Film grain synthesis + robustness.** Film grain, show-existing-frame, error handling.
- **Phase 7 — SIMD optimisation.** Hot paths as V128/V256/V512 variants with scalar fallback via
  the existing `SimdUtils` abstraction.
- **Phase 8 (optional) — more containers.** WebM/Matroska and MP4/ISOBMFF (`av01`) demuxers.

## Testing strategy

- xUnit; `ImageComparer.Tolerant()` for lossy comparisons (mirrors `tests/.../Formats/WebP/`).
- Unit tests per phase: bit-reader/`leb128`, OBU framing, sequence-header fields, inverse
  transforms, keyframe image, frame timing.
- Reference streams: small AV1 IVF vectors under `tests/Images/Input/Av1/`, compared against
  PNGs extracted with `libaom`/`dav1d`.
- Memory-leak checks via the existing `[ValidateDisposedMemoryAllocations]`.

## Current status (this branch)

Phases 0–2 implemented and unit-tested:
- **Phase 0–1:** `Av1BitStreamReader` (MSB-first `f(n)`, `uvlc`), `leb128`, OBU framing, IVF
  demuxer, sequence-header dimension parsing. Format registration + detector; `Identify` returns
  image dimensions for AV1/IVF input.
- **Phase 2:** `Av1SymbolDecoder` — the multi-symbol arithmetic (range) decoder (spec §8.2), a
  faithful port of the AV1 reference Daala entropy decoder, with `Av1Cdf` CDF adaptation (§8.3.2),
  plus equiprobable bool/literal reads. Validated by round-trip tests against a matching
  test-only `Av1SymbolEncoder` (symbols with/without adaptation across 2–16 values, bools,
  literals, and mixed streams).
- **Phase 3a:** `Av1InverseTransform1d` — fixed-point 1D inverse transforms (DCT 4/8/16 and
  identity 4/8/16/32) ported faithfully from the AV1 reference, with strided/in-place butterflies
  and clamping (spec §7.13.2). Validated against independent mathematical references (DCT-III with
  1/√2 DC scaling; exact identity scalings) plus DC-response and stride/offset tests.
- **Phase 3b:** inverse ADST/FLIPADST 4/8/16 (FLIPADST = ADST with reversed output) and the
  quantiser lookup tables + accessors for 8/10/12-bit (`Av1QuantizationLookup`, spec §7.12.2).
  ADST validated by transform-matrix orthogonality (a transcription error breaks orthogonality far
  beyond rounding noise) and the FLIPADST = reversed-ADST property; quant tables validated against
  known specification anchors plus monotonicity and bit-depth ordering.
- `Decode<TPixel>` deliberately throws `NotSupportedException` until the pixel pipeline
  (Phases 3–5) lands.

Next (Phase 3c): inverse DCT 32/64; the 2D inverse-transform driver (tx-type/size mapping,
row/column passes, intermediate shifts, rectangular scaling) and coefficient dequantisation.
Then Phase 4 (intra prediction → first decoded keyframe).
