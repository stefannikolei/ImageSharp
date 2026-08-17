// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates distance-weighted compound prediction (<c>enable_jnt_comp</c>) on real two-pass aomenc
/// clips with alternate-reference pyramids (128x128; the per-block flag chooses between the plain and
/// the distance-weighted average, whose weights derive from the order-hint distances of the reference
/// pair): speed-1 and speed-0 8-bit clips and a 10-bit clip. Every displayed frame must be exactly
/// equal to dav1d's output, verified by per-frame SHA-256 digests over the cropped planes.
/// </summary>
public class Av1DistanceWeightedCompoundDecodeTests
{
    private const string Speed1IvfBase64 =
        "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";

    private static readonly string[] Speed1FrameDigests = [
        "c6911989698f646189ed0f859d8d39b4b915ae730e5d7e7fc75b7c949a4f0306",
        "ae06703160415a66fc68eca350ed6500630ef4972722ceb0e42b6be1aa22e3ed",
        "4498c61ed6af56a1f1a4d1d05ca111c065e31f9149dec9f357a2fa73dd77bcf3",
        "f34af4caf41ed4e0871ffac0011c7bb5b36915c669396ba5bb7259e523953b08",
        "791408339925e5fd11d8a5fced06cd00aa577244647ccfc5548385788feab172",
        "22f923c1b6152085a074b5c1f9782135c78f68c7af351c7a5dd818479ba11d2e",
        "606ff0d059e10500287bd6bceface05e06840c2ea9c0bf4435a5050d73f2709a",
        "18527936950a3b858aa7ef2d5716ca3306eb651713d9c62b9cd0866ffa0b6be6",
        "73c21916f4e5d3a5bc4b9d12146069838da4981cd9b401b1afc59ac8028e0abb",
        "e6e76f3ea5f718ae43f3ba839a86fe90cb4dde5b7de2bc686abbd34d6655e630",
        "9f0a6bfc16595464558f7e433059c284aeb45800898c7aeb85a1a69356f8bfe2",
        "371e85541b3aaa351e7b7a080b35e6b174bd76dcb4d7eca73e1e002b14a02ae1",
        "a40e1b95ef4cb428be935eb9294f8cfdc8498cf5134f09fbd9d9fd0098c31c2e",
        "82bf0d10ed86bfd40ae7ddc2ab546d84c368b621e115e577239d3e6172bb5a58",
        "eac0b4908d20f86f9b84b8861a5b388e02efe5df09e2d73e5b9d70013b17fc66",
        "fb8c081161ed55263153b18c013648d03ae900484d357e9632fb514cea6147e0",
    ];

    private const string Speed0IvfBase64 =
        "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" +
        "eA==";

    private static readonly string[] Speed0FrameDigests = [
        "74eb1ec0852649a59a3fc4057a6dd0ab67c5817ccf0a3806ee2b1d1660fa67a5",
        "e43887580f91a9cbe08aec1fd2db00e311931ed3f80d5a66446a23dcc722a522",
        "8ac333821ebb31ff9ad4eef05d6aeacfd60e35c8379590ca72c264d822ec84f0",
        "f27b7352cf994924ba902c40a6c16613139be871dd5b86f00f18323e684da5d3",
        "0899741bfd92531f2b19b577ed2cd99f7e772c8ec75c51e9615eb74bb0fb6217",
        "172c13b90f63ed287694e272bac2d7b7d6957e37e993a529686ddc03da499e95",
        "63491ff56dba91ed29d93d3f8732d4faba5e278519ba40fbd80ce7123d0037d5",
        "b384ce001ec965d846d900b17105b83a6ae43008c3beface0527154d233f7ee2",
        "8c16d3d456cb40afa5ff01642400302813e5ad947ae47ea41da7d9afdb7469af",
        "9cb009552f9365fc61a67b88c507b3be81adf94ae4c37693d261bff1a9c40866",
        "adead29fe0be11d9fb2f705f525a5d4c692a1bbd03837677ed24eb654d317843",
        "1565ab8fd71f1be0b319425470348ee13b7989be8d3f9c30d209333564ef10a3",
    ];

    private const string TenBitIvfBase64 =
        "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";

    private static readonly string[] TenBitFrameDigests = [
        "d7bbf0135ca7fd4caed12cdaccf42b30a67a0a54958afe62521875d88bce6b52",
        "b67d89ebd97197ecce7b1d3d4fe8a661f5ef33008e6212193f1f7f048cbfb23c",
        "24ddf247546814eacc5bd5df9ac112918c8d46f9c66b67542e07ef0eaad7a79d",
        "88e1852890ae22a39e5bc271c6e3432820c64535f9c3b96fc57a38d57eb3fa17",
        "367e98bff5eb550d517a09906d471baffbd826447f1ed5bd7020824ad562e6c6",
        "ec7f1933d12186eeb898c1aa68e1750ac04613d616f91791855ac9af47530037",
        "e5888f13a530a1aff1d3ea93117b53ce5224d8f229251ad3548bc2fa318ba036",
        "70739cc1b65cbcbf31fe86dc08d8263b6059ecea49b9785a0452aa3647453990",
        "406c94a2029ddea27600f3c9b87719ef9a5d82cc97572b220147bc62cde9fc89",
        "3b65366d994d2a4d2391436047f3e5d35e5ffea613a9977f5f3e6427eb768271",
        "82ce425e8f66ccd11dd1f9061a524ba2329d1f6aa6233d12994fb69780ba8b56",
        "0c00bfdf4bb306bad2819614821680041c35c0838d75876650f5871b8b1d8602",
    ];

    [Theory]
    [InlineData(Speed1IvfBase64, nameof(Speed1FrameDigests), false)]
    [InlineData(Speed0IvfBase64, nameof(Speed0FrameDigests), false)]
    [InlineData(TenBitIvfBase64, nameof(TenBitFrameDigests), true)]
    public void DecodeDisplayFrames_DistanceWeightedClip_MatchesDav1dExactly(string clipBase64, string digestField, bool highBitDepth)
    {
        string[] digests = digestField switch
        {
            nameof(Speed1FrameDigests) => Speed1FrameDigests,
            nameof(Speed0FrameDigests) => Speed0FrameDigests,
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
