// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates palette decoding on real aomenc screen-content clips (128x128,
/// <c>--tune-content=screen</c> with intra block copy disabled): an all-intra clip, an inter clip
/// (palette blocks between motion-compensated blocks with forced whole-pel motion), a 4:4:4 clip and
/// a 10-bit clip. Every displayed frame must be exactly equal to dav1d's output, verified by
/// per-frame SHA-256 digests over the cropped planes (bytes for 8-bit, little-endian 16-bit samples
/// for 10-bit).
/// </summary>
public class Av1PaletteDecodeTests
{
    private const string IntraIvfBase64 =
        "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";

    private static readonly string[] IntraFrameDigests = [
        "7d01575b3c7e69ac9bddcf6c1b4fcbfe3b478b5ee9e1e127b426c00edfb9dd63",
        "64f91addab61faae28edd3401daf4641fc55121110710c2b72111e5b9a82066b",
        "03db8f64e403291cf702fbc2e2b5cdef55ef6aa2e3b22049b7723beb3d026b0f",
        "0c40b63da247a342cd5ddfa9cc797361a3829493864c27480961aa0b4f04de17",
        "6cb42b11e58f399b172a2cdb15e75e4ea7aefc4d2ea6936f4de4c8e5f2d16c0e",
        "0e6fb924c7c3f16b9fb18efecf0983258bc184df379b78b059b075e01b5c622f",
    ];

    private const string InterIvfBase64 =
        "REtJRgAAIABBVjAxgACAAB4AAAABAAAAEAAAAAAAAAAhAgAAAAAAAAAAAAASAAoKAAAAAzf/5tfMAjKQBBQAIBAAACAAACDYdYDM" +
        "0OXqLeX+OYkJpdLFc4uGQSYHmWuiZLDSg6iI+fpMWxhiVDehKOD8s5mylouthFrjyLvtShy63cyFN8Z51zxjLV6i2yBm5r+rvOOO" +
        "lcQWue8ahWW+qvOkUQNy7JsteAY7m5zIaIqZ67oNViTWMbvWldjcuPwkOa66zh7iABuexRROtauEKJ8+ejhLRrukuLjCk4d9CHw7" +
        "CrwTVlf2faepQlvNe8NW1VlZB9B/JwB0VFQ5U+6J9p7poEq61ErehgAo3i542tHNLLEJD0L4up/EwiXUlIgN9PJtBU7DWmt/9Wfk" +
        "w6CDbgeftv5XYEM7MU44NtcxwkON9EJwThFK0lME/yZun7sxwE2FuLF4Q1Fk+t6R9XIXZJMsm4yhgdpQfJ5jR7VaHtYLptmUz7Y3" +
        "tml45DXi8S/ORjj5eDaLQE/pnKnPJLhMa8cxni7i6h6IzPwVfpKytQ02E8BFRyxZXId+0ciGQ6f4pL41rxUH+iMNRXeXT3hs3mr2" +
        "5PJPEhg8nF1o2bDkroHbXgsTK8Q40I30leiwjiuOiURzYGz15q6KxIORk0iLRMo5MLfGj2F6IdzJV1gjfOK5fWgpxYErtRDuOLXZ" +
        "Xdbzwwjdjxan0+SmT2FvF97MeDS4FUZtlP3fOpD1wCU4lle3wn6Dff2/V/HvCdf87XUhNZnxFUqRwUMvgTkAb48AAAABAAAAAAAA" +
        "ABIAMi8hB/AgAAARoHAwAAQAAAUA2y4FnFs+417m89TwOFygJKLza3KwLhhlSHRHTmICiDIeKQPwQAAAUaDIMAAEAAABfZ8EX+u0" +
        "GsA/4ASdBwGAMh0pAcCAACBRoPAwAAQAAAF+U8E396hpQf8AQKhr6DIbMgHiAABoo0IQAABAAAAXgMfwCMQD+AAAm0SoHAAAAAIA" +
        "AAAAAAAAEgAyGDICBAgAaKNCAAAAQAAAO8SEQ0IPwACo6gUAAAADAAAAAAAAABIAGgG4OQAAAAQAAAAAAAAAEgAyHCkChAOwIFGh" +
        "ADAABAAAAXsBPwIPNxIfwECxiOAyFzIEMAdgyKNCEAAAQAAAO0EAHCBAAKy+BQAAAAUAAAAAAAAAEgAaAegbAAAABgAAAAAAAAAS" +
        "ADIXMgYiDdhUo0IQAABAAAA7wMRDhAgAsXsFAAAABwAAAAAAAAASABoBqFQAAAAIAAAAAAAAABIAMhwpBaICmHZRoOAwAAQAAAF+" +
        "Aaf8BQZEH+AAueeAMhspBMCCmF5RoPAwAAQAAAF8DgS/fwxAP4AAwI8yFTIIMAUwdKNB8AAAQAAAO7OgDQgAtQUAAAAJAAAAAAAA" +
        "ABIAGgG4GAAAAAoAAAAAAAAAEgAyFDIKKAfQsKNB8AAAQAAAODQQAK8iBQAAAAsAAAAAAAAAEgAaAdg4AAAADAAAAAAAAAASADIZ" +
        "KQahBcx0UaDoMAAEAAABfUlCAMwD+ADGLDIZMgwgi5ico0HQAABAAAA7CZfgEYAAAJOnPAUAAAANAAAAAAAAABIAGgHIHAAAAA4A" +
        "AAAAAAAAEgAyGDIOMAiozKNB0AAAQAAAOwsRA0APwACnVRwAAAAPAAAAAAAAABIAMhgyDwAPELijQjAAAEAAABZAAcFD+AIA2yg=";

