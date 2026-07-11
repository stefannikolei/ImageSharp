// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates lossless decoding (<c>aomenc --lossless=1</c>, 128x128 clips): every block uses 4x4
/// Walsh-Hadamard transforms with an exactly invertible reconstruction, chroma-from-luma is limited
/// to single-4x4 chroma units, and no post-filters run. Covers an all-intra clip, an inter clip, a
/// screen-content clip with palette blocks and a 10-bit clip. Every displayed frame must be exactly
/// equal to dav1d's output (and thus the encoder's source), verified by per-frame SHA-256 digests
/// over the cropped planes.
/// </summary>
public class Av1LosslessDecodeTests
{
    private const string IntraIvfBase64 =
        "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";

    private static readonly string[] IntraFrameDigests = [
        "222b91907a2fad4eabf892c214f029afde66a158e044cc4aa65620ccd82085d7",
        "512bfecdacbf2577783a5ff45e92ff022a3896fc676df0592a8510a2972f93fc",
        "4d1e0105117964fc16eafca6d821824361e7519aa6559ca580f8dc090729d87b",
        "7e0d0c931e5fd2fd478876189da1d730f4f3946305a887cb3b5c92f529a4e289",
    ];

    private const string InterIvfBase64 =
        "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" +
        "RoAAQACFt7KA";

    private static readonly string[] InterFrameDigests = [
        "222b91907a2fad4eabf892c214f029afde66a158e044cc4aa65620ccd82085d7",
        "512bfecdacbf2577783a5ff45e92ff022a3896fc676df0592a8510a2972f93fc",
        "4d1e0105117964fc16eafca6d821824361e7519aa6559ca580f8dc090729d87b",
        "7e0d0c931e5fd2fd478876189da1d730f4f3946305a887cb3b5c92f529a4e289",
        "17172e420ac883941babc106db8f740a1ba2eefa5e9d14287d1300f592c7a2e8",
        "f5ce66d9b0d25b20db23a450304e10fd536db535c8c4cab2a5359cd5b5dfffd3",
        "0a77ebc4b0c137395ddf04a4efa5e7b6bd2ae0d320689948b2ac3ab03ccd6171",
        "032c91c0c48c2bc85992f779cbeea341a5ca7e8d2c880d929479cb67aaa5c20e",
    ];

    private const string PaletteIvfBase64 =
        "REtJRgAAIABBVjAxgACAAB4AAAABAAAABAAAAAAAAAAZAgAAAAAAAAAAAAASAAoKAAAAAzf/5tfMAjKIBBQAIAAA4EK1wl/6mfOT" +
        "fyH/NXnP+9pDVTevILXN797mjOXRmHJ2l51AXcV7q8Noe6/fTyz70f+09iR0BSYH+yol0G28BoUqi+5cjol95t4XfzkMe8Xn/lh8" +
        "sfTqkSv7SohHaYujnhjgg4YFDj73txiFrBOZAdhLj5I0bPM52M8neBbmynvssh26O5RsSH5fZB6sBXRuPTFxiL3XuVpdvQEI5oEv" +
        "+vNZQycrPtPUoS3mveHPY7SaXJyz9wnO864N18DtHveRs72nbD3jA0vhV/Vk15YN0IbchMMbBckl1Z8tJQcn3VM8SxdXLBkWREsl" +
        "DNnsIWlq8q0N6I7kS5WlY9YP4I/guPpvL71m0brs8IQswjz/a38ciJk/K8ms/ow5NkCoGVgSJeqoe4RbVRpfsVrI6zUXxe4fk71o" +
        "YzEvKb1fBt61ZaV3HPEMbA6a9aJ6d/ZBP6kRR3Cz3vG6sunCt9EAD30t+wiP3Qh6G4YG63a/X8PFAC3up7+FKNFqLuDrG3MPYKoM" +
        "DGrjMaNMgz16EApBLsoAe/Hrf5KCZBelPP6GLCMLpaLxrPLJzWJByMmkRaJlHJhb40ewvRDuZKusEb5xXL60FOLAldqIdxxa7K7r" +
        "eeGEbwBqgpgcaJty3EZ4t91vIwfPtl4UaYLrcMh+L9IRc3YBxolPrCoA96iBNZnxFUqRwUM9HwbJAAAAAQAAAAAAAAASADLEATIB" +
        "4EAAACNAAC8zj+BQANsxLRO7zC/ZqlUIVQhVCFTK9hdlpDhZkv//DWTZhEUGWjnnd+roW1YRzIq/U+ghh5PJW1BzaZuFwWjXMJjg" +
        "qiQB+JXUOdO18qSpKiaRBD32DYNjE7Ko0zlLS9CRlnWM7QbmmPZNp1su+B0xzJVySP4eh3WENcvofewbBsBaOots5znQLK1tiQwM" +
        "IQUoNsJCfxN0icz3XlZB3N1sG+liAcvWy9mEvCN5y/L89BgW32rrBP1KrIAWAAAAAgAAAAAAAAASADISMgLgggAAI0AAIDQA/f6Y" +
        "pPfgGAAAAAMAAAAAAAAAEgAyFDIDAQICACNAAC9UC+8mph9/AIgy";

