// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates quantizer-matrix decoding on real aomenc clips (128x128, <c>--enable-qm=1</c> with
/// varying matrix-flatness ranges): an all-intra clip, a two-pass alternate-reference inter clip and
/// a 10-bit inter clip. Every displayed frame must be exactly equal to dav1d's output, verified by
/// per-frame SHA-256 digests over the cropped planes.
/// </summary>
public class Av1QuantizerMatrixDecodeTests
{
    private const string IntraIvfBase64 =
        "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";

    private static readonly string[] IntraFrameDigests = [
        "41ebfa1aea574fa6aebe7ac9c129e9ed56f4e67c1b80d2c5dba54a54075f854c",
        "a2f955e3884558b7b7ae3f4b9692535b5edfac28833a46fb864fac3ed27b4f4e",
        "ef575308d9cbf985fcf3b3d4d2956616c755e2fbfdf5db386d385ecc1a6ea4dc",
        "70bbccb1d7c1b453ef9d02923d59fc3b2d2aca70fc559622aabf3b440995f76d",
        "6f4938772aed300f66eef808ca08e7734ef3a6810241362cdbc678c684892b56",
        "dba5225747cef4d909469574a6eeecc8c976d4a9cbd02c90a2493fdbb49f48aa",
    ];

    private const string TwoPassIvfBase64 =
        "REtJRgAAIABBVjAxgACAAB4AAAABAAAAEAAAAAAAAABlBQAAAAAAAAAAAAASAAoKAAAAAzf/5/fMAjLUChAAgQJEACGACAgQ7QCa" +
        "VDfdTsqvNScVjUSWHcP/Ae3xJuEa66vOHFcI2qn81hX7qgd+suWjG6+9PyR76hL+l6SPelDb04R4QJV02OEBoa4oG32WSeQVX75d" +
        "1JiOpnLGTNHqEfiyj4ML+1ercC8nHyS95kEyQtMMAKt/4uWorQzy6unLyW+y0cgTIofA+amsHs3vRArJP8n1RiG8tccX2pGfY/l+" +
        "E1alYNFDgJ/Iqklj0t5kgf18esXVgmm29o/l0QKceScwQYkcpVT6pMDIh8zozomMJF4R71yEKScODLAybX5ovvAKNsikSEGhm/3y" +
        "vXmxAT/9OqCgYfzGm/ZnGBSq/nht3wPvLR1IoA+jW+Dt8aBIuHewj/HyEiE39Ger0UGWSYhBezVgGqRzLwD6QWnQ0U4V3Rn6GWqP" +
        "EM1l9n01Dgv/bfo+/dccc4EJDSx9mQSBJlEHmIv+e6yGfA9O08WEzdsq+Mv76F6GoXdCZuRpYUJim0gVWSFHVuQdvI3izkFCUhRP" +
        "LdpvWTb5VqQhkFuva5TLxY1qDu0nwIA0GjPB3Q948eJmyJaKUDEBHAZnZJvLSW+MJG/6j/iPjOYb0N5Jd/0b7FbSTp/2ZVIjZCFV" +
        "GEgiTotMEx9XJi6JVneIjfq7+e3OcsqFJPe1edQuvJlmgEtIUAkUEZYmUA6TIhSXip7Ptvv2MJUARUDy9ONaj2e9MU2kCoex2HW+" +
        "SDjgG9dmHuCO/egNuXc0EekrNewhgv7NLCHEnpqdkvQuJTre/fEzayMm9kXqjQLfGk2Sza8qC7exEVkulQLQ54jQlMfrjvhDysSo" +
        "gl1CMAuh2oxgdTfYKcq+mMxID+BvWihuLgYLDtRSey7LRqAaiCh1/B8uOM26xvniMQXfoLEytaryE03nI6NXNxuSC9p1EZ28L+Pz" +
        "PFAybsch3SaEqNu/j+kw3Wh36UTt/fXXlQQ4yhU7LmUmXReAVhCPsnnchFgnTe2zzqcchKypMODeYbmDMRJDgM9IQG+eqQ+KXZAx" +
        "cmxEt9nOCYef+gYczOLkG3+cG2LUMYbIFmb5ewAWeeJhaqNhzrJTMBgjclkqCfCXr8rJA/2pwmE7AZxemauiVtWa0AmB22g6YMsu" +
        "Rv1NGZWgz8G9he6mng/Hp9QYV3RbcnHOV2fj+X8rdPtw2QWlX14JOflvYo0L4m8rST+nZVrEXV+TBVtG51n8qXsU6SyDiCviPr5w" +
        "pje6QC0BLHCb+jRpzHIwxHxFcIfet6EZ9YVEu/AmMyhzHi61i3+kR9vyitoOmDLLWy217nIyn9ML+0PlJBOYIMXYSE8HQ3PIj2fL" +
        "ppHyLPSJ9+Sue+gOzcaJs+oCwEyt+3SUxmUOY782jB431Z2a44Qpj7wh6EENh/vTfmfaV5PJxtUHm7Y35N23oZIrt0Hl0YId8ilX" +
        "vAJAbz14DFxDVP65TqfH9f7BpUDa1BCMAaFeJc4Z6C3po2jF/MxMBQJ9ClTPAbzxAAszPxGf2XM3/qoVRPFIvswIjfyivAh0Jv6M" +
        "9GNsoAhMXxiq538KDO3CrVVAQL9vRszXY0sgSZRCV9ByUKNDzXxD4biW1msWzP5IENahtzliCCpgCWrQB6M8mMAzgvAktX8njvnn" +
        "M0I7Tf08IrB/27gD/EMUgcCihLFX5JrdzgqjqGN5cEKMHLGviviiHgOudqNndhC7OxLA9sHOlC21egruIT7U7+FBnbhXCFGkb/LK" +
        "vymFUONGNRVjL90aPGl0WoMZlDnDnzqTpZpbuFM7lH+B9gDf0sCY35sqb++U67kjVoGNtMXMpc/ypMMih/wouL4Uv/d/frQPzOrA" +
        "bAEAAAEAAAAAAAAAEgAy5wIwA8CAAAB6CwkQAIgYIAAytQD+KTBkBsqyIecT1vzRxtFiiiEAAOru0YOL4qDO7ljAaGkLVzjviePS" +
        "Y0ymW21QrgIx7angkloWB1Z3vk0NvhjN/eXbv5/rbxUP5oFK1ZaESdX+5qPTHpFWnaiuN+psUb8GvjoUbBvjEFW6/mCL+xQN01Pi" +
        "HSOU/ZiWAVOPxAq8QdL+u+DA5bwDECZyQy7+0JVSioews1xl671u8y8qOIhlMvSQP+vKaodWOAlRhshKa5emkBv///S/YSzqdAg2" +
        "kGwTAZzMQW8vOdMWpu0FMZXRcce324wZiV3x/2VBQeEWBWI0/JNOwBQxqk5rzmvysfsomG8xT2p6kephhM4O7ei6ovjo1Am3XTYH" +
        "jK43kWFbeqsEjTcYZbH9bJY14IjjNa//9wZ1Nf8wMMBIUdRvAjKWMYosDukvSPyCEqxtJrCr8NHRcSpLUj9CaRkHjtPUGkJCQcbI" +
        "gD4BAAACAAAAAAAAABIAMrkCMAQBBAAAegsJEEAIICAQQvUA/b8NH1zfvNvecSBO62xNviXPX7HYCQbpyXdT6EkRfTWHvKJAkxgH" +
        "rnBHQlH0euwAVo+MyWlX1kXHwaGv380NV72GiG/H/qMisTV2cw6Mckvp9tm5bVjxpNJeM4VZfGMa5Grr4iKkmd8D1s2L6h3GLMJM" +
        "qICL7HGHVyFpfAx5UxCs6jhGIFSGywkMV/zzoUvSNMOIl8YUWbqgkswRq8GQT3HDf2LJ3Y7JHyNW1FIRqgEZRcBFlnT8y2BTSpDk" +
        "0X/zZ1KmNrlTbFeL0uuBP+mkhA2wP65M9eDB2F8mdlSsvMRUvGREGaxhvLpokZpljVvzpPwk7FeWoU9ul5aO9vfd3U5FmKGpehcy" +
        "3rEdv3qirc1o/6heUH5lg0k98XIKYTmCRzJ2yrk7gB0BAAADAAAAAAAAABIAMpgCMAYCCIAAeg6JmEAIKCAEQvUA/kr4MIkhl6s9" +
        "Lx2a1yRr4/UaS3I7Cj+ukrdmUjmzd7CfIl4rlVA66WrezqtDp4tXrv3A4U1rD7lDYpMq+EXwQQUCuaOL10LRx9/DIS8C40I6coSA" +
        "qqFrQR0Azn7XtlyhAB5oUp2DOMWToXZDhF36YOid4rVm6m/qPQ1JiOO3DOQUQJHHoVTMWEqa0xt6iSeo19i9Qs9YwcfqJjSL9tVM" +
        "sPzNSb7HG6/PEaf4belP0eS1ybUhcvxcraCKwrfAqsWUkuowKtJdlFiswLxHVrfnRrgg8h6dWNyXl6VdFn0Dv/ZYVaP60UiXam0o" +
        "XJJcM3btyHMCN/cA60viV1K3EDsMkcLTy+VzolEBAAAEAAAAAAAAABIAMswCMAgEDRAAegsJEEAIKCEQRwMvaAD+XlXloi4dlEXZ" +
        "rz8gX3/yGNAJ9Oicqkz0/TVqYEHNSosjhdwablUX/gyptlkXKur8bbJhUYZlRHxDV2JK/vwwb4e6BpECl3Qbjqn4vDSPJVMcobRo" +
        "lbmdJbP+s3qR8iGVvhjdjLlPOLlqVwY0mx/8P6XeLRNWBkwF9r2yiG/KO+azJJuD77QReHNei9nzd8MAC1lN5bNCcBBky6zdPWh6" +
        "p24OQNX5L0wGZzSpQXPTcLnmGSvHInCQcecsm7Rlps9zJAupgxlvVPJX4vQx4f7+LvCG7wADLennC0H2XTQziv2RK2FMfrhfDtdQ" +
        "HsU5d/DAofkIpFiKTHd1eIVSaa4lFLVQE4ofaRJiZ7HhEFG+X6yhupHohBeWY4tcMySCS/SAO0hmeTiQOVzgRnHe5yBBfDJoadoh" +
        "2vgLAQAABQAAAAAAAAASADKGAjAKCBGgQHoOiZhAiCggEEL2gAD+LFBv1r+Vn7TeLd//20cEgiSDtbdmqM8ZjcoX8vAPiCS97fPm" +
        "mZLI4ihyzmbS7+NTv/kHcb4Jdhv9bWcOlczwWOAyp39CIT0SXtIF+Bg/u3UioiiPCIA/rZ4daCsk0JfmAtLmAZZfD4CaQUv2Hufm" +
        "p8/IBHTTBPcHmgHFXk8sWi2eMoS9SWWMIHtjjv4RalOved0wxpRhzwcnLv+zZ48wjSEkd5cEDTtmXS7XzWY5sRywAHazDqUJddu3" +
        "9YbncTeyMZTL2G1zjmx1WbPKx7WA+3cp8AI95v0HjKfOrB5xvXTpT4pG7ef4qzmxp/ViAeNF53AFAQAABgAAAAAAAAASADKAAjAM" +
        "EBYwiHoOCRBACB4gEEM1AKi8pdr4lZuGeVcozYn5qnlCgkc7FSj+ctZyc1TZYypzT5J9hPbuCZqGx0bSBo1E5KLWbXRdBR1mGBis" +
        "nMlkAhoCQPWqmznrwDiQqdbjWkrNHNtBK7MQXTuE8svpg9z/WDJSIY0sneBXE1MuZzek6vwgg9SkbtqcQZl0hKXi3FngCYnbaJAG" +
        "LMCuKaeJKYK5UrUR35TMmeCF6T2WR5j5suO2LLdaY4T42uaqWjXn4Hu72t4lXZU3oZL9CzIIG18nx7PMzPlXo/LnVMEBgAPy1BHs" +
        "RBbgkIFsCVPkSrN5R+i90uackXPFdp5Gbrei9fATAQAABwAAAAAAAAASADKOAjAOIBrA0XoPiZg4iB4gADAoADiqltMaHGZHYKqi" +
        "hy4ZLu3NJtIJieJG3l4PSxba5e8Zdl+rtOIJTliZfbeFIJEzsLvJe08ayX/IRZ1Ji+cQPEd4fImBuFgWZPzp6vQgMGx9DeojpenY" +
        "7OfjOLgLm42MXln/2TuR41xjYtRlpUB6sZ2XGiV4kA71aRoYyR5y1rYOo2ZLp8dAHsykoh9bmtKp6DslQABBNjO1O4sknuvgAtgf" +
        "/3q7kk3L/y3ynpxcCFDGNW8v9WYS9Z253MvHTL9Qlgao82GRD8yaLEMApcpPAXNi87ljmut57iDbYBrP5NUWQSZQ25GC0gYgw+DC" +
        "1nRSBCvdWs3bSrN3QiTVgygBAAAIAAAAAAAAABIAMqMCMBHAn1Eaeg8JmDiIHiEQMwQzaADQ0BK7dcrQGVGKkcwMN8KWsARbJXJ2" +
        "V8JLKWPce/WRQGSAEc2SH+ywXT3QGt+tYL9Rls9jy0f6r+kKNM5+HsH0UxYJttjBr4BSEjs0Ske09DplxXoA+6kmJjQzY1SaOVJa" +
        "QuMRsYWU3OB0+ZQNqgs1GcSHQiyYijlA/1YYRNl0U9LdPF1Svfq6i3m6qXFIbNRPEIcAS4CImP55g/OilpkLGH3qwK7a1lGBZDkM" +
        "YF1kVgMc+Z5FCR0tB6KRnb4YSD6/YMdL7avqk7SP98RaVA0I3Xb9/h5o7iCLWvXNaH2PmB3RzZe202hIn5Vk0xcE9irBEn1K9eNF" +
        "RtgmUbtP2LzNAqjL7BURTqADLMg/cD8wIgEAAAkAAAAAAAAAEgAynQIwEgEfUiN6EAmYWIgmIAAytQD+eW/GHJd6AEECnDrPfeXp" +
        "S9iMlh0+S+KkBQa0bSjRixJQ0g0NEXjwQ/51sb6r8gXqTVwnSF1jMY/d0Z/9TOvTwE2woeUq/+qqFPqCJKpB/4ts/7W7IZjrtSKZ" +
        "X/2zj7lfvzMHxiRwOM/G3446hvRZcdVK3RVtgHtcJum0rnIEWADRiWHpebwT1Z/FbpHiVB0kI6bwhBY8hIBVfyotnGgVknGUXmOg" +
        "xIxfTTT6P2DURM7Fg81a9dCdvCdcnOY0ZErjL64CWVcb5ayBOa89jOt8OfjO+gE4pi98koQyy0IgkiOfZTMBXKiNGEFN16xRtha/" +
        "PEYe2OpkwV4IQbdBFV/35Sgekw88jXijfybuAAAACgAAAAAAAAASADLpATAUAgviLHoPiZhYCCYhIDcIK2gA+r8YwzFw9RyWuEox" +
        "YFD5sXLB7s1LZ1wr9xc7jEEp6OqyxANfk2onrCNyiZaTANd/yExpxiuQYJUPy2Y5tbLqG4A0rtXgdncPG9gY4inyH2p0R2c3WRZc" +
        "LqSv4wcOjJSInlqUMH2sO0YCN50xQ9rW74oIxtIVR1Qb/Vd5dVVk1qpcCK/6ZHPfKnXjcwmOPRvXYVbNTLaysH5/MF7YcucHhqFN" +
        "wn3Tf1BvG4o3Is8bfWELW5WRyTQqOgaKK3otIeTRvAFTkrIzdaTZ6i776XknrQhHK4Lo4gAAAAsAAAAAAAAAEgAy3QEwFgQNcjV6" +
        "D4mYKQgiIARCtQD9bCC5hqoxTcpfTJ0TpmQqExb1pp+7EtAAgHhheKWkRsSj0OisV5OkQf2t5Flie953nCP40mJaqSUam6i293S5" +
        "TTrp+fpR/gj+viqzt3ly0R3NTQ+LPaWqQ/E7rwuDwfYf01E4UxP9DGWQTLdvZy08814My+BXUeC2SI1BVePjInKTZkrhzYKVD8la" +
        "QADR1W223PJ3UvXn/WbNm5ltdj9eKjbpmI72/haSCLxUaR9OugxP6VePGkZF8T1+Gvky5CEItqkQCM0It4p8gOMAAAAMAAAAAAAA" +
        "ABIAMt4BMBgIEaI+eg+JmEkIIiEARQMraAD+M75nvl6sGKmgDcjfn1Ts22+KWHstXYXw/X2BJGM6oUMtKSEbddwvrS/zkpVYTcMQ" +
        "iNxwZ1ss99ZfJsV/NLGzc4G/qNq19clCtv9EhiZ7Lp4sCOU1dCKXY1n3o98LejH8WY6qJVCmlahS3ccWyF2gQfX8B80PGIkHKuYs" +
        "imjAtjwQVyKruGAqHtY/sfgfN/2X+aU7gId308D68kxtjGo7EAobotgY+TjXW16bG2dOiaNQvb4wcaxbAQuYprHMp9O1J7nZeLn2" +
        "x638wAAAAA0AAAAAAAAAEgAyuwEwGhAWMhd6D4mYKQgqIXBBCStQAPqz3L5k5k4Dvy7Ki7YIu3FP5xhRV92XycLF0RsS0zCEnZhu" +
        "Yr/TKcClYOTW/GfjPtlJrDaY9n+e2JdsmKaLlzV2Au5cE8JKrD0ZC2v/YFizpxWRgF4vYP37yIsCSHo1K++hH2AyNNj1g0E/JcmY" +
        "oX26uE7FgmO8uvg4sEYZbQwIYDdnKf5ZqhSAYRVjwyM25KAI6ociqtwmMy8rgC32uinXoKPpoHkstwAAAA4AAAAAAAAAEgAysgEw" +
        "HCAawhp6DwmYKQgqIABCsQD6cQ8Y03cfpvDwHOO4uO2qVmhNZvsE1HGb+hYz9XOtuG0UGtZVu4i3oKsYmgQdyEQ2EoedHUFnwvkq" +
        "M3oPAkkEmTZDxW814K0vYj0GSOGQqW7RQkB0VBplixJdT8ODhHCrTcxrExySDPZ6Pclhe/o/AXnxexP+OqrxwONIEF100+WhUg5o" +
        "INHTOs0RsEm2ZruPm99Ko0eiNwyz+BTsp8ogxAAAAA8AAAAAAAAAEgAyvwEwHgEfUiN6DwmYKQgqIABDtQDDL/Q08DAWcwfZv9Fm" +
        "UeBB+KeQx6imUmz+okZGw7fga/A+fWlevx1XUX08qp+k7n064m+On40Mm5451yh6yR55QYqx+Ea2iI382Srrp98bA7B/haw61uUK" +
        "TbD/AATK79131761Fu6E0EKuEnlD4LhbrLE5vUbHpuplhYg+/033q0lR8r/HZDdVTKXUxj3QJ4dwQW/Vs5T0VFW1AVPfYa5x3lK+" +
        "LqNHbYK9j1+qHQ==";

