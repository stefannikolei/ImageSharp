// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates monochrome (4:0:0) decoding on real aomenc clips (128x128): an all-intra clip, a
/// two-pass alternate-reference inter clip, and a 10-bit inter clip. A monochrome stream codes no
/// chroma, so the luma plane of every displayed frame must be exactly equal to dav1d's output
/// (verified by per-frame SHA-256 digests; bytes for 8-bit, little-endian 16-bit samples for
/// 10-bit), and the placeholder chroma planes must be neutral grey.
/// </summary>
public class Av1MonochromeDecodeTests
{
    private const string IntraIvfBase64 =
        "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";

    private static readonly string[] IntraFrameDigests = [
        "fe6a808afa2fbedb97e27ddd644ba2aae83fcce23480c0c221fbf89ab9ca67d0",
        "a14f9174f5058b03f317d998109947b45720ff6a2f80c6c3e898697f9ec4046d",
        "d75fecdb714b8c692d0148d9c9f027989b7bdce086713ecd53ba53fe314a0b1b",
        "a9bac81cd791aa60e0702acb54b22e3d56bbaf05edf7637b52396858ce8f7bbd",
        "6d175cf416b2bdb3297348ac2f16ecfe69cd283d6a02786f7a3ad287197a3cd9",
        "4491252e8e8a46adcd7e483fa4def1b8cf8add505cf61a86f3392d54a23b3624",
        "77ce5bf76e5046a6f8144938216472aaf8709ff03b5208acdd4e5531d2cec749",
        "88308835dda5b18696496cbff50efe5bd1b9d8673adb780b9b67a0f86bdfad1a",
    ];

    private const string TwoPassIvfBase64 =
        "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" +
        "MgTsXGZwRFbk0zCA";

    private static readonly string[] TwoPassFrameDigests = [
        "caa40204b7ed2f0acda6e8a7366c617e55a0ec56005ef4404643ab970e4b5ccf",
        "09e305b23e7f7d705855ccc1d3f152d9c5d0fb27d7d920d8ca86498cd00304f2",
        "5db59a8a8f989d76bb880548c6dedd519a7b82fc56e10c44a0bf39a54fbf6230",
        "1bc835e7256463c945c41a43f7935791a3c2a74e72857677cb17e89bd443ddfd",
        "48abf8171e025aa90b72865919bc296da4664dc0307f72e3115b9bdc3e45125b",
        "f8d430e8c75babdc558b5c94f2859b022856c42958b7a251b6fa42c4a8ce3f5b",
        "e3b8510755b8b473cbab2f50d8a58ecf9c3abc32da4ca32a9695106f3850a61b",
        "300bc2a13c02f461bc1f0761a3585d40cae9b8b16b6132f92dfeff0e77e71e5e",
        "b7c11b9d70dfcfdc7477d96b67101c5d329436b52d58ffd053d0cb0242c661fe",
        "4a7a5db852f17019adcc0564dc584c1aeb3d08aa5e22bd5568fa981ad15bea44",
        "660454a5df03a64e902719166b80f119c76b552759cf367a393e08f56aaacf30",
        "08003fd1ca06830cf029cf2c6fd20a9daa8536e60145ee05af8cd07005edc129",
        "0138435050e29a88403e19ba640b3e35274dfc4e60fb8ebebc0813f60dfd41c0",
        "211ba8e1d9e8e093b82fac513eff2446cbb1918876b78fc20164b1e76d4ba09e",
        "523c2066a7da36d2c431be6602d2e119887e8837511be5544cfa7a040cb8eed1",
        "9c2cad04900cc551393a25abc3b5c9157464cdea674da2fd869d54407ef25216",
    ];

    private const string TenBitIvfBase64 =
        "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" +
        "5d3MBQAAAAsAAAAAAAAAEgAaAZg=";

    private static readonly string[] TenBitFrameDigests = [
        "6ffeaeb71f71d84c4861e60f4f8607ac049c2ccc34775a4f77da45243055d254",
        "768e6f96162cfa9ba23008e21b9b4bdad93322d76e07413b8d03534004fcdf76",
        "45942a9168e386f6b57618ffbfce4701791657345eedb854acf3ecac574cef9f",
        "bf3c1a1057ed6d9bbbb0d76fd817b892dfae1764bd7f901d78f0d1fbd183ab53",
        "97cac8a1735390dccb21e2053deb9a48330beffe82f2d697a04a4d3be89db2ce",
        "fb6ca12a180fc4188fc69f95d0df47efdb8bc1c4b8ffdbcf3d3317e4014af2e8",
        "9a8b738c5f9518fa3a4258d788949b80f7e5da047356b85491618da2dcd1f39b",
        "a60da2c19645ec0b48694f5df431fc14e08e7c829bb2064fcfa83c1412f8fe71",
        "61169df396edac2142ddd831a268754559a9e524b914f2cd76f27e0e29da280e",
        "15291178a7594022035ea3c05cf2f6a9dd29bfd83e857f23fcb9d453bb1f38c0",
        "e8f8648651ad8dabc551e1fd8bead9b7b844ec40b5f385d0eb0f1f1aba9b05ea",
        "1438ed360703975b8a9b6d197dd7409f9e167306462793ad97b85ab0831e5548",
    ];

    [Theory]
    [InlineData(IntraIvfBase64, nameof(IntraFrameDigests), false)]
    [InlineData(TwoPassIvfBase64, nameof(TwoPassFrameDigests), false)]
    [InlineData(TenBitIvfBase64, nameof(TenBitFrameDigests), true)]
    public void DecodeDisplayFrames_MonochromeClip_MatchesDav1dExactly(string clipBase64, string digestField, bool highBitDepth)
    {
        string[] digests = digestField switch
        {
            nameof(IntraFrameDigests) => IntraFrameDigests,
            nameof(TwoPassFrameDigests) => TwoPassFrameDigests,
            _ => TenBitFrameDigests,
        };

        using MemoryStream stream = new(Convert.FromBase64String(clipBase64));
        List<Av1DisplayFrame> frames = Av1DecoderCore.DecodeDisplayFrames(stream);

        Assert.Equal(digests.Length, frames.Count);
        int midGrey = highBitDepth ? 512 : 128;
        for (int i = 0; i < frames.Count; i++)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendCropped(hash, frames[i].Luma, highBitDepth);
            string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            Assert.True(digests[i] == digest, $"frame {i}: luma digest mismatch");
            Assert.All(frames[i].ChromaU.Samples, v => Assert.Equal(midGrey, v));
            Assert.All(frames[i].ChromaV.Samples, v => Assert.Equal(midGrey, v));
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