    private static readonly string[] PaletteFrameDigests = [
        "7d01575b3c7e69ac9bddcf6c1b4fcbfe3b478b5ee9e1e127b426c00edfb9dd63",
        "64f91addab61faae28edd3401daf4641fc55121110710c2b72111e5b9a82066b",
        "03db8f64e403291cf702fbc2e2b5cdef55ef6aa2e3b22049b7723beb3d026b0f",
        "0c40b63da247a342cd5ddfa9cc797361a3829493864c27480961aa0b4f04de17",
    ];

    private const string TenBitIvfBase64 =
        "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" +
        "CZoc1GoJvuXZwS+OiTC76GG1ozOkLDKJpgtRQA==";

    private static readonly string[] TenBitFrameDigests = [
        "6184938443458b87da49be1b44be507cf7d5b3a3c4575678042a9a2eed7bb9ee",
        "9b9af84e206a962671496aaceef80f693e0065b815975632b15325e746330a22",
    ];

    [Theory]
    [InlineData(IntraIvfBase64, nameof(IntraFrameDigests), false)]
    [InlineData(InterIvfBase64, nameof(InterFrameDigests), false)]
    [InlineData(PaletteIvfBase64, nameof(PaletteFrameDigests), false)]
    [InlineData(TenBitIvfBase64, nameof(TenBitFrameDigests), true)]
    public void DecodeDisplayFrames_LosslessClip_MatchesDav1dExactly(string clipBase64, string digestField, bool highBitDepth)
    {
        string[] digests = digestField switch
        {
            nameof(IntraFrameDigests) => IntraFrameDigests,
            nameof(InterFrameDigests) => InterFrameDigests,
            nameof(PaletteFrameDigests) => PaletteFrameDigests,
            _ => TenBitFrameDigests,
        };

        using MemoryStream stream = new(Convert.FromBase64String(clipBase64));
        List<Av1DisplayFrame> frames = Av1DecoderCore.DecodeDisplayFrames(stream);

        Assert.Equal(digests.Length, frames.Count);
        for (int i = 0; i < frames.Count; i++)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendCropped(hash, frames[i].Luma, highBitDepth);
            AppendCropped(hash, frames[i].ChromaU, highBitDepth);
            AppendCropped(hash, frames[i].ChromaV, highBitDepth);
            string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            Assert.True(digests[i] == digest, $"frame {i}: plane digest mismatch");
        }
    }

    private static void AppendCropped(IncrementalHash hash, Av1Plane plane, bool highBitDepth)
    {
        if (!highBitDepth)
        {
            hash.AppendData(Av1TestData.CroppedBytes(plane));
            return;
        }

        byte[] row = new byte[plane.CropWidth * 2];
        for (int y = 0; y < plane.CropHeight; y++)
        {
            for (int x = 0; x < plane.CropWidth; x++)
            {
                ushort value = plane.Samples[(y * plane.Width) + x];
                row[2 * x] = (byte)value;
                row[(2 * x) + 1] = (byte)(value >> 8);
            }

            hash.AppendData(row);
        }
    }
}
