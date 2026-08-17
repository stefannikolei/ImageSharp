// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Bitstream;
using SixLabors.ImageSharp.Formats.Av1.Obu;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// End-to-end inter decode of a real error-resilient clip whose inter frame is a single 64x64 NEWMV
/// block with a half-pixel (sub-pixel) horizontal motion vector and the skip flag set, so the
/// reconstruction is the 8-tap sub-pixel interpolation of the key frame. Validates the sub-pixel
/// motion-compensation (convolve) path end-to-end against dav1d.
/// </summary>
public class Av1InterSubpelDecoderTests
{
    private static readonly byte[] SequencePayload = Convert.FromHexString("00000002affff036be6010");
    private static readonly byte[] KeyFramePayload = Convert.FromHexString("108c00019d0000050000002e6e978c5a9b417653cc58a1486d1ec7924952d8ca2c906a02f380");
    private static readonly byte[] InterFramePayload = Convert.FromHexString("38460404080000000000000000000000000000000000000000000017c000001600000000985340");

    [Fact]
    public void DecodeSubpelInterFrame_RealClip_MatchesDav1d()
    {
        ObuSequenceHeader sequenceHeader = ObuSequenceHeader.Parse(SequencePayload);

        Av1BitStreamReader keyReader = new(KeyFramePayload);
        ObuFrameHeader keyHeader = ObuFrameHeader.ParseIntra(ref keyReader, sequenceHeader);
        Av1TileDecoder keyDecoder = new(sequenceHeader, keyHeader);
        keyDecoder.DecodeTile(TileData(KeyFramePayload, keyHeader));
        Av1ReferenceFrame reference = new(keyHeader.OrderHint, keyDecoder.Luma, keyDecoder.ChromaU, keyDecoder.ChromaV);

        int[] referenceOrderHints = new int[8];
        for (int slot = 0; slot < 8; slot++)
        {
            if ((keyHeader.RefreshFrameFlags & (1 << slot)) != 0)
            {
                referenceOrderHints[slot] = keyHeader.OrderHint;
            }
        }

        Av1BitStreamReader interReader = new(InterFramePayload);
        ObuFrameHeader interHeader = ObuFrameHeader.ParseInter(ref interReader, sequenceHeader, referenceOrderHints);
        Av1InterTileDecoder interDecoder = new(sequenceHeader, interHeader, reference);
        interDecoder.DecodeTile(TileData(InterFramePayload, interHeader));

        byte[] expected = Convert.FromBase64String(Dav1dFrame1LumaBase64);
        int exact = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Math.Abs(interDecoder.Luma.Samples[i] - expected[i]) <= 4,
                $"Luma sample {i}: got {interDecoder.Luma.Samples[i]}, dav1d {expected[i]}.");
            if (interDecoder.Luma.Samples[i] == expected[i])
            {
                exact++;
            }
        }

        Assert.True(exact >= expected.Length * 98 / 100, $"Only {exact}/{expected.Length} luma samples matched exactly.");
    }

    private static ReadOnlyMemory<byte> TileData(byte[] framePayload, ObuFrameHeader header)
    {
        int tileGroupStart = (header.EndBitPosition + 7) >> 3;
        ObuTileGroup tileGroup = ObuTileGroup.Parse(framePayload.AsSpan(tileGroupStart), header);
        (int offset, int length) = tileGroup.GetTile(0);
        return framePayload.AsMemory(tileGroupStart + offset, length);
    }

    private const string Dav1dFrame1LumaBase64 = "l6q7x9DX29vWzsO3qZiFcV9PQTUrJiUnLDQ/Tl9wgJKktMLN1Nnb2NPJva+fjHhlVEY6MCklJSkwOUZXanqAf5equ8bP1tra1c3Dt6mYhXFfT0E1LCclJyw0P05fcIGSo7PBzNTZ2tjTybyunox5ZVRHOzEpJCUqMTpGV2p6gH+XqbrGztTZ2dTMwraol4VxYFBDNy4pJykuNkFPX2+BkqKywcvS19jX0Me7rZ6MeGZVSDwyKyYnLDI7SFhre4B/lqi4xMzS1dbSyb+0p5eFcmFSRTkwKyosMDhDUGBxgZGhsb7J0NTW087Fuayci3hmV0o+NS4qKi41PklZa3uAf5WntsHJz9LTz8e9sqWWhHJiU0c8My4tLzM6RVJhcYGQoK+8xs3R09DLwreqnIt5Z1hLQTgxLC0xN0BLWmx7gH+UpLO9xcvOz8vDurCjlYRzY1ZKPzcyMTM3PkhUYnKBkJ+tucLJzc/Nx7+0qJqKemlaTkQ8NTAxNTtDTl1te4B/k6KwucDGysrFv7etoZSEdGVZTUM8NzY4O0JLV2VzgY+cqrW+xMjJyMO7saWYinpqXFFIQDk2Njk/R1FebnyAf5GfrLW8wMPEwLqyqZ+ShHVnW1FIQT08PUFHT1pndIGOmqaxur/Cw8K9tq2jl4l7bF9VTEQ+Ozw/REtUYW98gH+Qnamwtru9vbq1rqackIN1aV9VTUdDQkNHTFNdaXWBjJijrbS5vL28uLGpn5SIe21iWFBKRUJCRUpQWWRxfIB/jpqkq7C0t7e0r6mhmY+Dd2xiWlNNSklKTVJZYWt2gYuVn6ius7a2tbKspJySh3tvZV1WUEtISUtPVV1nc3yAf4yWn6Wqra+vramjnZWNg3huZmBZVFFQUVRYXWVveIGKk5uiqKyur66rpqCYkIZ7cWlhW1ZSUFBSVlthanR9f3+Kkpqfo6aoqKainpiSioJ5cWpkX1tZWFlbXmNpcXmAiI+XnaGlp6empKCalI2FfXRsZmJdWVdYWl1hZm52fX9/iI+VmZyeoKCem5eTjoiCe3VvamZiYGBhY2VpbnR6gIaMkpebnZ+fn52ZlZCKhHx2cWtnZWFgYGJkZ2txeH1/f4aLj5KUlpeXlpSRjoqGgnx4dHBta2lpampsb3N3fICEiY2Rk5WXl5aUko+Lh4N+eHRwbmtpaGhqa21xdXp+f3+Dh4mLjY6Ojo6Ni4mHhIF+enh2dHJxcXFzc3V3e31/g4aIioyOjo6OjYuJh4SCfnt5dnVzcXFxcXN1dnh8fn9/goOEhIWFhYWFhYSEg4KBgH9+fXx6enp6e3t7fX5/gIGCg4SFhYWFhYWEhIOCgH5+fXt7e3p6enp7e3t9fn9/f4B+fn59fX19fX1+fn6AgICCgoKDg4ODg4OCgoKAgIB/f35+fX19fHx9fX1+fn6AgIGBgoKDg4ODgoKCgX9/f399e3h2dnR0dHR1d3l7fYCChYeIiouMjIyLioiGhIKAfXt5d3V0dHR0dHV3eXt+gIOFh4mKi4yMi4qJh4WBf39/e3Zyb21sa2xsbnFzd3t/hIiLj5GTlJSUk5GPi4eDf3x4dHBubGtrbGxucnR4fYGFiY2PkZOUlJORj42JhIB/f3lybWlmZGNjZGhqbnR4foWLkJSYm52dnJqYlJCKhIB6dG9rZ2VkYmNlaGxwdXuBh42RlZmbnZ2bmZaSjIaBf393b2hjX1xaWlxgZWlvdn6GjpSan6OlpaSin5qUjoeAeXJqZGBdW1pbXWFnbHJ6goqRl5ygo6Wlo6Ccl5CIgX9/dWtiXFhUUlNVWV9lbHR+iJGZn6WprK2sqaWgmJCIf3duZl9ZVVNTU1ZbYWhweYOMlZyip6utraunopyTiYF/f3NnXldRTUtMT1NZYGhyfomTnKSrsLO0s7CrpZ2TiX91a2FZU09MS0xPVVxkbXeDjpigp62xtLSxraihl4uCf39xZFlRS0dFRUhNVFxmcX6KlqCpsbe6u7q2saqglop+dGheVU5JRkRFSU9XYWt3hJCbpKyzuLu7uLOtpZqNgn1+cGJVTEZBPj9CSFBYYm99i5ikrra8wMHAvLeupJiLf3NmWlBIQz8+P0RLU11pdoSSnqmxuL7Cwr64sqmcjoF9fm5fUkhBPDk4PUNLVWBtfYyap7G6wcbHxcG7sqeajH5yZFdMQz06OTo/Rk9aZ3aFlKGstb3Dx8fDvbatn4+CfX5tXU9EPTc0NDg/SFJebHyNnKm1v8bLzMrGv7aqnI1/cGJUSD85NTQ1OkJMWGV1hZWjr7nByMvMyMK6sKGRgn1+bFtMQTkzMC80PEVPXGt8jp6suMLKz9DOycK4q52Of3BhUkU8NTEwMTY/SVVjc4WWpbK8xczP0MzGvbKjkYJ9fmtZST42MC0tMThCTltqfI6frbrFzdLT0czFvK6ejn9vX1BDOTItLC4zPEdUY3OFl6e0vsjP1NPPyMC1pJKDfX5qWEc8NC0qKi82QExZaXuOn667x8/U1tTPyL2vn49/bl5OQTcwKyorMTpFU2JzhpiotcDK0tbW0crBtqWTg31+aldGOzIsJygtNT9LWGl7j6CvvcjR1tfW0cm+sKCPf25eTUA1LikoKi84RFJhc4aYqbbCzNPY2NPMxLemk4N9fmpXRzoxKygnLDQ+Slhpe4+hsL3J0tfY1tHKv7CgkH9tXU0/NC0oJykuN0NRYXKGmKm3wszV2djUzcS4p5ODfX5qV0c6MSsoJyw0PkpYaXuPobC9ydLX2NbRyr+woJB/bV1NPzQtKCcpLjdDUWFyhpipt8LM1dnY1M3EuKeTg31+aldGOzIsJygtNT9LWGl7j6CvvcjR1tfW0cm+sKCPf25eTUA1LikoKi84RFJhc4aYqbbCzNPY2NPMxLemk4N9fmpYRzw0LSoqLzZATFlpe46frrvHz9TW1M/Iva+fj39uXk5BNzArKisxOkVTYnOGmKi1wMrS1tbRysG2pZODfX5rWUk+NjAtLTE4Qk5banyOn626xc3S09HMxbyuno5/b19QQzkyLSwuMzxHVGNzhZentL7Iz9TTz8jAtaSSg31+bFtMQTkzMC80PEVPXGt8jp6suMLKz9DOycK4q52Of3BhUkU8NTEwMTY/SVVjc4WWpbK8xczP0MzGvbKjkYJ9fm1dT0Q9NzQ0OD9IUl5sfI2cqbW/xsvMysa/tqqcjX9wYlRIPzk1NDU6QkxYZXWFlaOvucHIy8zIwrqwoZGCfX5uX1JIQTw5OD1DS1VgbX2MmqexusHGx8XBu7Knmox+cmRXTEM9Ojk6P0ZPWmd2hZShrLW9w8fHw722rZ+Pgn1+cGJVTEZBPj9CSFBYYm99i5ikrra8wMHAvLeupJiLf3NmWlBIQz8+P0RLU11pdoSSnqmxuL7Cwr64sqmcjoF9fnFkWVFLR0VFSE1UXGZxfoqWoKmxt7q7uraxqqCWin50aF5VTklGREVJT1dha3eEkJukrLO4u7u4s62lmo2CfX5zZ15XUU1LTE9TWWBocn6Jk5ykq7CztLOwq6Wdk4l/dWthWVNPTEtMT1VcZG13g46YoKetsbS0sa2ooZeLgn9/dWtiXFhUUlNVWV9lbHR+iJGZn6WprK2sqaWgmJCIf3duZl9ZVVNTU1ZbYWhweYOMlZyip6utraunopyTiYF/f3dvaGNfXFpaXGBlaW92foaOlJqfo6WlpKKfmpSOh4B5cmpkYF1bWltdYWdscnqCipGXnKCjpaWjoJyXkIiBf395cm1pZmRjY2Roam50eH6Fi5CUmJudnZyamJSQioSAenRva2dlZGJjZWhscHV7gYeNkZWZm52dm5mWkoyGgX9/e3Zyb21sa2xsbnFzd3t/hIiLj5GTlJSUk5GPi4eDf3x4dHBubGtrbGxucnR4fYGFiY2PkZOUlJORj42JhIB/f317eHZ2dHR0dHV3eXt9gIKFh4iKi4yMjIuKiIaEgoB9e3l3dXR0dHR0dXd5e36Ag4WHiYqLjIyLiomHhYF/f3+Afn5+fX19fX19fn5+gICAgoKCg4ODg4ODgoKCgICAf39+fn19fXx8fX19fn5+gICBgYKCg4ODg4KCgoF/f39/goOEhIWFhYWFhYSEg4KBgH9+fXx6enp6e3t7fX5/gIGCg4SFhYWFhYWEhIOCgH5+fXt7e3p6enp7e3t9fn9/f4OHiYuNjo6Ojo2LiYeEgX56eHZ0cnFxcXNzdXd7fX+DhoiKjI6Ojo6Ni4mHhIJ+e3l2dXNxcXFxc3V2eHx+f3+Gi4+SlJaXl5aUkY6KhoJ8eHRwbWtpaWpqbG9zd3yAhImNkZOVl5eWlJKPi4eDfnh0cG5raWhoamttcXV6fn9/iI+VmZyeoKCem5eTjoiCe3VvamZiYGBhY2VpbnR6gIaMkpebnZ+fn52ZlZCKhHx2cWtnZWFgYGJkZ2txeH1/f4qSmp+jpqiopqKemJKKgnlxamRfW1lYWVteY2lxeYCIj5edoaWnp6akoJqUjYV9dGxmYl1ZV1haXWFmbnZ9f3+Mlp+lqq2vr62po52VjYN4bmZgWVRRUFFUWF1lb3iBipOboqisrq+uq6agmJCGe3FpYVtWUlBQUlZbYWp0fX9/jpqkq7C0t7e0r6mhmY+Dd2xiWlNNSklKTVJZYWt2gYuVn6ius7a2tbKspJySh3tvZV1WUEtISUtPVV1nc3yAf5CdqbC2u729urWuppyQg3VpX1VNR0NCQ0dMU11pdYGMmKOttLm8vby4samflIh7bWJYUEpFQkJFSlBZZHF8gH+Rn6y1vMDDxMC6sqmfkoR1Z1tRSEE9PD1BR09aZ3SBjpqmsbq/wsPCvbato5eJe2xfVUxEPjs8P0RLVGFvfIB/k6KwucDGysrFv7etoZSEdGVZTUM8NzY4O0JLV2VzgY+cqrW+xMjJyMO7saWYinpqXFFIQDk2Njk/R1FebnyAf5Sks73Fy87Py8O6sKOVhHNjVko/NzIxMzc+SFRicoGQn625wsnNz83Hv7Somop6aVpORDw1MDE1O0NOXW17gH+Vp7bByc/S08/HvbKlloRyYlNHPDMuLS8zOkVSYXGBkKCvvMbN0dPQy8K3qpyLeWdYS0E4MSwtMTdAS1pse4B/lqi4xMzS1dbSyb+0p5eFcmFSRTkwKyosMDhDUGBxgZGhsb7J0NTW087Fuayci3hmV0o+NS4qKi41PklZa3uAf5epusbO1NnZ1MzCtqiXhXFgUEM3LiknKS42QU9fb4GSorLBy9LX2NfQx7utnox4ZlVIPDIrJicsMjtIWGt7gH+XqrvGz9ba2tXNw7epmIVxX09BNSwnJScsND9OX3CBkqOzwczU2drY08m8rp6MeWVURzsxKSQlKjE6RldqeoB/l6q7x9DX29vWzsO3qZiFcV9PQTUrJiUnLDQ/Tl9wgJKktMLN1Nnb2NPJva+fjHhlVEY6MCklJSkwOUZXanqAfw==";
}