    private static readonly string[] InterFrameDigests = [
        "7d01575b3c7e69ac9bddcf6c1b4fcbfe3b478b5ee9e1e127b426c00edfb9dd63",
        "64f91addab61faae28edd3401daf4641fc55121110710c2b72111e5b9a82066b",
        "03db8f64e403291cf702fbc2e2b5cdef55ef6aa2e3b22049b7723beb3d026b0f",
        "0c40b63da247a342cd5ddfa9cc797361a3829493864c27480961aa0b4f04de17",
        "6cb42b11e58f399b172a2cdb15e75e4ea7aefc4d2ea6936f4de4c8e5f2d16c0e",
        "0e6fb924c7c3f16b9fb18efecf0983258bc184df379b78b059b075e01b5c622f",
        "05bceed254e5f9e31e5dff7727761b2b99097c8c4082989d252ea0c17bb2eee0",
        "7d4ec06fba5d30cdb0aa726f0e012da58d202df69178d12dbcf3e267a0123a6a",
        "306506a7aa66c419782a840bf07c7a168ec60fea39ecbcc094699d6fde21b9fb",
        "29784bfe7859514ac91f7e321baaf65a5e42601f2f93f0a526f5a645ab16208d",
        "5ae4cb703f259a6aea9e24b6b783fe31f2526d907d201436f3a0bd7941e29b74",
        "c22ed32be3ab50deb0a43da55b8a4a15bbd9b98317a561eb413bb67d34f4ca99",
        "a4fee39547543feb4456ca89e145b4080bd6728ba3d3b14cdcf521d53991bbfb",
        "220041c4ca7dc85c18b96b6151b7446915606a8394a083b4aed2fb5670cf3183",
        "e0defa668cc0f0b28a6699f4787f2600b613addb086e916ff9134c33ecda23d6",
        "38d1ac45ce66074ac3f8cc6306da1a8e8fcbab8c753bf2efa1037914de36667d",
    ];

