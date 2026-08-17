// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates super-resolution decoding on a real all-intra aomenc clip (128x128 upscaled from a coded
/// width of 79, denominator 13/8, cpu-used=0 so CDEF and loop restoration run on the upscaled
/// planes). Every displayed frame must be exactly equal to dav1d's output, verified by per-frame
/// SHA-256 digests over the cropped planes.
/// </summary>
public class Av1SuperResDecodeTests
{
    private const string ClipIvfBase64 =
        "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";

    private static readonly string[] FrameDigests = [
        "bf0ae94d557623abe675e072e2e9bef85bd288759f0085ab611eca22fc216a9d",
        "986a5a553878a0aaa6682ae9b30c4cfb7d9939d96a7c88132b7fda6fea2da9aa",
        "0f5bb8fc41ab5c2c0e01ba48cd0bd0a1981bbf4a935a72c12c2a1fdce6c70d62",
        "f4341864495926f1d5583efbcc690bac2e3a3fc6d0ad14ac04e2c1b612a81688",
        "23d4d410a0a01be3ecb406029d3756c3b9e9c493ba3ec560bb9abc823f880daf",
        "3eea4932f9d1fe351a4e5db1f120146c4dcd120ad13cb5132fdb0585176c8c26",
        "6b64051102f7243db37bf874af51276add8b9102b971448e0eb3273b57265612",
        "41a56696d3ec2ec40b6b65b811be40f262e47ab1b6da2af100cd21e963c8e529",
        "f8a172bd420fd527b394bf31178bd57a1dc4e83508548c18df161e5526e1a1ea",
        "0b089d4f2eaa8940aaef2fabc07dc6fe3a6ae2455a41db7bd9a6704bace555d0",
        "1a8df6551d0e9e216e62f7ea00c05a925df4d67311b7168dbadced9a0c0c0f43",
        "d5646d5f8f08ac19422b2b02bc8ce8f7d5323b478cfc3a931783523cd11674d5",
        "a57ca4fcc4798e6de98390f057cb44f913e0c801a63f792a6abc22ae191e909b",
        "110e24827eb023616bab0ff9d3658bda584205cf71ac69634468040e9b78f370",
        "710ae95769e7a1842414e36c558e2339afe022b496979542fdc8eaa96c567093",
        "e73134f00cc86c8aa9f2b08364c329d5c01bd97645e98a07c726a2b85b3c4cdb",
    ];

    [Fact]
    public void DecodeDisplayFrames_SuperResClip_MatchesDav1dExactly()
    {
        using MemoryStream stream = new(Convert.FromBase64String(ClipIvfBase64));
        List<Av1DisplayFrame> frames = Av1DecoderCore.DecodeDisplayFrames(stream);

        Assert.Equal(FrameDigests.Length, frames.Count);
        for (int i = 0; i < frames.Count; i++)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Av1TestData.CroppedBytes(frames[i].Luma));
            hash.AppendData(Av1TestData.CroppedBytes(frames[i].ChromaU));
            hash.AppendData(Av1TestData.CroppedBytes(frames[i].ChromaV));
            string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            Assert.True(FrameDigests[i] == digest, $"frame {i}: plane digest mismatch");
        }
    }
}