    private static readonly string[] TwoPassFrameDigests = [
        "1b3986b4f2a6ad917efd542fcf872e7ef8f9d2d0c90a27f8f93fd9893ebcb1ef",
        "22a6c23bf5b40c7e300a49787fbe35c3a7aa06fdaa1a51c633802ad8fc7849cf",
        "841e23c7d2d410d119d1dd35abb6a0d7d41e744adc36af64f98e959e68856063",
        "640cdd978038748e21ee21e1c371f668b2e7fc3959d0f275a135ebda709b9075",
        "7331457a2cd26fa1e9a51807433957b04db9ef7ffc420ee0e600c2b493116642",
        "6d9b41db80c13bd647bc549e4af0befa57b7e704c0623f471ff1887bec48ccfe",
        "e85239b882a4199813bdb7f8c5d1e46eed641ab216050119509a59f5e516dd1c",
        "d1555741dc097253090c472f5aa53f5e0cfbb4d1dd0188df3a4dfb367c1ead93",
        "37e678be1b76d55dedf3c8098378263a07f08a7546380ec9257097e1c9cb9660",
        "a115b24d20534286a8b58077d6226ed5bb95478e7ea1ea3b0237bac40cdb0d75",
        "5bc6fb94577dc7e1b67664f0e335a46b6d8a6f72695b49d510ebdff1ed71e490",
        "06787b72a8679f0e91b5f402624b57aba45a4d3736df6a474014c0833304a43a",
        "0df732ccda7dde00afaddcf439c1ec3eabcae9623db0f4465c35f7e759498d0e",
        "68258eabde66471d35b1e84d58e0a796208654cb1409355a62eed409497981cf",
        "d3f89fe15b625f8a3ec24a31fd3b1521a999832e1349634edcc5a20a3b666041",
        "c4a08a305139149fcfc5f3f0f617d136ba4346905811e6e54dac3b44cc8cf502",
    ];