    private const string C444IvfBase64 =
        "REtJRgAAIABBVjAxgACAAB4AAAABAAAADAAAAAAAAAAqAgAAAAAAAAAAAAASAAoKIAAAAzf/5tfMEDKZBBQAIBgAACAAACDYdYDM" +
        "0OXqLeX+OYkJoBC3EENl3QxVRePMy+HskwlaVtXgCDkAfImVtjLFXzkPpAIbB/n/FcE8kYcl+rJZfRViRE57WQxcwmEI+MapRb/c" +
        "jMwcLYhYVjk8qRFjvHplY029vdgPVqIbd9E/Gl54kxD1yW78DdnQ/glHdu97YZklNFTEa+S5iIVT6LNxgyMk/Om2muwL30Ciq4a4" +
        "FwC7t6U2YEd7gK2dX1x/jJ+N3ODeuJY+ZpIcqfkAjq3ywDNrg8e69ft2r00eIsBeq1g2bky/BRVwJM7isWoMdCxsGSUbkVjsywC/" +
        "xK38c6VxGc4ussSPEde6NCTzOOtKy42BC4ihWz8Tp0cIOwaMVXtWB29EdVcHqpP1y+lzA72f3tMEgSY0/vh6PZNKbwY6+RFBpAfo" +
        "tWQ9QWP53v/j3kwz+fYCK0WzqLRF2fD2wMFM1sqqivgsfn4TDw/qXK9qB7YqJL8Bw/B1X3MmXha35on7+QhrZN8rszOL8jUOJEQt" +
        "IZzb6BwYQif6P84TmLRsVjiVlQq4cUq2u/eeQKJpPXucp9G9YvKKhji4eLETltsfrZ+aQ0nuUcs/MdwraMUojayPQWXtLWJ4pi8M" +
        "onE9zIK1ZYmUBiNp62jGmZtgL/rmUuQe+itP/O3eFXBvDGGVASNwaqNJJNubvhRuoAcx7xrAWcvHQjerpa+3dEgEODzRa9N+QKAA" +
        "AAABAAAAAAAAABIAMkIhBfAgAAARoJAwAAQAAAV/8hn6T+eP+Q2A20WObr2oCGv896ax0nf1b30e3LnFxT+FcbfXUDekXCHOvWAq" +
        "D+dPbRkyHikC8EAAAFGg6DAABAAAAXysEf8gINVL/QAAnQiagDIcKQFAgAAgUaEQMAAEAAABfGwS/oEM4B/QIKhsWDIaMgHiAABo" +
        "o0JQAABAAAAXgMfwCMQD8ACbRKgFAAAAAgAAAAAAAAASABoBuB0AAAADAAAAAAAAABIAMhkyAyQHAECjQkAAAEAAABeJCIeEC/AI" +
        "qInuHQAAAAQAAAAAAAAAEgAyGTIECArgQKNCQAAAQAAAFoIAOyh+AgCxt/AFAAAABQAAAAAAAAASABoBqDYAAAAGAAAAAAAAABIA" +
        "MhspBEgC1DhRoQgwAAQAAAF8KcPvoNmH+gCxk+AyFTIGIgWo7KNCMAAAQAAAF4GIgLodwBwAAAAHAAAAAAAAABIAMhgyBwEIsPSj" +
        "QiAAAEAAABZ4yQYgH4AAwNMFAAAACAAAAAAAAAASABoB+B0AAAAJAAAAAAAAABIAMhkyCSQO4FijQiAAAEAAABeDQCgxAP4AAMZu" +
        "GQAAAAoAAAAAAAAAEgAyFTIKCAvYiKNCIAAAQAAAEBoIfgDKzB0AAAALAAAAAAAAABIAMhkyCwANeHCjQnAAAEAAABdSL771Iv36" +
        "ANDA";

    private static readonly string[] C444FrameDigests = [
        "979ed9b2aaf0ad0cb3819192e0d7075dfa9f93074ce8a41d109656d2bd4f2aea",
        "fc6db0e0d53ae93959f0ac7ca766b88b2a913a58eca835956ff8046cbba2dfef",
        "ad46fc23c29503ca56ccb61cac57732f8095f3be4cd7ddaa1b7bcb7d9a2596e5",
        "813874dc162726182cf4d9fcdcdb52d53cc96745dc05d340566e3873ac3aead3",
        "baad9524cb52058f72387dd95fca45a651aa8146d82a6212e1063a65f1368467",
        "750993e6d3763ce8a02308d35f39fb1d66a547e6041dfc24deada169d4c838a7",
        "c009110787ffab15fe192adc0278b2bf2cdc9d8923eabcb11b8e39a2a8689507",
        "b4cfae09c44d9c3c30394d4483f3eac489296af99ef00a9952a9f45f374b5d20",
        "3a10be4cbcab5e0afc4f7de06fc355d1ba70997961b1930cf3d8d6a77b12f56a",
        "2ffffef37f3b1b3e35aab4e1c2a93de1a3401190ef4e5292d021e11294dcc156",
        "433f82556917d18cf219df2254861377699f99cc99c828f6cf69a204b995e3c9",
        "5d2b1b8f97608301221b2baabfd8c2bb39caf3ee94a581d6491086e9600feeb4",
    ];

    private const string TenBitIvfBase64 =
        "REtJRgAAIABBVjAxgACAAB4AAAABAAAADAAAAAAAAAArAgAAAAAAAAAAAAASAAoKAAAAAzf/5tfOAjKaBBQAIEAAACAAACDYdYA2" +
        "ncB+ot5f45iQml0sVzi4ZBJgeZa6JksNKDqIj5+kxbGGJUN6Eo4PyzmbKWi62CPFPPGdvZEocut3MhTfGedc8Yy1eotsgZua/q7z" +
        "jjpXEFrnvGoVlvqrzpFDA1O7JsteAY7m5zIaIqZ67oNViTWMbvZbTBrt1FclrEZRilhzR3NxePwWJTkfWBNqaOXSRBnrUU8oY/wz" +
        "McTOSXZUFlN817zTIn4wQDyCfyBim12sx08PRxW9nBgjDK4fBo9/MhJxLIuyZn8FUthyxek2iVIWw7bognS55zGVC4J1U5uQlAyc" +
        "XH0E/4ZCbKPZIU2lsPQ3UJrn4bgUUIsopcz6U0A6NPMOeE72nasDs6jwo06OUx9Rht0UfKQPR93Vf+8rz4P/nW9Bh6SyQPtCAFR/" +
        "0zZmFXYBtep61DlnwmzWo1gYuCiUkP58wfWa2VVRXwfeKP1AZ6Z0ltqQi4h+dFkWuW7vlsoWY6/ULH1pRIBCtief7XgFKRI35qVj" +
        "nRUlZq60yhfoc7GYaQARl5w5NGcPaBC48kxpZwfdogmn/kXOQAjHoG50NOX3gndbxptT+nbJDSe5Ryz8x3CtoxSiNrI9BZe0tYni" +
        "oB9spXw4NDUDsd95ZbbIxF5xyyCZK57Ir9lGnZrwqZFlmsg+9ZYhkSLrE3CkWJ66SLFf4fCCxaJIqXjQjeroNOtpgRv+11ECGUCf" +
        "AAAAAQAAAAAAAAASADJCIQXwIAAAEaDIMAAEAAAFf/H5+x/nr/kQgNtFjm69qAhr/PemsdJ39W99Hty510Cwx5kuWARlDGC57mFH" +
        "kysQkZSIMh0pAvBAAABRoRAAACAAAAvhSf/ICDVS/0AAnQiagDIcKQFAgAAgUaEwMAAEAAABe6UP6BDOAf0CAKhsWDIaMgHiAABo" +
        "o0KAAABAAAAXgMfwCMQD8ACbRKgFAAAAAgAAAAAAAAASABoBuB0AAAADAAAAAAAAABIAMhkyAyQHAECjQnAAAEAAADvExEPCBfgE" +
        "qN0wHAAAAAQAAAAAAAAAEgAyGDIECArgQKNCcAAAQAAAO0AAHZQ/AQCs6gUAAAAFAAAAAAAAABIAGgGoNQAAAAYAAAAAAAAAEgAy" +
        "GykESALUOFGhKDAABAAAAXwpw++g2Yf6ALGT4DIUMgYiBajso0JwAABAAAA7goEAsdEdAAAABwAAAAAAAAASADIZMgcBCLD0o0Jg" +
        "AABAAAA7ncEGIB+AAJ0owAUAAAAIAAAAAAAAABIAGgH4HQAAAAkAAAAAAAAAEgAyGTIJJA7gWKNCYAAAQAAAO8GgFBiAfwAAsxIa" +
        "AAAACgAAAAAAAAASADIWMgoIC9iIo0JgAABAAAA4DQg/AAC0uB0AAAALAAAAAAAAABIAMhkyCwANeHCjQqAAAEAAABdSL771Iv36" +
        "ANYo";

    private static readonly string[] TenBitFrameDigests = [
        "b6b43350ff9faa3a99c4c2c8872220d30c8ae3ac226bdf137e6efda0e3f1f0ac",
        "aa38c42cdfe613be688f3b8ea22208a4f5363ff3a31c738600e90ced5ad1de1f",
        "6514a4dd9f9a099c5431a15708417b71bd75c18f77b553e51fe632b95c527aa8",
        "23bf1e4c05d25e4ae8a3b55d4857a8efebf258a5e0b9576795caa960f9e4e8cc",
        "9ea4766dbbc11ae4ee979ba474d48ef19fbda41c3e376141bd387103dd382595",
        "d35e0adb2f4bc708ac439271482f18e9802eb222a5123bc5011396806c7474ce",
        "01c71ee1233e267793737971c3ea21a5c56f84e346a833f14f7934ae019fa0b0",
        "be3f492802182cc63cbc92dc96fa8c811276b6f0668a537870f0e1f31016b890",
        "86dc9a434a67713928e6aa2a334a22978dd616fc7ebb02372f1bc6aa398af7f1",
        "089d8462f2137c4e3934a91d06c4e4d31b5d7a304b8b7a3d789c13e3c01a098a",
        "4c182152c3f443ea6566bf8670157674add2fe4e610b7b1e05a7cb9ed5330f3e",
        "f7659f910572fb05b7bf5cacb87b5736833e515a0567da2b8d56faa1cd438a60",
    ];

    [Theory]
    [InlineData(IntraIvfBase64, nameof(IntraFrameDigests), false)]
    [InlineData(InterIvfBase64, nameof(InterFrameDigests), false)]
    [InlineData(C444IvfBase64, nameof(C444FrameDigests), false)]
    [InlineData(TenBitIvfBase64, nameof(TenBitFrameDigests), true)]
    public void DecodeDisplayFrames_PaletteClip_MatchesDav1dExactly(string clipBase64, string digestField, bool highBitDepth)
    {
        string[] digests = digestField switch
        {
            nameof(IntraFrameDigests) => IntraFrameDigests,
            nameof(InterFrameDigests) => InterFrameDigests,
            nameof(C444FrameDigests) => C444FrameDigests,
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