    private const string TenBitIvfBase64 =
        "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" +
        "CwAAAAAAAAASABoBmA==";

    private static readonly string[] TenBitFrameDigests = [
        "021bc34ab583535f6d39cd0113b14950004b734b0b372a813a2c236d4bdf83a1",
        "a014c754767ad20ddced1067c2d2a00742a59d03fb06ad9fe0f17b7a4289ba94",
        "a5b5aee0cc5fcce4e4852e032358ae817ee647a7c532283188fde4139cb44051",
        "84de7f8b89ece792c0cb184a264a4b8e4d79da8f1655edb1b9e81fd06d379c03",
        "a5b72e89261436940fb1662ffd437877913ec9dfbf0a5dfd391a08909e3b3a03",
        "abede0238ed564e9fb8b5e5ba7078c9667b844b338d67c9abf564532b653be99",
        "c45c8aa4f20ca15cddf1703239b3020041119024f5220b6b8eef256253894bf5",
        "48d68c28d1619e8253c7207c18127ea5c9605007f4c379371155491d74fd2f45",
        "1e4001049d2dc1a3db60de5480cd9d7edef4d2ebaa6c99d1317f174c8648d93f",
        "c5ef24e9b7154ea1103ed131da36b517cf64b8e4d76195d9f65c302093d173ac",
        "0f683d6f0d1f85f52bfec7fc51778b6534e82f11cb8a1997b2f688c03cbb6807",
        "169c16d1ea842b2bb1cea7da6ae1e37177dfb23b4dde523ae32bb8d603f9e2c8",
    ];

    [Theory]
    [InlineData(IntraIvfBase64, nameof(IntraFrameDigests), false)]
    [InlineData(TwoPassIvfBase64, nameof(TwoPassFrameDigests), false)]
    [InlineData(TenBitIvfBase64, nameof(TenBitFrameDigests), true)]
    public void DecodeDisplayFrames_QuantizerMatrixClip_MatchesDav1dExactly(string clipBase64, string digestField, bool highBitDepth)
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
