// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats.Av1;
using SixLabors.ImageSharp.Formats.Av1.Bitstream;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates film-grain synthesis on two real aomenc clips: an 8-bit 128x128 encode carrying a
/// hand-written grain table with luma AND chroma scaling points (exercising the chroma AR filter with
/// its luma tap, the uv multiplier path and block overlap) and a 10-bit 192x128 encode with
/// encoder-estimated luma grain. Every displayed frame must be exactly equal to dav1d's grain-applied
/// output, verified by per-frame SHA-256 digests over the cropped planes (single bytes for the 8-bit
/// clip, little-endian 16-bit samples for the 10-bit clip).
/// </summary>
public class Av1FilmGrainDecodeTests
{
    private const string ChromaGrainClipIvfBase64 =
        "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";

    private static readonly string[] ChromaGrainClipDigests = [
        "7335c306bff06e0a8b299d94b621c894b9e38bdd440196096c4ce20622a9ed96",
        "6f33eb4159547757dcb741124b4de0cedcf4833f39bf01f9bb2de98a9bed6f06",
        "c7ef8528445362c367c4a34c8e7238c2ca0b0ab60bdbe234c7d805de819d1a98",
        "1f3bd989886837e3c8aa4c2faced54e3f55d5ade792e1d3cf24b7863195d982d",
        "3dd604bfdd5a625ac91148df2b67ac7c5f2959bcb7070953edf9d7dc6d823905",
        "7d824481dc801ed3c11cf3320fabec3930e7e4e7d5d4872b3c8301647850a99d",
        "172169cf60af6f6cddd54852484d4f2ba0c4784f0e4d8b1dfcf2458f976c0faa",
        "c624410978f76bb70c7471d5e1ee30373680603b4cf1c2880203408079a9162a",
        "7878d8aaa232fcf6e3f7a2cf173e7c8170f8c9b7fbbba1eb93a51717cc5ed654",
        "7532a85c53abc52eff8ce786cd1d4a5b10d209b64bfe795fa9ab5db19a1a6c4c",
        "9d88060aac0827a1bef8c611cf0be403aca0b06a7e1f3778e8805cecb51e7b1d",
        "decaea7bb0ca34fc8e5a942170f65c5f4f2ff37dc84ce751ec9caed2a40fee9c",
        "a4afc83fec20715de54f135174b1f58ef02eb6baacef22367177558b62115fa6",
        "5ce79556167efa6ac5fed6e086ecee68c99801a5d7dabfdd52ae1deb49d2e5d8",
        "b9ef46ad1491bac0307d5ab9bec792c5c7bcdb5825a078c25b51aabfd2c169c0",
        "ac3360f8498c40accb26a4945bb4403d2b4bb5b3321bad5e70e2aba631f1743e",
    ];

    private const string TenBitGrainClipIvfBase64 =
        "REtJRgAAIABBVjAxwACAAB4AAAABAAAAIAAAAAAAAADvCQAAAAAAAAAAAAASAAoKAAAAA7X/82vnAzLeExAAhaDCD1UBAADVKVCh" +
        "AAp/iggAU/xQgAV/xL4aJjVOIhYN7d2+OcW94c2908HuDgY6Ti4KCdYpxf4qHf2qPc3F7dGh3r5yBdYGTun+Pe4t3dIGDhX6AhYV" +
        "9dk5hio+hloWEqcmBSAwIBAYEAjhAifRPWZlSHSDIvEh8Se3O1g2NOszEqZF0aDlVsXklAKkh3L4v+002vfebocbIbIiOtA/1JaZ" +
        "QnUo9ss7ZlaIHBhb0PJw+U4+vIZOMDBfhQa/o2meAcNJCMfybl3MJMQnByi76uTfXmbdJVpp4gphorj4eSOMRVlVqEx7Zcy8stod" +
        "my1i8zJb4b+eb0y90HBgR86cJYx91J/A8QKh1maJz/IiNPValZen0eF2JWD8WZup1bnwwwge9P7qL2gMlXkBXJmaser+zTTgthoo" +
        "HYHCWuRn0fWJQ3HQ5aa4xW8e1xa7RgIr04hdamEwF6VPGGHWqbyOsUuxh2kzaaUxJPboqhBpEi5yBmxk2obZpaVSTMZIsdRrlLEH" +
        "x2Q8i+ekOz49V+Ro2buLkDedv7v4riUA1tqNpw1WkikLgrijsW51/INcAgpvsbkwVp0ICJ8xGH9Dx1DXODRw9FtKzU0erpZB4bNH" +
        "EbJ8DChpD3E7Jibx/FGUA11cSr4OykEIk+EsC5cHocA0pPNoHhLPOZLv9I62u2pTFmA/Ugw4LTG3j18RSYeiqgr1gg4ylgeqGCaM" +
        "5NJrwwZqnx+AuvZjUgcfKKQSXHzYJxa8ckMVgeKIpveo55vx4T1PNIGMpOPpe5ht0mTXJgntG3BqJhjyyRoGDU5gVyK/SlfHc0G0" +
        "bYyPnScs8KCtRdFGNgdHot5wkMDfNx6dRVUZxuWCrd+YACjIpXuPeySkb8z5JuZmmXRMA5hPAgaVyDiAFo6+Su/zoraRoRQ1gtYA" +
        "uGcq2GokaSPrygkSzfiMPxx2yH+ovWFuucJ7Hc9JiaDf5R2PeZk6eRV4myNRO8/yAMkxHGNnsmtwxbg4NeS9ounWS22soaAQfTXb" +
        "J7zarognqy3vtgLkxl2RV0valjnU1K3/AIedcKntpJCqrrwBmFAnuM0a00x2DYUoxaZrnEfsXnnNsfm73lOSEZhHwXWz9YKK/vMn" +
        "I5p2xMX4C99Ag+IQz5eTlrNtqrH8PHV2F7HgZKyrrzLptuOrrhJ2F7xqpa/GTLwyOg7wUwkYpaqze3ZlRdM1EPS+XOfVeSAJ8lnD" +
        "zhWq7/2Eti12DR0rnMkwMv0ib4xAdN9wmRUeQdaDdy+f0v7G0B0GaPpShwVJMzLjGUMNNUmSh7cqT/dZe4jvBL7mi0e8RwVicQsc" +
        "1tfPNXZ+RPal0FBw9GQ0+h6UPsbPUICBRJsszrqLDnlnqe5WtrtMSIMLybii2wnQ+g2OA97Ln/wbnRaNCdHOcHnmOfr2Mx53Zdle" +
        "srcw1mcJg3+F3xtFruQO6DfPqYUuy7/Q9J4t2Pd0n4B99oBz+aE+tErp53HF62W4r/aTokdclXHgSvhoZoxPObsGLwhR6skKGN+G" +
        "nmGuVZAtDVooSW5jpOEW75TuoKyeho0HP3BlykeDrCiE8nVgrOcRgYzpwO40OBkwWlYQ6RTzrp25yxDiGKR90OULuknotulqX4Ij" +
        "z71ZcqdS9rQc5qgXP8F92ekEGkqmPgUmZSC8bCYQ5r67ju00VdZZnfTIl/0z+TqBfsPvGpbh3YndoVPUp9GNZilRKyIc3t1pu2GJ" +
        "+nQ/IIu6rPDF969YsgdT5KuGnLle36SNZIGfpaPM1U7s6hHDdEYss1kZYpn4aN5wfSiSKm5eAEuDTht5rrJXgZGw+GruCHIr/mNW" +
        "PD7+6XMm9BZQb8FYCHd6RErYmq97SU0zYwpSb//TxxOjdtxOclcUmSgtFX1zVVvuzeYxd1qqz0yAGVLxGp28DnidW36CkdkgGVMZ" +
        "uiloqzc/vY4QsumcKaJoInDOd71dFZPiLLTQIeLb+nmUBix0YgAPa6vS1F5hIAJVnr2J33+F3TW9qfSfcLE6c0KQuIR/+Yh0B5w4" +
        "cFvW6781VCIi2bOLU44ImhV5lfLRvL3Q5td8am6rABioe5T1BuRWWVWdhJUO9sVdEvfue5JQNouvFCxpMmmCMaEbaj+HO+PkYz+0" +
        "mca1mlKdwlHF7ig0O5RTmCidl3v2Ow0GGRf4KwIerew7zTvdGE+j90M0yAZzJx0SHZgTYud1JuzpdBWCvLWGgMNTEeDyi/apmHSC" +
        "NUhVwuURS3TlX8KvnCVjNkkFXH0zxNcsHR8UI8XGW6dROV0Kqxu5mG+HlS2VvuzI6q+ADWo/bBmULQ7bURKdxRhnOQM4gJGcNJjQ" +
        "pcFw0YV0uurIfYxWLaTBMecvXr5uxRPj+ySfcaT3aXRZdeu4EsNHpLyWbxqXsAWg3xapL2rhW4SZB3n8vtb1UNLSOjVl00YfkzGq" +
        "keFdnuhhB7kr94ywNNgeJ1UeKGOWwRS11Ebjr4WiH7vwsUI+Q8vjaRDHTevgHwoej6kazPtT+xPFxa+zQpPJ28S8qMKBLFi/M2IE" +
        "3irBoJz/IeHW/jo8nIXggudXF3bEJPCBtWrNqJcC/o2SjuIiDEk/UQkbidZMjv+8liRz96tt+H+A6Hqt/rTGeAahhfFvYHfCnDmf" +
        "jPMnNV362c/JLthPtLX1TmYSPpgzVduhEXzzSk7/8uqxa8Pi3vSfjeulrLjTaPY4KPOinFXpUWQKfBQud1sbOQClmzAVXX7ZI8yo" +
        "0LUyZTY4F/A+kMYZ5lMonJIc9I1LJkwTo+EU7yBgR231zT2FfD/l4DkogacW734jXVucNlPGH2Eee9pqCqC7QoeQkYhWrMvJH9eF" +
        "8ozj3MfsJ6Rfpw9UXUG2D74EvQN1Cs8JSMINFprfJvmPm/EQGrH+/vki3LIZ458lyqLWK5HRbw/mgF46ZYg+iS3M5MxW5ooU8/FR" +
        "t0O3a66SD96dIj82Wu51LhfUZRMD+pAmKrUPmVhwfiq9aC4V5ubcrwnoBAdAZLM8RIQJl4gRDEU9/GwMdbyfW2GY+nYKNMGrG0Ib" +
        "m+hsZbAfdG/qIGE+QOwSKM4st9hhjzG6lh5qehMx9aduSl2AaAc6ee4DiXGzBNYj4iVuM9oIgVpRTr6GD1PoZoWOwrMQjE7mYpUQ" +
        "GrdP2yFqM8qIZ64wDK/lyu+UTuWVdW3BsgBtrOVnYwUBtyUN/rs6+HqVZnwfVli3EIgd4uJ1g8srRDka7XUXCfmWUIXIuCCAHFCO" +
        "COW1JOKAuM238ow9eY37pLpqS1mOJ3NC2RlGNDb6ReNy0P4b0oM5ing1OgYNxriZ+pisriGhFdhI0vOpL5/1B4UaToaBTSJ2g+7b" +
        "Z4Uzcu3E3orNrkLvhWB0y3mSi7dcRG4mwTdm+0n3ByjsNilIwMYjAAABAAAAAAAAABIAMt0cKAzgQAAAPRDBhibSAlIAABKlf6y+" +
        "t/5RvigFEfrACybLLYiw57KKn+piABL/ESABP/E/fHprc3x+f3p1bWZ3e315c2tVdXl8e3dwgIRzinWAh4Z/bo9xcX52ZnWxmoJ2" +
        "gZO8fo6Ie3x8eXx6gn+HgICBUWSJiJydg3uo0n9IDAgEBgQC4QqhTRO/z8Jbzx+H7efoj248vv4dPbr7pGQra6GW+i2FpqhHn33J" +
        "hqvpgRI+jj9mOZ30BVOYVNfTATLAxBE3C+9thMfYrdjkJe+h6Xk++g2272l8k+6fKy7WCJO2pzK5Lv9NcMD/bW5VrrmuCjh+qWSO" +
        "mah69u0dRMIFX2QEF10ZVAMGNC3Xz5znfnS/aVbj0fRhqtlutRBdiVGwxD7pRmaQ8shni9Dn1ob0H9khEW75K60k+dKCFJGkB/Zx" +
        "hITbrLuGz4mZAAGqAtwn6IxkOSLbZAlsDxPlJHp2pYHcTKovY9t0P0kyVlL6jyYwOgQUSuOgsME/9jJQHjPsEeSrFxuuAs0T9kT5" +
        "tqHAwru1GWNIJgb/Cncj1acrF+U65h1RoPGS71nshFT25ZOo1JDDJTSZhn1hVgELKcMdTh21fSXGzzV48cr4K5Lr0vzbVUgPubfL" +
        "f4Q9gNWqzB2P296S9+FuIO0gLng/wF4b6LJKIrUHOsPXz8/QOzi8HSbo0GuejezJI/VHbKpdcVS3e7v/AcW62YyeXsynOwl8czux" +
        "ORfZg1FX198kh6gXjL5eAcX6x72MwN1WKUazLsMLrz9+TUdexYmmWfyHXPZhp28ZexnqIf2c+Xh16GlaVEdEH+oTy4Qpsjkh9xXF" +
        "9bhStLrSJNq0XNG2a1XPqYecqJgXxElBWE4veMJTNUVgkKf0taVPym4T77u7vIclsbsPeJhUp9kFEG47pJLvqKkRvod/YtYesDRr" +
        "eGxwyNMPuBQNXk8G2Av43433O3Rx7xA5UJpfcNNw6yWeBbQGQ9xryOOfqa+OqVacX80ngAt5utqbyp40W0f0frd8ojzgqCvIziRv" +
        "Tsf+55YJB1upnXkqY9NQN14iB7/3pa7idhUz0weQ/Pz2pL5Q00BZj8iJm31XdO4NwvSjtTqVYbHCdN+va0v5Ezq+/YfU9tAsoQ5H" +
        "FqJ4eE1AANP7FHmTe3GSgUoRZajUuroT8Yr6SgO9IQclEJQ9L+O79FnTwcvCfp9PmmGx3+yvSxIZyk19Ex/zurcZZY2x1Rg3JPZ7" +
        "OsegQMKtpgZ40upPH3t79imeIBVyFc6AM/wnJ3ulPTV9Loe35LMIN54yH7LPPiJ5/8JREDt4dVzn+UFSWHvQMjQeyNRHTxpS3NSa" +
        "O05+Dc6+Qb8al2VKkRnlH9Id24AogpHI6wzJfwK5hKe+/L0HngAsqNbji4O4klNHb76PNRRnA0zpQkZgkzXwNQj4/nQT7U0+cGUq" +
        "EOtZ1f8LUmT83YIcCQJdfuoEj++mdiergDZ+t3fBk0ocdPaMN4S34WBDiUSfGmsyhtZbTU6MoEA7+mNPpSIKk9RbbaT3Xdh0SsxF" +
        "OD00dFpmzzvfx64NkNGDbIqH9uQJ1Esl2XcV8I0p8N5/fuYXO3vbRrdjLz2xucBrs2EGLkAoHCychcSruVlJ39BsQrDNr4dWi7mE" +
        "6zJLaxWYZZuK/MVDEBA46CLUHge0tCPfxsK/xOum50EheTNdU6qpNUxGQozjWhWe8R6CycZKSOUsLoMDKxsbQ6yP9tLsL3xaYThJ" +
        "DBoWUvVtxcXIwFN7G5jG/zx26WMI8Z14GH6F01BojWyPXf/e6iSLo85A1nIONnZ7Eonnn27JKmFHQ9fjuOv85NgUoOsW+fDAAPXg" +
        "4MOk4fx6ZJpdhzJusW8x1XzsVLRzfeVYcbmY+m3WFt1kRfTux7gUBaSy18q3Fo5+9Qd7YC6pp4R+B8uTxd8xA6kkpVaKEeP7NbPs" +
        "qZ6AjhLb+1+DVxftdT264x53kQXeBniG4QJ1a/pQ6yR43sIQhnSzWaTWgzq1PYtwQHgUL9Dm7npil8HxeUV+716MgMplguB8T1bM" +
        "pEC18MilNRsAXP2tIADClGyRGoVcGCiqgXRUDVav71hsEcF6eEiRVarMe6mvMsWNllKG/VM1+/hyXlqC/NbjunGZBVQKleXgED2M" +
        "TlruD9CTafIIBxYIsKbquY7HOjdwpy9IsMH047KCP4J5cM1pcOeSxrTCkwUWvPGxqO6Zs/ASXIU3gj7lUyhwKRi2piwaSkQr6s7j" +
        "OtLFwvJfDXUYsjeoCcK/4M9G6hLO48MsuH8q8s6E4COZ+CLwJ+HISmT+PEJkFYlcE1TLKiOFfhMRGTrEo1A9NnklQhd2CXfxPwuE" +
        "kc4fxy2kpWzT2DLywyru424jC27NsRD/dbrg3UglUHCvTZsg7+66aSNQisWadful8kEq+Om0xMf2O3tHzIeowt481GAkBYIZ+MOg" +
        "lpx3MxR5VWqD/hPe3KJWxyVONQJCs10DB+GWM2BUInjXu1YiVYSyiBp2lAqEVLHQD3YJxCEyWiKMjkU0dydr6HSHF4DTuPgvBkH5" +
        "XX/iSlST7lo1R/kgY9f/XX9qX/kaM8rPYYmXqVuQD6KSy/FYPOPnRhO1qJb4NM9UPz++YyszNb0TQWNfCnaTifNdCwrpmu0Nd+r1" +
        "ve8ZGBbb/MTVgAOvXOWnSsjP4FgZLUXk00xgzo8R5jtSPJ/OLQ8Gtcuffr749kpBCbQ9gj/DdjksT4jv+kXd2laSwgeV+83xWLCy" +
        "lMpLs5yiL1LBC7o4MM4+WPXCWIFNvfaVnOb+UMnHeN3LTKf5DvsutL4a92GXM735yNid2o08QiPRIeNClFd4rejZnjOKOMqtejeS" +
        "mOT2Vm1GUVHwji7YZmDYd8AgW1fPU/3QHv0/6USNeDpbpUT6JNta60SSJLbgl5gbBUcOThdnTC9vo7YgPWNGJHnA8iuw69hIKi3S" +
        "jGWt209t8FD0oHqqWc0CWutToOxU/bG8Y4ieJuXhbi9qwDbktj4Wyk/77mkEz8sIrIJukfneHvMS505rBSqHaWF2JYvpPkyZXJaN" +
        "Po3jBjXb47+vd8cPdI44A9/x1xZb9duPxSyr/SmBtNoAhxFHqA2Ddk5VqNm2Hknv0cqsO0itxf2/Prm1t/6idjA64CiULMcb/lVn" +
        "4CaUk3P/eMdUrtw2/IO/E2KvhtoC4rb8cwixdxjJSKG/2IHp6R9AJ0K3QKNDbQV1H99Q9a/R7LmIoFvdjzUQIrmDZ+dprv5WU9t4" +
        "5/F5iP8Prk2au/A72dyzW4tDCx+RMCBNpeOfKXZPCiLTXzoMWTYFlBavgDDyrfsHpu8lXh2qTwTLS8oDvJuT6LsrIH+qdyv0T1iM" +
        "wf03/xug2rqu90dz5FlO13ngn8HCFFnc/jRMXtxIHo3zKHKFWXG9a7fXY0WaennZHA+djX+mShpJ+3XEP5pW5OCitdkBzt8Jsp07" +
        "sEutVFRrW7NEte/0rHc4FYYdCBARPz6y24d8UfUOQ1byIopSjIUAUw6EwfQ73/UnaQKxXZ8jBZ0vxlorieXRcaRUC3GXBM2BIWjl" +
        "VSdoHl7eR25thSLNsoxuaOBETsArPnnB4etjlsOPyUodEoUO4V3/AuqWKnKmJczQwBx0lnBY2MVg4uuIbwFBZWsMHBY1BVTwLou/" +
        "Hn6x/LSkrqIPRLKVS9mh8B4f1w7ih8DrVvG2MuOVBrKjoiTG6O1KLIqBiwVcI3jtTgRab3pc39uQjQxb+rWGnqiq2NyxUwKUfouf" +
        "lJNphSyH5hHHeMzbW1rHGKjv3JRJ9Uuz1MtMke7IauOLUG8Xn+1mcYhaelN8KIVQVn79+OcJkLCqanhh3NY5rLHoIlymj1uVTduo" +
        "yMwovI3j8utmzvbcInMQbegq3loZ0FekR2h+jqZWlh9Bhnrqyv6B1O9Dhas2XkxDY02Ma2x3GyTSKccKNAGRTIzH0rF3KfVYYQDT" +
        "Km3tEXl0VwBUvXM/iEDCWo+8hpfJCnb75cUmuqZ96Ib8TIEdBVUufqe5U7yIvd8ELeRUBAbkDapX8xFQTlALFVlgqT5OQGfVkbiG" +
        "qswF1QTZHNyic1EKlUU0QVSWtt2flz1F9b/AC8IQ409miX6J4vn/p21NDdOWlRkYOrORr6gXY/C+QqJprGau+/Patiq3zLhqt0Wx" +
        "8UNAa6GxqVZp86UezEf1jAWCFGwS1X4EwN9dViIqpf33+hasVO4fTf5yvBquaQWID+AotESR3/ZeExNV6Bnq6LCh9rXaUj/55QRW" +
        "hwxS97Y+xMcF+sKsQuLu4w568hBwHaN5hSCa+5Zitu0qmEexbFSLIEK9/gD+exDGjKizM/tWeb6N4Wx0djgxLDmDgdI6oR+Kb6b2" +
        "yQPdOg9uQnJAVJQFxGdQjLfJWbIdUXkaPuv3n/lOD6Q3A+9wasCeYJP+4SUsgcbwDREGpwFTMVX8/KGGwWp5+5hFjqoD0Woy620L" +
        "XoHfPYXIyIPjrTBuZ8W2QmkT6gkcF3k47NgDbX4d/d7w111nyUQ7Je/ISTUgvVCyXFJpeHNgIBjDNaf66JbpQ6Vn67QCBbne3E3X" +
        "sme/AXgMaIpleMd/B0w54bUOEcmvXGQ3WYBou2JfEEwolTcTrwejSbaVW3rsZu28eL/9yUTrXM3nqUq8dkm/61fcFp0CbzegbySE" +
        "ygITnP054s0WIpnOVggBgdnpccnkcDAqx7TV9xB1FQSuv/QHc9FBCYv7eq9qVbO2uXv4FJ83GCik0OujzGlvRRwO4zMS1SzPCTKs" +
        "dS0JMp8qJB9fg8DRK5YZ4DYzi7mXh/DBUJFG3RJ25z7k1x1mi0F0FSDb8QXpvVGF3/7zl1XIJzHpixU6T1r6h5XIQVmSU9EM4saG" +
        "n7VcHlk0EGB9bJ2mZEYV3ZiS2/NCrtHRrnPwJ+5ulxf9TwSNbCFqJZyS9NU3BTk3c4PdBK5cTnQy/xMoBuCAAAC9FsGILtKCQAGC" +
        "1/Sv+7P/r8/x8gV7ouANRs1NjNDrsotV7D/sIgAR/xQgAT/xP399e2h8foB9endWenyAfHl1RHl8fnx6d4KEcIpxfYqGfW6ScnF7" +
        "c2p1sZqDdYWQun+PiHt7en18g3t/hYZ6fVNejI2imn+Crcd/SAwIBAYEAt/9x9x6tKNsSKdTBALvFQgRAEhAiXef42CugmZ+ofsg" +
        "VuZCpz6S2Lg+WsP0+/CnOFAEfZ/T9wuTF4NvEgiZX0r/HGEo6VapzTOi0nUPvnOTczPBXKIMvXu+dpPJLY8gGZpbXDzb/lbepW8m" +
        "kMwD24f4Anjq0pqNlXM7t2HGsUGF/Yxx0aChX0sxuKJW5WIUAgPocKGtf7zpzzSbQD0W8ykmgOStKbiFE3JDPIysrPdUMZoYPVd9" +
        "sekg3NmvqS3aNQfObTq9/DbwVqdGCTQEqA7pj9LNf+TkrMg2R9KdEGCjOTvWVnWziQxL8Tng11Orfpgw48hMVeO6fakcRXdcUyyn" +
        "t3LqpZ11Xn6VHXwd5Ix22XUq4tIfd3+kvmHr8TbofKWpN1r9/Ll20EDnqNtyMiFBDDf3xjrDKSoYQgkZlwhYLFR7dIc1uvjBy2Zf" +
        "0AVv1zhHmj76ZBRQ3Vug9dg/1IgHJ4r7Lx8VqdoVMYBGq58joNWmEo+rqXiJflA2p0AlexrOVvlTqi7b+5gxWm9728MyOPF+VfCZ" +
        "/xy5BsdoFzzaFUmTfzrb1L3HHKtY9MdxFvxX6RImbqinIaChY4+jiZh9M3ITiucq6pSRYF7eWdcjJOaWPu7pzHYsml9gP0zzX2Dc" +
        "ganBsC5pMeV7KGB1yeEA0o5NQBStj3hke34oGEL5V+16drPFeoWmkX6P/Rk87bCeCDdHnVUvrklWkrSpe7izP9Vi0h7mXKri3VYy" +
        "vPIuAHKBkEI3PSsKLqrsuvoW7YJ9MwpPdduZwD4i8Gcg55KxfpdIsOQjOwXTopkrRDSqMhhMl2NiyoIksK1avI3L9JzWKndMSkml" +
        "CzEDJO8OksvMRikca/EfMHbpO/zqgZDLB2jUspPrF4/5H33UvGDJ4cQTzAE0hlXT/45hk95ZveyMReJW5JZpGyI/r3Z4BrLh5C2x" +
        "/69WXC56AooFTKPBvuOmId7bvTOi7k8uICipcf0tUI4dJa5X3p8xhk9BiQ/2eSHpC/nPBwI9tfS5M5IcaXMDkdb0YR0Ww6u7x25F" +
        "iM6RCEaCRm2MOh1yXaJzogvOOrfvgZnNgH3vdEiim0xTqTAV3qCf+DpW9IG3Uj+AYU3ljcfYg6CwPiUgjkzSIud+xjd5SwA+/w/U" +
        "x3I2kn09aX8E/Fny4OBleFayci8jwh29rqNHtSx9JqIcEv4N+0y3aw0mzjrbjx8hq0ofaPFbbhepoBn3I2wyCur4wWtGD6ARhAbX" +
        "YIwysi87ejX18fhEY4hVL92ToTUVJkiuQIQv5Oaqsiwq1+th04wnK7LSz45w3MkgwmSSULzYme6/4C2iW5cW7YjSECRPUP/oLlCf" +
        "eZF86cWSmV4vByFA8MseJDvm+i2WVjX3cLwxhLsWXs8rRMhO3xLXga3mCtOvYC1N35YHBhC3QY6sqPKe5VnEJHrOwMsv5wPMLOP+" +
        "S82g7sBpZu2dFIkR4goi5ZI/Cuo4ioS2DJFUsG8jTm4q9veA322AvdqjhGjvICzXxkkSE4qVGdlRUYXBIkfLwsn2DjR7iWxn2UYy" +
        "30p3lcYO81zgBh5YfF6FIPO5twmctvZ0efFp4nN8KwguK9x4RZpXbM/HjZGB2KGRn/u4zQNxNrHyqzU527g58TI7wXHILJnElMbH" +
        "Rwn7S1iVMAI/VC5zfiJ+pgv2oRQLhp+DISEuCvdSqcTvlrxNvdmHB3AaZ/n0IZ85zSFBIodDi/oDtF3LrjQUsEEnG79KwTTQW5Os" +
        "MNUbtI0GhK9KRTJagwRXIzq2I9pqQV66wf59t5U7cogFniGCelM4V1YSdmRpaLsXWitfe+RP91ndFVGYUsONxmBWyT4yBuuBqS1N" +
        "95Od7EM3wzjha9rGzVnXfES9nKoRjM5drUy3ca/cRR6d4q6kx39IigvrSCrRmqpB6ez03PlE5uFnddaPAIsDxQkXgdV5cqwDNyJ0" +
        "haJzXrmbIC9XiZ8psk8LbQ+a0xY7Wr5bmvnvtjcCjZt+hXM+3oeTqnuT/m7Hcq+qnFHeFZqaZjX+/XDhMJp09NegxwbtF1miGEIL" +
        "ymqj23t5xih7p1v4NwssljB7tG3qPrbU0XZnVvyaGszMIPWXQp9fo0BBYw/Hl1CT9yuaVxi429uOt1YOMl1mID2owi6ZJumAFejC" +
        "TLJjO6O7gduxJqJelEuDM7INxooSnMQTt95zmxs0oj4aR7R5oBMa7+JqdbzG6vgi5h/QKWhJ2RgWV4SveLfVj10ZCvnhshLgiR1F" +
        "Uqr94h+K1RTLcNclvgbxTFAVJKQo9GhCoE5hMQftFyvOZMlDAHfOyifV+SZ+AUXylZD7ikG5Abhd1yfDLzUXAMKi0smIDUZh65wG" +
        "/iKSB5s1r6Slo4MHTJNZdOLX/n6KF3OZDrUROWg1Ebk8O/wEAvdj+RuyOhc0HBEtPdtsk8N0bdA6zX0FPw0fMDTWnAuuD8i8psoy" +
        "QttXgvg5GLIEC6h6gPCO2q6zTMRDayzdQtQl4gO2Cm1Or6G1P9cn70qyJk64uvKc4snVDE0BLvofp/U482xFNp6bwM68+Fu9yq9J" +
        "boXCieS2EwNDTOYcRxeYZq5DxkrGlNwGFxLJ1LMjOLBbrgyQQMFuFgQJLj+fUZodjBJ3e3dJgh9gb4r7ErgVtr3J5I7RkSalx/oo" +
        "1FzyYZj1d6flv5UicEu6eI7rv6eBpuTTxWtD/tMEiCKm8fvba/XiUED4RvkxCKxmKPUxNj+9UKDG59tiHnc/y2QUtO/DA0Ks1Ntw" +
        "c0DMXNwvcHMRqnPooh3X/XrYtVOa5ZwbfgKMTo13DxfVZ799eOEMykJLKFyCAuRz5p6cO9N2pKXl16Nyje7IFL4OCL4foKDZkG0P" +
        "2bS0sDEdJ83KESKTHA9KA2nCoozELPC1tISK1hKzya3dUz2WgaN7VXSxxnZcugZXWFQuHpvU4aLU9na9Yo2+6aZXYC5pm6tLuKiT" +
        "EWI+M9OUwmT3xFpZ+Efh8SXdtZ60wYB7sP3TGoS6zp+9vrWH7FysiNwv+0ZTgpIB6iHIezPQ0HA5TMYo2KSMiKqA5KWuBDLrJak+" +
        "jG/Z9upFmPnqf9oPmymBDvPTiWBmq3z+/8atBbdJXjw3zjn3a+/aIwF/8GlaWF60GPyP7v0APGAvpRWO/tHfJ0q86lsGpMALnmXb" +
        "l0SUB1nYNeAfHm7mUxg44EAoU/G/MctiTgJddJ295dZfvaX6WHJmU+z8Gfrz+tWcwQ/NwH1tVujXAEO/Qv/yvZMoJKYy2Q4oA4EA" +
        "AEC9GcBCtpQSAAwegF5UrgDobOjY1w7DKL1e3f7cIAEv8TIAE/8V9+fnxlfX5/fXt5VHt8fnt6d0d6fH18enh/h3GJc3+JiXxsk3" +
        "Jwe3VodLOYhHSDkb1/jIN7gHp8goKBhoV9fX5QZYyKm56CfqLOf0gMCAQGBAIN3WsMVX7+/mJLmifwheAMb8eBlAuE3tpEYzY7lU" +
        "lD6ABTKGzF+6n/hX1UAG+LAMbjtLs6caJiJE7g9xYwuSrSvYIABF2iPGq/Xs3BljAMMqjopakxkU5M2bFYsIZCFdDAkNlr0Oh+HD" +
        "5wM5kmm45dzHn4A+Lb5I9h7GGV2xwGygNBcyr5EqBaN5d9cLC+SefoI+jWoFtCV8KHcuIo0lX6vzHMstHL1mUk61+LOsKD5MIaNe" +
        "jYVgxcF9uZsRvl05BbzD6O/EV0c3F+T/nbRC/GsbTWxakbd1WY3crENTHq2qd7tFpQ+0YKBF34Ik0icpE37Q+CCQ9zxf3OvTTpii" +
        "2/Wy4oR+vfii8YXNdYYEg5cZ7lrwqwomQNW+23p/koDecIUtbxrpNzrnzEIPk5tAmW3jnS2M5SPGRYUzmwjxmxS+h76ySmJuNROn" +
        "wys0pQss0SeC5X9mOK97UvfdVMcu00EdP0sg3dZ3+0R+uMGdUHUlw6qgplj98mZDmHxXiD2MY+5u4ulP2reN4Kl5630bgBngOa3d" +
        "I6LUW60DpnhyBUwcgmaJpLNL4ljPEk4GW7X9ILsSnNXXWsrXkSBUxkVl6Xhv58oacrVNQvPMYFUeiThhKFOoA5vT0FLZRRpn43nk" +
        "j5KB+C08+La3t7MI+JGOB+0olF/Fx04nkQ3NRFnnjgPUnJLmzcQKjw4nEn1Ruxoe3YkKBTz+FCAHxAZsZumW8SmXC0w47MrehNwU" +
        "jZS/r/bAmg3k+vPgrSDaMKGAXX1GBL3lFwVm62NJpwiFU6OPhdjNJZl7xIpuatCVnodeQs0IbqrZ6v9N/fsHox0h1BfWQQ4kWszl" +
        "2fQA/vYL9qF3SwpVnZMO5+EfzJNwSkQNRDZgPHCC9uMeshWiICX2Q7zbBfctuM20chcYHI/v31Nemoz91YE6BXIvPS0gPWVk5j6D" +
        "AoMPtgz+zDYI60KkYEFx526cwxqfx5dsQTvPOYV43CS8GYospg0I9fTI6tOnwCaEhjSZnWuxbXAzasxs93GVW6SFAkRQoyJDxmWy" +
        "GeLjuuvCyWvkAvIrJ61TwSNXCWMzu4PwOZx2hxkpCzTFMuPT5gRdKVE1uzalwAhG8FF/X3u4yAxJU5xCOnmi6RdTS9QCxUJuh5kc" +
        "09UJi0to9R783K11JUGbBIyLRRWRg+NmSh1ViZS1dK9guw4WorT6XiopPW0QoxB0vanwzhbbtakrIvlt8HaPVGIHghI+Khilm278" +
        "ZxEQSNdDo+s3sFAttvTYcyxjHMcn1cTUXBpAGWuq2GnYYJjwrOimdBgGinTS6w2j1XeXVhdViXMhfiNlbeA7cjI2pX40/SBva7p1" +
        "AYf5bhzz/DCP/5mxRZtx4KVZni0PB5n+BXH+uE7ABosOFe/DPJewFoZ54YH32mQYgD+BN2k70O6WggnZ8+NUDbh5Kbnog/sGx2M1" +
        "2aYxvhT0VHsSAA4stn3Rjo0yn0+Y46Cp7SjmRbMJ+gOhFiCcsJD9WU8pHFaFuRSEQT4nWkjqG14ERIKO2njC05lAlI1uAtwKII90" +
        "cw6G/Gq1sWtWfdsyLxR4/qSPhU/PhJQ3kyE5GBNDwznMWmCA5hM6kEHZFDwZyxi/EKAXmyuk3Ze1pm3bVxflz1ZWv+ZtNAlIL9aF" +
        "v5wZeAO9MU9e5UpvOrnBOjo3NZr+LJwXz3dZAAtKGGuPOVOBQ+Tvn70ludLXMdW7eXICPAtJFL+4f6gL6Ur7bXA9k9l4OfzOH7BS" +
        "w/B5FA0XyfgJTNMzpPe62kK4ZNrB0Fc5/wmcNgZZYBA9xzOlgHctdllictbEWgts7Y3c8ZaoQUTpUBtRdY2zHXJ6esI1nXoPAYRt" +
        "gbKa81UAbf4JzmtMRn2HroRKrxVWols76x+VGw7HXi19sIAmDqVOJ8scZ6JXTpT3gy9JuWc8Rsje896VHnRenjTyce1N+6e47d4q" +
        "dQ1PlrWH05mMcznPpyRzLYc77e0Mfuy6b96JCPV0Ae5NQiic1cyAFLrk0fN6VEhaXLUxGp87ECyIMPoDV31MtsykmA9hYEn+F4cI" +
        "peH5XespDS4cDzcRgFjWpvjOhvH/cNWXTKSdTw0Frn8KNCLAskePOBNHpRoRIzVXPAvUF+h/VIgQAMya4m6FS3A3qS7//a8Qz1+5" +
        "J3Ane9rRwmvOWJl2R2YPl5yxIF/D2hZO0UBGSLpTd4I87s5OsWLpu3mAUZRS/4/zhOqzJCsXWMDjthj9caGdpYEGP2lmS+yQzCxk" +
        "81/iI30MSqw2TCwwDH38NcVK6sgDgPJRdfN8ffyF0f1+tXIr4qktkIFtFBiWksdYVvMwDBIxIbYbtbnSu6j3pZh9xAugrpfazIsh" +
        "U3oygwgwA8QAANF6NoCELqgkABA0Ayd5cAQDZAbDR5M5QwokX/QRAAn/iZAAt/ifv789Ob0+vz8+O7O8Pj6+Pbsuu72+vr27wcM2" +
        "xLi+xkM+N8o5OLy5NDxXzcI6Qknbv8NBwT87P0HBQkU7vz8/qrBBSFPOu8HVZUAkBgQCAwIBANqba0W+Io8vqXutRalvPRJvVpGu" +
        "TnysvT6m8m1//oyARAGhNmlmTMtkWTpkFYaFr3kmN9Yx2baMTmpJ3tyNpxfWp89hCJUfB3L8n0gJ9OS6s3HKN9/qFuxVPDhZVccS" +
        "yYVbbNpY3DVbGggyJwA1KH9zKkun5aRm8mhdweLQWTkeX7JDZ+EmlrWDGTGJM9l708YjYjYT9IdIXeEFdlNFecuZG2vsGEV6qqiq" +
        "akrYYkZcppLH7nAVTRFbbgTlragrHGvinwqNx2foHufp1dAdzpMZdiSobOFV2K58I3zyfKqJzhaEZYDT66CHQfgF08RTzzwqSsAR" +
        "WCLeaRAArEJgYS3WgRvNKydD6LQnF/UyjmiqVV0Z6mYbrZtqz4cmr37SlXsdgNZDJ6qxWZEnaghD1DQKBG1mLgCyZsNGDnXcqGEy" +
        "C1ocAH7ImClV7vEeS6EnUVuoBMpQmxPtgw09AuFpe0qdsZrGXRdy1RiP2rd9vF2a7CNrqf/mcmOb/T4Yi10fZeGHB9ISEuvzs6AZ" +
        "1aMAzz0YxGxiXWafZNBFgVZlmqnFh1X4LAZ4xgFn8ljR7ML33Asj3c4hKR0Zsx19veT9yiu/m0mtS0/l7rvYUZ1IZ36XO+x5tW7Z" +
        "ZeLgHOTyTVxlV8HZ1HFrh79EfTqTqqo5YC/3pcnBkOT8XdnHVPq/dgz6qlzb0gr/QTPYqY6nmVVVmhmR2El2yO/SBv9CJJAYaGFG" +
        "TJDtuVCjOm44hjeZRuClhBSVmtkl5dniLhaieh93JlDAKr1az7xxOiICn7LquGSOWkGSfXkAS2TKh3tmK/8gZr18YH8rLLSU3qL3" +
        "7U0MkIp8gZ5eSJ41WX/M91DqxpuinHESxllml2MsfzTJ0z9YEg+droFOpJ/ySa9YBiCAfdJiMegFqWtSfHLuGFzUqq85Z1TTAAr6" +
        "SizuYnuY2LmJrMDsUTRs5YNodZPZAvoPmi9UZSSpl9KT08FomAvR6DKBVcE1zoF+T31GG16z7nFZfEQdiUndUm5cr12S06sCykCb" +
        "r2bZspI3Cf5x/KRBHhXxukzWzRP3pHjeG4POrfa/n4kICYa7sEWKZvftIGPA38LMVFDWe+bWUmCUK0rP0O3wEeyGL4XxCBC6KyMO" +
        "ol/Vo8tyKxrcNPWEN40Dp+b7BnRcLCgQ1Tt8AgLL77s1BeytI/V3ahhTvoTk2OLUftRMKHGZ9gGAGwMAAAIAAAAAAAAAEgAylgYw" +
        "BAgQANFGjaAgzCoJAAsPwDXE1wB3NndsaYdklF6vbf9tEACX+JEACn+Ke/Pz4yPj8/vr28Kj0+Pr28uyW8vb6+PTvAwzlDub9FRD" +
        "41yrm3Pjq0ulnLwjpByF9AxsC/P7rAwEM9xMK+wDynM0dCTdG/wFHnPyQGBAIDAgEM+dpkxM4CaoDGVf8Hd6dLxtjm6i4owv2pAX" +
        "5Kikm14OKiMoPf51uvBrtkHZHlcLnTwSS43hFuwptHi0fxfItxkuiU98k8rOmDqCwRc5LkD7U+pZ+nOJkS1p7CyYc954jJDVMZaX" +
        "180K304Gj4FOqzzQ4VD18dQMR0BIHyB7DCdWT6B1Sv008tjQ8WmMERdBJcTe2ImeV0t+YAzwv2b2lcFKxGCI5YhfcwB9aDghnA/S" +
        "DWFNI1XlZvp0NZHACZqYkKtTtwoaV0ZZrJCNRF9aFWl3QYssci/agtcXZ4+M+TK8IBGapkHxdPqRPSU8LWj9ZBNWIdwKYSYecQjo" +
        "wrmEBoW/HRXD45SCpHvx3kwdSVIX5m4xjY6zSZkAMUcYMgjlzgSSmUN9hfYZlRg6DtRVsWJJcDZqDExo2uzO8KWQBIXROyVRJ14j" +
        "515dGTtDVoRdqKiWAJThbNQisOfVwb2+P8ngGt/vc5Dt8bENdC4UQck3rKS/v1nfHuiJ6fkeGvnvnPiUjGzvYh8UH4EouDHEzBgW" +
        "n0dPA6Z6pX1QPvP25bwE7M7dlXorjsuN7ex+ElK5NTYqdUsmPLbX1DM6D2VWMlZebV+i6Wq6Crd3Gq3YG+6MCDWQbyCqeSCZbegj" +
        "62AuEeTnd6TYRcL4c0TJr+cTTs6u4lPkj1zTFwlUMT5bH+fqsKkRl1vClsOpRztWIO4m8hxoW255chtHQT+B49pMsu8v92h81xfD" +
        "Y0Qn/4ql9fgIoqK36snXNTK9lAVfeTY7oQzaciEikz+WrMsIegXvdKY1cPr1ZNEKCIJmqzqDFgdmIlDfeHM6/tcC/6gZrh4p3aYW" +
        "8Y5VFSou+FW7ETHzKuCtbtigOckG7WtZuzWOEqO35y6x+GvCk4TqBQAAAAMAAAAAAAAAEgAaAbh6BQAABAAAAAAAAAASADL1CjAI" +
        "UA7AgXo2gIUOqCQAIDQDkSFwBxNnFsa4djlGCvbf9tEACX+JkACf+Ke/Pz4yvz8/vj28qb4+vz29O6K9vj6+Pby/wzlFOUBExL62" +
        "SLm4Pbo0OtlMwbrByN6/xcK+vry9wUBAQkK/wD6ps8XGTU5AvlJlwCQGBAIDAgEA2cuxg7Mshw8HRe52rCdQUgKx3U/IjKocH2l5" +
        "af8Nb2Syb6pXAlYIe/CxfEjF5NxKorr+6OqMQ2TZXcx+Yn2GJTS5DsaMuuGgfRRs383lu/epLulDe5X5xPnfuz/xg2qRuWTk9vqB" +
        "TQtP4RQ5Hg5f8zJiZ6AXeQjQxFopScuTcLtFAsSnX+BnzaFqLqMk2omoIi8v/Cv4WknrVdT8hwd7O66Y2A0RlIhkQjcHrolmkiHo" +
        "sWATMynnPeis0Ez2At0dKTObP/4k+xUg58zJqwrLoTRgYNtYA7xezgiqYuBAV3aZm+JHTVoiaS8eKrmIngmjUCAus+Qsd2sVA72i" +
        "R7Czs/GEAau4ukuAfcBCRPUSj6tWZ8d7qtlkddSnSD6Idodb5sN+WmXmtdJnMFtvMqHc1YFGZfLA6N6UnyHRHJsnaL9AySUSfX4v" +
        "Lghh36KPz2TvuAklVslTH2j+/jbW28kROur66FSihXCOOia+9CzKDg8oT6SHKEBFQIEZDWvnyhcbqfuR4GITpUvOSjQ9mrJf9wf7" +
        "HwvaXFNdNzi4z1CEHke9sJp+tUC2QXoSTKf4KzbcIoKzeHXxGzzof97/uv2DcLB9cO57HTUfPnqKHkuADI9xyPX+AeuMJqVWWwfs" +
        "vWjIff8lAblbFNDNI6fiILpJ6vFOVUnLvlZiHvpNh5AtJ07xkESnvSJhCQmeiNXMw3pGiCd+jSfr3oSDlXe8NgkwNoKDEv8lESdP" +
        "4CQTNSRHdOQOxRN1sgR0F+fiG/yN6vgBcNwpxRGSBwRW2f70ipM6aV76vld212txoJb6e/El2O9mk2vNwwPmsOxOz5wYb1ulZ6FQ" +
        "oS0F3UcsKRpVOPupGf6J5kFqfNb2dXozUVMY0dPaszyL4broyLUARVmMQ23SNv8ZoY5SY5j801PYh8U/38OySJnOcLGJIHp6QfjW" +
        "xTsdg6KgLYf+Pyv/QRqZUd777oHKrTXqy7REjupFoR9OmiAOrFFiMSOPqIU29kMBEtShbtRSE5tE2TfyIanI4sJbPQ2YsQ2IliGR" +
        "BNMLAwqlV0v6yybTGLBdr94HY1jkrsgd/5fKMQyeJti1Lnw04IIFNqg/lvIwECWskuNexdgXFIrCe6/Y6aRKv2eYaJ6iYRfqt5Fr" +
        "ilgGytXvRNENBQByNjhlRo8gq0Osq0UEg/wjR392K1HwtZ02uAgIQo377yZwiJ7C2h6bn20rNsvab9C1ry3eYj8MUX0FSm4CcXra" +
        "NL/sBHbYY1o1ODNhAFFLlMTT6ZBgLBS7K/mLxgGUjDcQsIG6do9aAmJyLLeIGDpfbKMZQb6QY8Qooif63xDyUKVnYXBXv5ObM0Zr" +
        "Fg1b8khujPnIMPkcw2zCbhc01AIhFAjdbTPY4YhwrZYw9Yin6t+PU7+2xXDqK7QQI+Nxim0lVXXeoI8uZytdpf4Iz9S7mwDjxtGE" +
        "3Kk0NB8Czn/zzsI7SNeCHRjc58lFj6jFWCJKKL8TLfWXPGm7ODYzj91f0IaCVqdZ2J9qoiXCNN/qYdUO7VjB+98XY3+YiEBO1Pb/" +
        "YgCMZMBdop5f48ptPFt7HgJsJm9sDx9jXcm/CneW59ivGRYTatQgWhCCMfVFLvHdUupHJ7YXwTaj84B7TY0pMze3kFm4pgxNNZD3" +
        "TgrXTSccHcPh0HVBtbNn8SK3hPZvGT3Ufjhq3RcZ2MF/NI9pDvkR/QlL0rp3lgMAAAUAAAAAAAAAEgAykQcwCiAZ0KFGjcAhRCoJ" +
        "AAcNwDxfVQBnbGSiWMpf/18QAI/4qQAJ/4n8A+vbO/P8A/vbwqPb7BPz07Ib2+vz69vMJBOEU4vkVCPrdJuTk9uTW6181COkLI3L" +
        "/JQr2+PD+9wbxBwMM+vSusSkXSTL7DVWM/JAYEAgMCAQzdfe+1cPbmexRnJB4D7NNv30inKEob2S3iP9exLbgxI9Y5yEHDCbXiDh" +
        "W+pT5gkqEekH1E2QKmCBV0zhCbAn2JwcYHXzTyOSOKUKV8WXRCGtvRlX27wuDI1lAdgjKAwDMh7OYE3FtNQb1NZaQZ9tL/jNSqdJ" +
        "ing88FTkxiVwOk42HqdF9lLVf5wUh/mK7rrQoifsE6bJ/RhhegR6X6jFeMoqhjqtKFlBPxZ8KquH3K4WahLQQX0LrHP62Tup+R8g" +
        "r++jb5zbWMoWYgp8xJzsbgk7T9JBc9gFsevdkMg7pJLE0DoFQIM7CYwlyBm2Gc8zEIMZ+hfRjJgN/o2wEFkvxmkoqwlrehR5V9Kt" +
        "srpVjKEmp0YVvyg1S7ukWqqpenW/kSury8+ferpTk7elrclDnKUXrE8WobZF51QsN2s4H+TER8ukKGpkP/RQrDHF8JmL+SfS1ysU" +
        "djhKwQpamX25ZgiCo1xnoxAd/VprjKsDll2yqEvlLBjcoaAPoN9xbBvcF7a9enqnhPTsNHoJpCO0y03IQnubAq2JHY3EWgzxVC6h" +
        "D050x83DycuS7U/OwXUkNiN0kpu9KjW7LOloBIYynXiC45mVwdDki/fkt0vsW1kOSLoDQnulGdIZRv7mmtrOnGT+XfbLqtgDtrDO" +
        "GYjHu4Ucx3o6P61IQSP6JxH7FN8Zwg2rgOe4g162TW0XopYdwGoOIv5cQS6XI8oWytWdA+v6FdNJD9IjdR7hJu4zVGQSYJzpkzp7" +
        "mXNztYN1IOt63sqpTRnAmZEZOtLM3vmJ3p9DLwOE38L8w+nADcajOwOGLtwZFPTu8JB7n4p6mFjgPx31li7rAkWzmnxICnQPH8A2" +
        "km7UjRFz9NJJGgk0yCZKFkJjrJSIGuIP9xA+lUQDoNsyG9UkiGmlVhdnxLwvvvn8dwRzoJXTBcvoSnHSM6PW/2bYT3OaH1xuXstp" +
        "R134q/Ynm4XLh29stwMcyd3I/2UewvX8V8wcwZuGkj+pLfA5n6ibpfXAzvCKNzT/9O3pendEaWosMqfZHrxQWTGDxzEkMrbNma1g" +
        "BQAAAAYAAAAAAAAAEgAaAah5DgAABwAAAAAAAAASADL/ESgJggXwdL0ZwEI3VBIgCBoB/WSgA1tjVKLn+piABX/EiABX/EvfXt3c" +
        "Xl8fXp3cGR1enx5dW1TdHl8e3dxhYNxh3F+jIV8bZZycnd0aneum4J2g5K5gI6Df4NwfYN7fIiBg4F/WmiHiqGZfHqlyn9IDAgEB" +
        "gQCA1yPBNMioAG05uwaQal/1YHuyyu+ReM8KH5gv/HaxhomilkM0wdlLfsB8hlqlrowkq/0Pc5Hz8mC8jTxhM7mDdz0mUtuv/hNu" +
        "jv6nZCu4jO9IDTGCrUoWbbs/Gfd9Pbde0P+AZGuU/E2WPJjW1SiCNzLCHPXYDrgNIx7uc6gMjsSvdQb5D15b/7awupz0Id9oy2Tw" +
        "T4oN36JT3JUXN1icoXprXEmL4JgEdpkTC+C40WcOV9ptNUfD7D/WikKdevLaXD/T0eW8UH0y9UxBvk7X5ErrzybiwnGNdMUacoD7" +
        "ms7ZTyPIKnmTCp0eGUzUj0WtKuUzy/xB17BrdeOfXFEv6LtMXDh6SpYxIN0Yq4mD9axXWVxbg0ZapdUKHVZITBBkRB0BWCmQQgtI" +
        "pSZ3f+fV6YTEIDCDxlzg2Jy8FaQyBJBCce/joP+Veh5Ss1QK8AWXQC/aWrQpHCbichkxNviwmI48/0F7xMtmaSwiErgd4sXrRFet" +
        "tDs8XEPpribaXX2C8eSKe/cLEhUfBNcOnLV5EPa5UYUEXPxtJ91MniK8rFkJZFHf+uPU3grSIi/7oCaJ/I7L/rZ3NctMoJzjd0ys" +
        "HOFHgF1HSX7j75HtjfrkkGvF8+UC9s3RuBIu8jPpHcsSbE4UpsLnrSxxSfyxee8R7bOaM/G+kRuElSP5B6VUlq0U+KDzQZczQind" +
        "BxPLweGSRWV0hJowo6zDMopfVn5Y2tYhkB63MqR0t/KTTMTd37iejsP3nASnYTnjuabLviL1zXbJ9WXDAa9nIPAgGaFukIhyz6ko" +
        "SnDi+FCFdrgAJFCSon1i48NIeQQc+HTK4I6L2WvDFz/mMXPDqh7LaS17FbRkNRyuNWYkpV+Qt3+cE8L06ak2dQYV5VXiaFSLMBg7" +
        "VnJWK/4weRHNSDSP8Qh0TJGldzPwKQ4AOIhm4nOcOlCFyLmpxcnlumhJBGVb0lpMFp80aS+0CJhxFOuKF3OCezTAhe3vx6B59zwo" +
        "bVbfmxRsUGzRpxr/X185vNGmgWmT/H9A7EwWwe4wmpNdJ5lEF4vOzW74WOmABRb1Q7wcqT9tMHTgR7ry7VvYQ93z599ojIeL6rhr" +
        "iSleF3QaYwff8WUPmyXSkC3PwEsMiKWTBSyxCAQGm9vCsPZsJEm09YLKu7e/dHvQz2+245KvI/Yb2FtU8MF7JhdIJyQC1HyPP5z6" +
        "GJSZIJzq9k8CniQiRblPVc24gNNww+GzzztnVjSGfyxlA6/zrztNQSM0qxurwFDl/PG/VNrzpM/68RIQPtbkUrnYnx6qwJ9HrZFo" +
        "ILHoVGkZ0iDyQGTnIOyTAllk0bk0V8TyCG/4N61p3obE9wEH6PY4URNhZMVAh70thljefL43N5IucDG0EgFrPDwrroflHDzzDvuv" +
        "2KQ8FhcofATKTlD9F6+h7600iSwRm3RnMoyvuepVgF5YUqTOO6T037v2ZIGLYm+AcH8+s2F6L4hxA0PDEy7yiIsBeucut7qMeTFC" +
        "j+uBc33eVcTQJfRA5zU99wr8gz62IVfgIK9LX+B38UdQ8vIbA6AmUAPplv7BqkzTM4N4P4mi/5bQmvodzpSQH6CywGBJlZVj+mpb" +
        "RH6nQGhO0h/mpf+3GydtAeD+nml8UJpU4EK07w1jLXGYZuyLBqsWiHbiU11rAIpLoQYI1rFNKxnrlN0uk4zDorUo3UsGP0pAzF4v" +
        "2ggojOokZy7tCkt2qT/7L3C9tqY/XGPpkPY95AKquReNVJ25uP06MeN1HpUIVNtJDeYzbYeGTzQYQx7EbyDd4ocBIDgZywlfmfHR" +
        "ePakqTNa6VWfrT9sUS/o3SA1ND7BRUVspVSqdnTFS23tCD6AVrV+G4zRbeUKWu3gGzkTINVt97cpultS3TecXL7pe5X8AsAdvQaC" +
        "D3zO7in7KPn5EXP4MCrb3UW2WMxhwKvOIOWMliz6W6703oXnRLY9dWocieWnOXaj/tXpLBlwVxnETX6DiCLB1zc7L2hdBGiS1Kzy" +
        "O4Nzmh9TJdiTUXptYCyDHKyy0n0pDLjwhguq5v6fSprG5gYZnXIhYeampv915b/5hXM9ru9NDuSaC3ZUAfDjcE4OiYc0m0lOl/Bj" +
        "tPfHp0/4UGyarmncSBil2l2RWHRY4B7aFznvQQnjdmyh5Mt10omiwwRwTt15oub3zJbK2rpIWLQNGqCYiPkjK9/Dg97LhyCNtiM4" +
        "lrUGnR6RNd4XKXccx0WdDUfhVOPp+VWmF9s3q4hZNB+04yqIJwjNP4QI5odQe6Jn/V/2ApjQOyTAlqHsL7/i76wYwo2MRO8UnAof" +
        "8eXctzXyl4Dki5YenLVNCQ05QZ4KbLjVBZq27LymtvE39QoHYgtc9VzVsejLpUILNUmbumPSkrCirq+zuu6saekaDBkoyx4Qbtg9" +
        "J2n0z3gEluNTe7eMl4oSde1myQ3pwcDxhZdbeIUDbef1Rg1ryGr4fS4/1iroBncVUqbPE/Y7RDLMDIYW3p7mbwr+IeuIGQ26stW+" +
        "DCjiYhEaw1487IGxistqxTjuSxKt2aX7jC37UtOulVYA932CjpFlhojkMkSdvBpwg2o5vePbQRzwFKWOyFJboFnd1q6jzi86rehx" +
        "odmHLSII1ldvTMeSLAHNNN9tSoVaaJcgUna4oosukquMnp66NZ55hiWj0N0zIsB84xajlhuZ7jpCrDYDGtUGR/0LpQc/ybSisgmQ" +
        "Swv0gz47Zjzt1O97EGBLfhzDxqjVphaUn6WEd/LZ93lSbgiinVgGtlGc45DFnxBq/WyPfJP4GoCMfe4omEVzkXJ738CfVkwAeYf+" +
        "Thpk1aP4IrzQocWlLHvJ2IdAot5766Zpb/Ruh7Nh8YspzfJboOZaSMV9xw3QJTwjFTjAcI9oxLXMdMl6IhblRvNtW3iJalxR53VO" +
        "n9yGQRrpcil4RCJmMvIKMA5IC+EZejaAxG8oJAAQNAIvnYAF82X2xYh06UTaJLylX/VRAAl/iZAAn/ifv729Nz0+vr68Oq+7vb49" +
        "u7mlu72+Pby6v8M5xbhBxUU+tke4t746NLpZTcE6QcjdwMVEPsC9PL9AwL7AvsK/rDBExc/PvD/U5r6kBgQCAwIBAN4bKDPDxGSI" +
        "59ALFJwZBJMEdPoYgFm9IrLgYwg4fEcef1PLKEZtLDNy2TyUFIgMyoYNPlHnuDP21NFWXoPk+hw94CKw8QmdDre7+Iz4yuVz7Fdo" +
        "llxzB8Y8ethld4Y2rlTLueoP+nmb/yFastq0w8V6Ry5ZSRPSflrGHat+TnXxHUesQ+qJ588IvDOY9ijJFg8gwTlpvPmQG8tPy5zN" +
        "slw/8jdVp5snOH9qiO6YDjFsC4JTozERYLaf2OjZa7pTo30oTVUvwPPGe9XFlOj9SuY/DHNgvYaEl1VvKOB8jS6Mshn0O97GYelf" +
        "BW78LRDK7e1XnhCqUyl9DEMTU/AxORZleR+ghAWaRlDEPUNZch6IHHwU8dAyqQmIkcV1akXGeIxs2rWDsWDa9/YsG9fRvQF1YhuZ" +
        "2oDs7YEs2q+EdBSH7LR2gK1HGNoYLR6j1BlEqNZloZxtoDESnzXQdD8wehwADLC9Qlbs1uaKxSBTmU8dWSXiGHa9zaoM+i8ynPPR" +
        "zGV8h7mU2bKEka7iXY2Ubwnt/0ERTgTWBlfKRV7dLvV6m4Wo0koaikQ/DvbLxCTWrvTqF/EA+ziAX6lIUvuRUH5Oj51yHfNmMPWh" +
        "byYL+gQVtIDUaHn3MMmbDpofCA4fqwNdYo7kJdvwnqxUgD6QuzZxuimKl6EdriU+/vjteItnqPL+W83/DelMtvj/GnEZwQDu/dya" +
        "b9ABpJUKjdsNS8lZYuD18tM6w5Tw9xHlGgQ0/2R17nvx5ip2mzKstCKT7HYLNyhos8f7zk5AxxwnhYPeSm293WL61ESkqmodtVge" +
        "8hmgSRzhbtW42KGGdDvSBBoCXpsD5z/r/KGMBoyM3rkB2vC2Qo7RZAky2hzdXNGM+430TAjqQbETxXq0Y43Jr5zdFMhAQuxGIvvV" +
        "9yzpaPyZXiTmsrifjGEAmPZ33pMhum7d0cu0Nvm8CFUweoxdnM+Oe20jKB0RpKtQt36asONFbWUGJIGEWiY2WrNnDMKrIkvU/92D" +
        "YBRU33/600vLN7QwvstymPFt5M6ZQXAOWzgZaFusrsJ17iBXnmgZbgnqH1ZV8t83sM81RPQaGI8sf+d/apGkGrLkP2sZ+t1y9Q7D" +
        "SqMjA4oAkk46e6IokYsxLoX0VEgJEHJCqBhLDZoV6cX4APPTCRe8pRf/DYpLlJ20qXc9105BH+WXcc+gmXXXlFE/7iGzoT3elwY0" +
        "1IMzu1KVLdJMGsng6KiA6BLLUKAoCtQ86eE/s7SbghEBwdO0joNiEvuFqSAzcvGxPJuv0O/sUlCyZtU8GEELvZv+cVFCm1o31ou1" +
        "kLICZTFTOBB7mPdf0fgUnY+tbsJ5T3ZpQGmUiHzRuEDFfPxeiiV6aspfLH86tbhF+kGVulVi05do4DbInEf4r81KigetcvGx4ymo" +
        "LB8d28b6S5yLqaFVlTWaJr4gsNWuaHTu9nIeUVgz+48vWeeF4zwfF3yIvkwjsP5pwyGvIZWmYoo3InlQ2BNqoTV8XT8kfI7OtqoE" +
        "AuhcUhnf1GMgojryoq0cRK6gUWQefHjff5QvO878w4c28bRe+8GkyqcpkqjO94MJneEt6+sEle8T+qFE4WYDgyS26/OwatOICcCJ" +
        "1fTBMR8b4kew/cBZYe/dvzmQGZE0DVrgx7jv7KhvHVE3FVS47OpNsEBWBPCr6OKbjWNDqTQOnwsiIcwzAwAACAAAAAAAAAASADKu" +
        "BjAQAhVxMUaOQCUcSgkABw/AJkcUAF9sXKJN/1MQAJ/4mQAJ/4n78+PDc9Pr8+PLmvu72+vbu4pTs9vr48ukBCuUW5QMVEPzZIuL" +
        "e+ujS62E1COcHIXj/Fwr++Pb6/wUDAQr3APysxQ8XQUD1AU+bAJAYEAgMCAQyy4I3y99HZjBzdovTKNiJiLDyAuSyBLINar0MgMC" +
        "FyX8lY6zaAyS6yLkEUiEXpvZrfqKJvoMPEHBcUw67wOlMJg4cNP5HOPx4Z9JfyeT9TEb+DSdXiRSQIGKF4yJAAFl+ldVLlE7CGyu" +
        "V3Bt29s6phKw2e5GgjaDcTDvitZqUbXO8Z4o54iNtLKw8JiinuHLHjbkJ+VFrCYWCV2UW0sL8adLpuK9HW6UHE6tYJUunFicZnDi" +
        "3D+MxmnSkqCS/jf9NHGnMjUYNaa2QHE0r6TzdYASVKHSdgdRvXFfHnwgPAlR1VxkCXllnfr2FvQKLVi2lkEFMgxiTmmb/kR8Pfro" +
        "iD4WPxK7WsHrm6Nr1atjecJ7p9ir639yy5ZSvDanC6gkd5ZRM9dZWVCYLy5Vi97c9HEgLNyArlKKKHD9Z1Y/ob5mgpvk6k7JNIr2" +
        "LrThfIcyoQgGY7r4UpOpLmTX/TP0p2CRITJFaAkuJXSfAEpNhp/hXtWH6XYh5wDOdgKYFbgzcSwQk04xNfxD0Laa2+Nem1cB/y/m" +
        "E/7P2hgfkokJZbSLfaqdXLxLxu7K8KXOAiYjkqg78urso5tXmiWFqDSaVulDscnetLUmIcig5tE2V0qJnShXp8OK0hkmowX4uJ7Q" +
        "ZSyH4ISEL3qV622tgTtrf2N4kpUxf2irzLdeqUesDJCURfhrn9c9Gm5uQTdjqwAtvEpUeLZwGTOqMBk2trCN9gE1PKOz/2xtx1nQ" +
        "BDJedCakT4cNuPrpex9in8KdBHiyKC4RPPcJfqnV73Zhw1PzsfWnXAR1y3g2sVGZq+vfbSecskSjbE+/SrbkTELfPEdNIvbtFN7b" +
        "IC6eo6o4+lsP0v/WrM3H99ThY6Be8I6hWfNPvEIP44o5FWl39Hjc1RQ1u/0R8D4c1s0q++8S4pAx1tFx6t2To0AFAAAACQAAAAAA" +
        "AAASABoByPoEAAAKAAAAAAAAABIAMvUJMBRQEdC5ejaAlXEyJAAgHAKZRXAFo2WmxSh0SURqI+/z8QAK/4iQAK/4j78+PDk+P7++" +
        "vLqzPD6/vjw5qjs+Pz69OsBDOMU5PsXEvjfIuLm8ujO5Wk1Au0HJXsDExUE9Oz9AQDnCx7zBwa6xxsXPzbu+0eLAJAYEAgMCAQDH" +
        "nmv1AXsU7oUglbXamIi8SJbJ58rUng0NB6JSeTNyNL5AGDB0h/jTozn/4Vnv50ZQEfJD585kc7w6V1IRxx94FZpxLxc1jlXHS4oY" +
        "Lig1zeU1Ju28jbRO9UnEG7IodoD+KPwiPjJubWjjUCw3BbD+jT1+QTCR9s9+XYwdibjUDP6Gko4LQA2XmvBdXe1YvaSzjT3bcF7J" +
        "SbH1zJx2dQ7cu3BqV5sI1V/y+twc9WteeRz6y6iKlbOvmB1w9RbDQlT9eeTJlFljkGnAlfkjhxReluH1ohgVQF4EB/llNcRw1LHl" +
        "u9EI3r2A9ZkdMfUNym607Z1MicNjq3CcmmdBBDVeh2x3o2i4QrkYi08AjOIXgl5FuVeMz//lpen01H8TCx18WYkh1sy8nl6TXDhz" +
        "0ryAOIHMxL/CD1R/L8NPPop7wgZz1tqn82t7HftroR9R8c9qqHDwTuKmqaPcFqrCzUWNnjLldBQNK/vWuICGBJ15mgrNTQOJVu8z" +
        "ctKxKKltXjrc6u8INx3VNt9JgkAs7g/ysGNAd62RXg0brKCHXAslx0Ua/DN8d89RLW5WpqzQdhGf/a7OhpLhoFaYTJjSpwpqoOZz" +
        "GihG4m5TsX3PyWYy8dH3Gy56nJCxOD9L1eETtN4o0+uBGbMvstrusq98eC9+VRR4ZifkYzMw69n6gqIZuqdw90RBpU19xxV1FZpP" +
        "WcmVycpPtEXCHRsUJ6xneTwMnpJ+nYuMzS7ACGlOtsyL4qu/oDtqPCmWghQHXA1DvD9fQS+r8QTHVb3dSsQEARspBznIAoo8rKAU" +
        "stnRMZVr5JfaeT8zPR4CIDCyDEUG4wgD4LJ+OGHa5avtM8+KBi317CHEI4FZ9B8A5OzLQXLOO5K2w4pKS93ToAYwdSGF3b87qUp1" +
        "uvXALgCsylIq6TwhUVjG/rdPX3omZltEKZRVu9YjzvBvuu5xSSqpG5Zz/wCyPcP2/+Jy0pkKiZUHbovecPbZVrb4NFTnCeDvqhar" +
        "4rGhVM4n3EcWsu1U89pQQBFMsGnvSYq39b9Jwd7jZXc/ElJHEKsEL82J0h20L8AnMIHkGtFg4yQt/1ncFoNa7MeF5ozeGIZDoZrJ" +
        "WEovgnHxzFUdlp4QdQqe0McqkaL+wTNoiImh6wcgY/JVSRaCCY1+F59IpOnYuhktkp7c3buEHY5ZFK8diQUnySRrMVQzfbCJo4a6" +
        "avjOFOUxia9zluyMFa39Lqte47ph1B7v6tayRCBzIJ0fOj6QRhTWPW9dscExJadaBB4NUpx7Q8ui06n2bGWdlmlgf9RkibEo7Ojg" +
        "Qupqywr5EWHYTW0tD7KEJKRJvQDfNuYXrZdRNrhKabs9YzmKCiptjqEBsj+D6PMoQAYBiR/F56RYXMIXpilPp01TpK98g9FdkR3v" +
        "Scy8rQHR3wRaI598Ay+6KZK+oytzcNqVB3pUD+U88rBwSnrYwlPfWk9sS3ANwfM4tjGDXsrCFWKnlSzmrMJvuoeddJ/4rWrXMIUH" +
        "Z1pt9nyyL/QrJFZSe6Y3sJzRRwMAAAsAAAAAAAAAEgAywgYwFiAaMVFGjgAtNEmJAAQPALOGXAGc2Z2xXh16UZqJZ/1gQAKf4iQA" +
        "K/4n7+/vjw+Pz89vLq2u72+PLs4Lbo8vj07uT7DukS5wMRFvjVJuDg+ujQ52M5AukFKXb/Ewz6/vb4/vj5FP7+9was0xcFRTcBAT" +
        "+o/pAYEAgMCAQMblsJsZ+isnzpA0q6xPws+YlJTy8OmKxdu79Up0QBF4zgUHi1/pIYv58xjHGozgtbCL6bWf6WJvkQflNLpY771K" +
        "HDrUW824EEQGrUx0HIYfFnho5FzWGATqF6lnaJ+WTmtAbui8lBLUKi+CSUTbq6IKnxsy5AK1qp7f36V/R+V8qS9j/2Zgx/AU/4ro" +
        "wWGIpw2anUADAnmsHPPJoINYy/KJMKMHnwUHQm2mGsj2fhyuuMc4Tj+iU+Uu9HvmemS49rsC8belXpERDONFWQu36MxnXocIUIHi" +
        "3oiepPdMp+4SuQ+sVtNGyiNlpPNss4bK2mYq3i4Ip3GDlYmEcTsxEZzaYAhtv0OhmzTYyDMvl5XyRmrvm/2A5ZTSUbzhTN7V/6qY" +
        "Tt2h41IW/fQLPKfMJSUaHJd9JAF9h5/9u+bKonG2/iGkCK85+c+hTMe81wi8p7N37ZYWJMcJhQoIWxwzb7UKYvtPO5lsjWD+yiHH" +
        "Pvwt37mSzmdIhyQmyRwgxTO78JIHeK3tFd0jd0D2TcqDthsi8htyhZHkDg2dHRjMRXsZXCVhEe3k6B+h8q6si1lqsubKXGkylCLI" +
        "X0+BK5VCCFZC9V3bj0Ku5Uw/bSlWW3lLz95ExDQhHXn3T/TQBr7GYkzXH6mhmkfEGWrDUGU313NUHqYyfmvaezG1vfg1C5a+ZBN6" +
        "41DBoo4prGp4cgChVAj4fGtKK1HhnfFRwsWKnQOjGLfeBXaDDEagMFaa/JmOIVMJHPaIwzpomKv+CqYnbVM88C/TGsSv+xxpLraR" +
        "99ZnPz+DAPSeRu28O2K+o0O9DW33MAOCMnxV2XY7Ps682VYHYIS9JlUd9CIzRCr8LhD10YbT7GL2hFzkx6an/OOoO616WmOZyJe6" +
        "QPSFW5QGdZf4Mr6IwBVhGl1GFadfSrSiywHNZACaeOGOgT9jV9WTj233vsAFAAAADAAAAAAAAAASABoBmMYwAAANAAAAAAAAABIA" +
        "MukiKBhgj6EOvQvBhhbSYgACKlAMC7XAHI2cmxhh12UXK9r/2sQAKf4iQAKf4j7+7uLw/P8A9uym4vT6/PTqysLo9Pj27N0FCOMY" +
        "4PsXDP7TKODm+OzO8Vs5BOcBKXUDBxcE+N7xFvD1DRUBAv6qzRETOzz9CU+ZAJAYEAgMCATe9CxiPJUDulUe34SGHCMvVGDEWst7" +
        "WAnFjQjahvT7Cm4gr90E8aCoeFoO1w6D++2htHGdMp4C/KFZPeDcr8OjjdlYLQ5xZgiq1pXNyoUfID3i5pw5Iq3qgOa9+l4flJH8" +
        "YCHjpUfEB6d1UVIzY78vbZk/G7pJB9C/3t0UZdKTi7SPy+1SB9DaBvC2qFXKKjMJjUTPIfbshQ9stW5pR+tTPc+UREadF34Zqzpq" +
        "LKhbNoice4OYvT8gJsDDRNCasC60ARsNiQb4mhTKPNfx9XpxL/Qgaqy3SkHWhlGCIDgcEtrZYWvlFJJ92l0WrkCaWFqq9De4KR1o" +
        "vnYOcO5M/E0ai54Gza5oNY/UKivfPSo89adSPo8vEqpEGYvXC6T/y3WqOfMrL/BwV73aRDSfPGm8g0rCQMF9CA9/4nT09afrYxTu" +
        "v9l6Yz/99MUgS7fxJgibNybZEIeRmlnIT17nfIOoD3kQtqp1rXv8n68e0CsdM6Odq1PA4cN+anTsunCvghfUVhDpNB/rNjDnIO1v" +
        "k/s0Da4Os7oJ6JtJFim588nQAXHlglepX2KN2eywAJg2e+7JHH/eZ7beFaSu5MBmYPxBSNWk4Or0gWyj01ugP9oV3OF3oyHkL891" +
        "nl1ZaxC3mOQKbZV4Ztex16Obj0kYvogvUjBxUMAeW9GMD0cMogjtE6intJgveBL6J66cLGI7LN5afgVeNywj6qUnjDBLLODSUYXp" +
        "ng/MhQtdknuk+uQDMI23XbAHxlZ2GxwtvO7xYeU0Zu4uH9k/u0oMG5s2JrLacuTDe8Gt3HJRGLx1hk4/K4X8aK14bF/36iEIjADF" +
        "5ZeEI3ExCXhXxnfD/89pe6X8eVKTMTvjfVAQUYTe4wF4BMU+GcNCdHPEq0ke+2oRROXgYVijx9W41q92lg3wphC8lSXdjpZWbkID" +
        "Pu3BWVyeQZ75x1z94HdRk9oBAaxLUbLPHKoIuILFJY0np/QFZ2IQVjVihkGenIamwLSQ/xsxc1eZpLG3tYc2WYGBpGgkuLICSutm" +
        "KWOGP0d/R7s8e+FdM7cgboH/5bt5T3jcHmX+udqzub1BlFK78Hfcx7uaQYpHKF8tbfM5xPVDhelkepok3kTK1hc/c3IQOR5Yz/3E" +
        "mZmoX0kloXqV4Vw8nXUb4ZDk4fCRDoGXDmJEnRlnWUfX4WUADKyWU0bmHBYu1IaUyaI7z7RmwcAZVdhEDV40vhy4il3NZx9ZUc05" +
        "CCurH2sfeAREaerkBEW7x4LOzd3ZLuGbKMyf3nkb+szvPR51DCA5yb8lX+btlsNHxANTBzlxLXhEZVdV1RDjGhDAyxNjj3aW64/E" +
        "zSRKdzRmXxaFaAurXrUUKAPQ4xlDhgj32gkuyVyskARMs6JCvFPHAWEezUuNRxkPI+EfvyH3vOLF9kjNwj2gtSstVRJNAJII5DTp" +
        "vNdJcJfW3PHTBEUeNvE3PABfHnImGO4iZxCI/GHfG/a0X8JlvRJHuFDJVX4np5xFY9uvMLrWdYr7ommuBjJQEciQIvA0Pfdqqr0w" +
        "pklZQmgA3WLt2hMUTJxnWbKdKWmRETduYolno0Rf6en1kWNngnHe+cIRS/KfTvVQw8SNBeQKLD930Cf1TKSOgRHXkO1fjmL4+ZwA" +
        "IvGR7XIOvLfAuPe5owY2ujcTiWg9SAEtMYlF7T7JjiKQ18R0AUZBAs924fJ8wH+jIf67My9eS/0TfHTus+slqwPipImziR8g59+L" +
        "iwojikFizpJyVRlwWdOKTou+yc8RMNuBFu4r68Wqw1R9NlI2fr/2C4UPftdTff4Z+HdTC/uKGWiDKUWU75wKKUxab+dlzX8cU5+t" +
        "7R2D6XpgOSftDHhsZhic7UGwzHQoIi+TJIdo65FkzvZGX3FZKNSIUc+QFRk2EkbFr/WEok3eqR5qwX6LQ1rV+DTCGOhF4krx6oma" +
        "3kibXUbyf0mBvReg/COtwRi6TKC4xnP7MzmRHmKTJOUKu3UeJ5qCSo3qXlVnfjuS8KLBfqw1eFcUebkzNC05RK9rSZXkQP+f9NKa" +
        "tF5CpJKjUVJ4+SwO2HGrvm6iEzQdoSHNRHpjR5bSx9gyId+k+ovE0TTn9KPOZDw+oy7AQr3cEHhUUhcUVkCOtrBXWyPtos0Zmtcr" +
        "LwkaKS7cBFjFasI6MxIf8xef2rJylcAD5oAve3/bwl5acLSWKn0c7Wn9DXkDu++0I6QIc/R42rfOhXCfUG/G1kOpZwmZdYRleQKi" +
        "YpU5V8Tchnf2dDxNvWG7W/jeQfcwq6Z76YFc7+Iq4BM9SYjIvIKt5jWrNLHeEnlcACVrJfkkkAEQWTU2WEXjjGdu0cBfCYUOE8rd" +
        "FwdygabfDEagfMkh6HGtVHLkSKlKDl53KIcVYdcLbQMnRTSsT7XbY9XDTXCMtFojNT3k3gY4cxTXoMr1lUzI5aqCQfh41+9hplL+" +
        "6ajfA158v27g+O3Hr+EfIrqxJ541ZhW4ZPNzIdXndBdzzYWpDDlqXn37VH3XaXZUvvJUVZY+EQz9TkDl5vg1Mwv6mfvOyky3umzK" +
        "mzEyQEuAIrJR4edQE2sJLVkl/Gxie1SCuMqP/cklo3KXAhKuKprdCIkedWlhpiPZIcKrLk0+erewln59c7x/p4yrUPO1FA+AThvq" +
        "ZBETkTNvtMO4+WeJU8qf+bnIIg1mIJ5J5+AvIZYqDgRBDH7k5EOZ8Xg0MdGBuqUKaSEI7ZCA1yjfRBfi9eVZn6NUkHHJPEajrIDt" +
        "K2TbafXLQV/moeOxIIHkbZy06UCCa9TwnD+mOVC2ZIHyB6IyLFG9eMAIo2ra9EVx9K1vCR40g2tA3VGO0B44f3+PA9QG6FNV3qvL" +
        "/TXn701xw9DxyxQDZja7RzXnR6ySunBHNDDEZuiN42MvObsnoPJk3JU3BQiKRW/riTu5G4ANyPr1qgYarHF16rAzNgh07xc7LxEx" +
        "3LSvjjpIIudFo9OvmEXcDOIu2wfCffrOmgvNsHRbaLVS+WdItTfFORy5CcK9kcezv8HBSaOfcXujv6Da283EjHiOXozJE3Ll6/IX" +
        "SJ7PZ3H0S3+H9Rb2LSk4UffsnE5rlQZs1YW2FcxfMAosBSoWCTfWAM6HD3AAg8ZeGvn8JeL+5X7uLiGIgcXNX3v8jWCViHlFWaMH" +
        "TzY7BfUrSrXHzjES7lh9A19ANdI5d9PtSj1eat+UmEAuXK4Zn2Gox8U0QTRtlv4bRtfyGh8teGvv6gUoXGiFhQKCr1aCSlK1ftj9" +
        "9ewhDs6WIkS42DUKUFrTZ/eHMzNcFd5CKuMTUne7kNG20SnRuyOwnnA05S9TtJAuvWPaIsV+VNs87t88dFHM1F30teW9mXJur9n3" +
        "39PbnR8XrJNi2mkhHVFTXUmMuwZkdPwS33oT9oLfX4jwPzPaO7C3U0GPIssqGFFXkHyf5VbMPxLc15rA08XtCUQ/cbFjDdB1nei8" +
        "RlzK9DjMnqBK9AYBOHUfspSpkp+VR87aoG2rbGTdUldQF1m6zU9lo/yG2hq3QirrIFHFiODQr5f0z7YyFoh9OfRTnLpE8qc42O45" +
        "utVa9nKbxZ9p/m2tTRO7LaOC5LqHEY8Wgle6w4XqEivVhr1yfRhJ9JbQbae03lteVCc5y+awMwFgX6GWb1gqXBcz8EDGHfZmDvGQ" +
        "Mc+IleCXJISJbzuTakw6I40UfHvx+pQtA76gMeFnUX5cvV5MVdU1gJdewxGcUllU7GK8TLgyd9AdHqcv7/x02SitaB1eIxX4CdeY" +
        "PmgqYrG5DoYswxxaEo4EkVcuQLg2gpi1HC/M7n6TPu7CnowBgnGbA9bIt4onD0yoLQI74Hmoz6KmYSQIQMk4uPc4FaIPGqXsNP0R" +
        "BtZT8Mick0kjKKLyfK6E9VGhcGwryubxFR9OKhTRvYlJ0s/y9XYbTxd3vL3zOmURLdNToo1F2UYx7ig1Hf0kmqCLUotCdD1VnlSL" +
        "yp/IuM4PUqsBWzVImSKNDIop8P8v3dOkCiltjZPQT1PDbmXKZwl+Bz0r5X/ZQ0PH690/NCTZe4EcVteEHoUTD0rjGuhsZAyso/Q6" +
        "gTQxL1lsy177j8CCfENvV6yf6j6ltcXn5OnN39xbG/Y+n3hoosajgqhA65l6HzaO20riedW4YSPWI+mGAi/Nr+gsVnPELMJ489qx" +
        "4YIOmSCNJEJflOUQuJRHGz7EGij7kYEEehIGfZAP21DN7DHBduCgLGbWKRzh+oRI1CNfbKYhFLEt3S2GY7FQtMDqTKvcXjQdAszQ" +
        "WCEku5c+mtK7YgiN2SpbyXYIjV0GayLnrB7oOpJx3C1355l0OENQRaMZPeWAqiLveuzmYB2tKK8dvbD67W/Ztnho6RT2GneGp3tD" +
        "m14OnYKeeJ0xCgBKPQog+XsuNfK8ZzPcssiq63uC+d5tuhQ/WTnN3xHOSgrTcGFCP0tMmaEdofQtOhd4dpc461JkIFL7IPlAbtsq" +
        "eMHZfVKwcwg5NaC1jZK68R4ibxM9/hyAaJwKudbNWduYGOjBllhcJkEYl2DiToId/dQNCwtnc2JPoZpDEiITgEAgbAzbF2S/uBXM" +
        "hIjiOr92hsQkFgrZigdWIXwR3qeAbz/c0i1kG8IRx8Yr+0HsR1YkOx/ZCBnVDk/11svZht9JHihwD5eQs4rzwecxS5P/NXumnRv9" +
        "IeJOxJas4YIMZsPiN2vESw3ELQ4+f43eg7K6DKhJdDzakKRZB63d7C2COcUYkukmsbJQrry9gGLnijf1utJ5NBdpczCqcvwqGTel" +
        "QhcUbVB9GYv5bBWpkIiuKtX6ZeTxuTSPmxqRhk6ILpi1UYXq6AbnOSU7Lnm8NtcMYSmrmzA/uMSkLwWLDPKHz4nYG5IB5mp5fp56" +
        "pqzHWO7y2Onz1RQT+Qjrrt4t0F6lrclPKhcc6dkpelhDYQIaGf92PmNu5T0ZAeZk9UcJzGbz2D9ByVotHEgB+//xF1W7VxdUVk6A" +
        "S5RsKz1GYnB9+l8yMnrLBQD4FiR8CGZU3CkiQVG80fPmZ3sosCEAq2z+dvSALVy4XJskU59im8p55XFGGUVunluqLTZDridR5TB1" +
        "c4fKfG99ZHX6Ms1Lr9zde34gcvYgP9ETo7BFCwoBmVcYRG07kp6wm5M9wiABzlL+Rus9kChEYUJ9m7fpzd8xfFTgMORWOAQw8pXF" +
        "oiqPL5QULrj43Ji3UK0fGX+xRFfCufrgy+zJWak0p0ImmZHoq3ZnF8FwDOkKP4Ow+oDpWakljxikl44yLunPzHkGP03QPDf0aiou" +
        "TpZnHo1jyyMVGZLaEVM/KSIQPWxudPW17C3/I/XgpUaxvFSZU5TXiI9+T8SIYOt2kknCiXqxXqNBLf41sPml1Uh/QvtjsVamaWWn" +
        "d47EAqEfuyL5C2ZPUyhNv8tq8jl1P6GEs41gvqrkriuBOazEe8SPQILRJOuKTOD6HsYnmP1HRvwPiyi2qkzZ6zD9WEgJpiSqzEhc" +
        "BBi9RSbwRgh5z3ZN1VeFU2RjGjEMe+PlIEx9kXAQkp3lTOQ+TMxWQy6wIqddjK+2gZ8FFtXx6sGdxo271CEN2SH8TX6sLl9Ohp5l" +
        "IUJ+gCTQQQLWHGvEGGyxHS/Gf+kYxjBRmcpuu4/zCrDpGGNgE7mFs/uezu0YP7F1nIuw5OwGPKMDV5++htzNvQ+kWOHx/lVSPn5A" +
        "NpE7YaCoSSV2LSg9zZeoOeaLGoDIHO9rfmeS/1rlbJWakBI0t5+yaLCW3ZjDbvKUaHzAlnqSY/4v5zg2e9b5G9ODV67YGLSLozDk" +
        "3LKEPjGjhVK13FAwgA809EDs5JwYAar3NuLmLeuKJuEsD4AyqhooEkQPoQ09EQGIJtJiQAEDQDN8EwBUh1f/YxAAp/iRAAp/iXvj" +
        "2uPL4/P727szg8Pb69OrcwOr0+Pbs4wEK4RTk/xcU+tsi5OD05tDpZzMG6wkhePkfGPTy7wTy/P0HCwT3AKq9CSVHOPURTZUAkBg" +
        "QCAwIBDVJu9OFlfMNOiAol6Z4qSi/ofwOW9uwrRoh+F8wRQYpO7S1s+yjnnwB7YRXnq8XeWQh0b1PSfGEklMFbJbUQEWT89qYq68" +
        "hMEKnpAhdET45wB567OLNbtKHJB3JHoAHGFFaNbwOzWr07opU3p+X5atm8vyrTFu5otHI3ZtpFE0fxTMutzSsLgfLOoqnRckqKK2" +
        "UhBn4/pKC69XSvh+KptpWUFvFNvdCbw/rQYp9HX/Bmm6Ob2r6Sp9fenZb7miR6w7ISVN4tr/A8k/h0aX1floYZtti33ZDmRXASMQ" +
        "4RkEiISw78lrIwBxXlZ9L11eRXZB4jMoQo/TOkTKbZqUIsft727UWaNQpbVq7YkVczda1lXy1+e/k0CKdbpDC+MDZS8B8g2TXA1h" +
        "Gz5gqA+T+mEbdOJakS5YTIicBtdDyO5hhPXTCY+20qlVA2P7iftNw6+LeOXagaMPp7owHIlvIdqYN/dT0xElCEydxBRENIjRWFPv" +
        "P/T7hyhQN5vnSs8or3Dz1i5aeSfrPOhzJf73YIflSqyPP5XP5ymyo78geuRyBG6CSQ9GfK0qh95i1U3JfjY6YAeuXzUgRYaIrVlN" +
        "d3eDwN6y6yUSy4zgVFz7m4kvZlDKlCowqoK5jskK6KDVKK9LkpgBBVz7fLQqjRauC9EBE4QdvRp6bGhH5IyzZkStcHiVZp/jS4TL" +
        "u/p3OHryMb6m67jo80wRztHUaoaC7wtirq860B+UMVZEFgtzCsRHsyK7Wf1FqU+k8P5Aq5mbzViB5pJ+sIkmxGrnE0a8J1Is5snP" +
        "04O5FmjKAGbaOdIvGPd+Abmtsrv+iOvRwYoQpwczM4JzPRsgzlokrIGjTkF01VlzMEqn14xmFnNPQUvKMJ3ygQRnuPvdczmvz/2Z" +
        "wxTKdgJUFsEr4SExCtM68eTXjxBwPzUAJJKebOQ8bQCWAMLfdKjjb/zSsHcKMmh/ensR63xGtdP5NlFETUYyW9N3XvKWoc49MUXr" +
        "S53GEp/gkfe5A0ABZU0BXqdjyz2tjHtPBVX6dpd1QfcC+CfZjVkEs3O54Jxl3bVxax5hhVsjkhsUkuiQ8hOGcpD3dtmhHNcQFX/d" +
        "Mi4pLJZ+/MSpXsl6hGV7KUVuyAc7Smy9uOhIDk0KDBqgCiGwWKylEuFwRJOyG7YRHhOqBXODLmVDGY/8Q2Uihz2AJnQ4Y4BsTJFh" +
        "VE51kTrbzwY//W3fdE+oEhOvCcpireN/RGNXSepIRn5rO3wfvERbc9lJrcq5HTwJBZfI6MhUWDXyZRaBiJsp58VaK4yWOooUMosQ" +
        "y43SCXpZWn8HpMykEJdbmujl2stmAupU+9hjnroHsKIwZluSe/zHDY+mGvclTWVbudY2KTDB8BXgHkAKqnEQi3KdKFJYUokoN2DI" +
        "lqg8fWHtBRa/2LGd+w0X7UWX59u5Q+2fXyErOHVPufNNJT569M4bS18EbDbUfl5+zCokkhllDR6VLGBf4Gk0Ushhtdbcj3nj61RV" +
        "rgy9ii5GqaQ3+0yEtfY0ZOf8NsTNneg0L/5w5y6uUrtRyHMFttqJdbj3sRBx5whCVBqehSBBzBEgKrOjezKkMW6auq9wkt6X51BC" +
        "I+DEEp6wbgz+ABSnGSBpDSQh+Fjq3pCGwULyY1SRUNmi3NxkXR065m9UgC/6CGr033vHJrWPE8OW+xz/k12IbjxXQm2LlI7rTC9t" +
        "/NJNmkoJkmvSyLr/rlf69g4sHL7m8ugrBVjM72/fDHRSngaEE2pklO/QsWLZkadYAHhFoVqDsBBxGZA49pjr7qwIBDXVnU2I1Omg" +
        "UHl8iZD6yhIz/v1RWs/7rBq9ixC4qDRQqtrlEhzp6ePxxWmKfotWGnlcJqJtD3v7tNilh4pNWdh+xiqHstWE1XETow72JLZb03dR" +
        "JBcfT5fsxIe77xByhtOiMe5nWZG8l+uI1BHUksNJQSVU6CmC4pcsJwYHkDxcb0cOspZPEd7edPQMRd+NeEERHUb0t9Cod/h9PYls" +
        "+tnuzkmFmu80GTfHCbVzqkJsfHvILVsNMadWaLY2YcJ+AKhkFS/vIhGLq4cc9fa8+/3qY3SDs6HV9bq4bw9WX/mhUu7fFFer474u" +
        "vNs/12Xc4oZ/X6xugy8ICTzckD+k3SFj8UFJUi/j2qr6pxkj/LV2KEkShoU+zhXA0sEvs8lGRqMjyvAcLyxS5xjTy4ZreBjA4ya7" +
        "FJiGhbznOZ+b8NyFX9x0/VUmksPH37Q4XvGBO9oZysSqc9mLTlOMTP4cTPVJ5FUQqesRXda3stmSTawnypfXwAskMWw7d2L/zcy/" +
        "8rxx5gF3LwJiNC9ufegjQY9IFKz2iRWD8M7QOp1yAQWbx3hYJGJHtlVXJF5Ot1wZ1YRPpxLU9+FwbrETlq8mMdM7bVJ/qJqpuWKF" +
        "CHDxUcz9whnBHy7cZ5gOpu5CP4HPnhdSDduZQ2rrOf80Qm2TQyjopBuOcVkNJqyFunnh57KpyeK9mXr+xsflGMwWSLq5/aD6OBXE" +
        "WDYPCzkh36ZlYEcA1M2YS1HVi2cRG9UMFBf1qQJ1XDt48Jbs4xlkGKMMaTdWMmVVI4eC1VM8GPzu8n1XqTiDuCHlMnMK353xtek2" +
        "/VOZuwmaJdmv2dpq4xqRcsUsBM283QBRLzLElBJwUCdeql2loiy3LVQF5+a4mkB0HAgH4vBEm6wpkJCtWkznzpswAM+0dAv2MOQE" +
        "bhxAxTdIsRW9cRXCWM/eWnGxhHKNxIeRjqE+NNN5YrxY4A5Vu/doyE3/+x+EB68YDajEIQH1ojjyA1jY0y68PfDzBt9hIUxsCd4Z" +
        "YxBgkQa6VDrgW4qOHFuK1ipkJSNZ1PsUSvXmIMnT7laEigkc/TTExS2WmVxmskPY6XwXIgTh4rXEqsJIDI6j8omVxLZzgs+0PGAF" +
        "dn8zzkN9IQDSa9LRQjdjsyzShiHMjmQkngDcPa2AtozyanLkmhEiNbNBQrxzSw083oAepqhCBv1U4uHH15Z4vbZ3mw3K6/jVjQwD" +
        "hQiXbscy6pGjQrv1npPW0+jOtwUWZbqJWfWAMPh0BRmsr83NYj5zyH/LomY8kT/6l5bLiMjATYG93q3jqffmES0FGThOuKWUXBfD" +
        "Y1HV9t1ZtBAUKRISAFIIzkX1NmiZ0VV0NuSIGkYK6AuQVJvTVvM5QA0uemeATRedzdEuF0e3zqgugXw2yeW+JE4fWBGkaI8dc4v0" +
        "we2lettHuM32mQPrNpd6Mmq64ytI2Vl/0S1uJRZujuiiQdO36ewhFZTqwk5KFbrhDrd41i5PdcPpwSgJ3aGbXQcAp1KHBCBNSNti" +
        "kxEHIaQNru7+7zsMwERIiA5AkNUXRYBtN6c1ZluMzIvAhjAQlOFGXeRCFni5ckf4mNeFhynUhX1TU0Rweqo4k8KkBOgnPOK7/KXO" +
        "58ZLr/K+NV8PfTLZVxhVVmmucJbejSfNeFclHjy4NcOR8eiZyp1EeicqdYAAR0Yh3JmCZxeVrTtc4mMlovKBH3wsm3C6q6eIsgGE" +
        "i4MwYGC5GpgnTvwGbNPopiOMI7bLuCdX9jxHxpPShgVQU1oNlQIueZD+T0/MUF0xpA/Vy6PxIh6sXuhmVwnvrXWYoogMgvP/CmtW" +
        "db5eSPRmMvcGIrm1yWhgrab7eoA/ssxi6DO0FzWg2NuaW5fUjvPUiUQ/PHcegPABJIf9jAfwkIL00VXWGyuSFYB0WzpnJRVgryqN" +
        "hTPH8hDLCDYHyPZrSM+FLJoKz1ywTeZEg0Cov2cW+4W4XBKlAP7AdNVo78HN4yWOEyz9rfWilh1AlQ8VmJzJVLAq4/nG33xD78k8" +
        "Rg3Y5hyZeb8EaFvzf4tGzINlFq2+SKQUZPt4JJa0DbSZw2PH1ZntCDoAX+JLWKZQFSHpTyc0lW6bne3aKZETY1Mi0j1qYlN2aGdp" +
        "03BxmdYyn6UFxzLZntS+S6+F27Jh8eBAHl9wfgOPwVLtz+dgPRGejxn3e+hAsMY4VpMdrVrmNberI+Th4gmKPZp2GQBIMuDh0RSg" +
        "evFpd7XsLbYIVxsfuFv535e2/rzO3shbId/h0FFs6Zm08qoHK5Aj34P23/fxzgo4aqI0D9slJl4oRF+CYhepr4k8dBQ+HUG6gre4" +
        "nJcZu4Zj1oVt5M6nh6hze9evh6JT/tWHCz5Pj1FxI0P7x+NWWK5ZjYjVqiB5rzcvPo4DoyDammONTMnPsu8ll8ZrmcIec3Ya4PBs" +
        "LE5cqt2IyVkZMVlywa2R/aQYs0Tv4pSpYa81ZGQQJSSeloU1qN7CsoZ+XfSgyWhe8EeLR4Lj9c+ryftf9xFpY24zb/wrMG+6XAON" +
        "USuDLNVzb5SfIRcoudOP2UOG4/rFrrJtrtyE8FjKXCBAMvYWKA+BD6GhPROBiCbqYkABA0A2yVcAWjZabF6HZ5Ruomz/axAAl/iR" +
        "AAn/iXvz0qPL8/v748Nrc8Pb68urYwOjy+PTs3v0O5RDpBxMU9tsi4Nz66tLpZTMG5wMldvsZAP0A9vz/AxL5Cvr4/KrRDRtLMvr" +
        "/RZMGkBgQCAwIBDbSifMnUtCMFboBE9gPGkF5Ob/tgVwW/ayL7m8uK57XLRBbljeQrCgXFatwsIdpYaHk4NIpCPKLKASSAmY9fOA" +
        "BkazyLrib2WE0ngt/XDbASlGicLLHpfDfQvXwF+0p0+0UenKaVuvw4Gn8a3vCTc0hgfwAwL1vlCeG2mkqeF2k25/IMWkG7s81ZjV" +
        "yf0kCXN7bgRQRMOoNhCLZ9DrmfMK1vMzk/boTvjXzyqH4rLNpcwxunmgr71/PJf+DKS07vpKTP/NNqvJDxc/BITGOTCT7/dq1plR" +
        "opQVCdIud/k8PCV/MrgKyj5U0S9u/EGxbEz8KAN0G+26ApqGBn3lAOV+f3U+HkPfong21/SrUsrUVRq9bhtvH5rzMl6TyBa62g8p" +
        "Ptt1Gyn9KDn2DHj0/+3e9bpVmYsKNTo6vfdT3MUEDPAyWmFRulGdWc20HjN/O8wpgNL99SA2yHugzzJglpck3ceDlBqz0ZK6iXxr" +
        "lDeut5cynRF+7GCzxJBrtWQRs3GIKzc8N8iELdPXz2JgojUS6+OMSDTpNy3fovm9hXYFUqsyBLcmMypMcW8y3KeNCEJnCttbGMeb" +
        "KsOk/Rv6aJu3NoukbvizhTDKZl3QOyy/TUzCgDrT0F3LdebCFpNFMJoFeSIqhzR3ceagny+Wj5gSQhCa9dtq2eVCc4YpHvM1a1k3" +
        "bKddh5xAaFBB7M7pv40OfLzuqOJ3Er1X4tAogFGW16AnC6HLHZTvk7Zntt+Oq+hdTeGx25zeNn1OSOsAVmqRacJyHaybDozn8aKB" +
        "vpXwCAKgx85Am9CikJ5U7yrSaVr5ehWp6aJXqRTz3F8sEjYSSCxAMG3iwOx9DG6MPf0FEtlJdf1HFABGqdJa8BZ0rExqUx0FtsAV" +
        "rA87dhl7foWe2XVNsIb9p75F8AtPba+ZnqHkAVQFp+H2BARExtXdrSGueKxbm6zZgGIZ4bC58u46l0grBHmj38j9mH6hJdhopp5D" +
        "2isY8i23rr+n65vdPf8/xZAlb+w4v5i0QVr+AqgoLJ3tyQpmQI4mAB62ABMgPv7KWo+B4SJ4zWjS485R7fck7nTCrdl7fc2MwQ0b" +
        "mT2hgZuUU1km7P3l3EQ2n1Kph3p4q1F2FzPOlmeOUFMkG4dOBcOsnJdk5MvegNGF9SgGoLXOjVN/CoI5u/p38gzUaU6sdpXQZhLY" +
        "AhLrcSpr/NkCHH6XpWuu/4PJ43nEhLF1YaDoMpqYu0FanKsKq5w/uhsyJHzjGQWOMDbr8mOwl0ID8JA0rN08sPonjqRSUX/5LExv" +
        "9Wdnte5asgcAvGyanzay3JkAV1dAsOmYg9Uv+lEqb1bE4sSh9ST8DinstsDL9zTS3gRyGnB5hq6ktEFLk7+bzgS4g6Hkq45MJmxq" +
        "UK0VKfIU4u9AOi0We89S21aT9BCVln89du0BTjaMYmGrFGW5egfTFjl8AZRlWFW9s9LvJmmtL+rNg6lyIK3ZsHNpNfueu+7/dd14" +
        "pfNQSV4gOfczrLDt4MXpPcWP7CMfy2xOaE4v467J+bm5IEkLwb83eHyl6N4G5qN4Y4KJEtgnn3MD8fEB+rYMkatcIwK/hkmPUFau" +
        "avNZ/6HtqmkuCGRqsaIHji9C+zWL7DGdOd5UPAceMbx8uydVWkLRtqshBGh/T1FAz9yFhC24CMPwgGF5IBzsDRjLNKIg2UVys1A0" +
        "vwO3mnaWmN9bWUqRFisItS4+/uKZtnPw66l8SavSqgB7YfPpNNtccGGfoqCSxWX7nahuzah/6+WhcONqf7e853/iyRpcINyNuyPl" +
        "5ytASnmjdPOIZONGcMb7puNHwbMAKIbhuaDmdBawTb/U0wxwOJphWcf3KqODyoLXvA3c8KEU0vknZMQRwHb6RypZEcM2cuFXS5Hs" +
        "Kn4RBxAuXSICB/LtOXTxNckDXnR3HfFGkmr76UslJ4Rw6XU5QQlGBMxGbn+X0Y7yN/XkXq9dkbznR5Vrd0jK2QPpf3VVRc2N1ydJ" +
        "NBWq0604gou7FojIab/zlGl3SbL2Cv8Vpldnz9mQoRevZc8ld6LuK7fghQ7d0PY9rBRDOGQb1WrTr+7UNMNeeIN/AdsPg+vT0CU9" +
        "WtCGRKKOsbrHh/zhiEz0cOQRuwtqGTTM+f5FvvthN1nxeKxv75926lhbl96gdeQu96/L6H80TPIFga3puNHYjEU0dcy33qsE0CUX" +
        "ixUo8IpWv3/HHe5CktIqSYZdXR2ccXoR9WCdL2m5z1eAtu/gIrhZTTobhebB6f6yApXyEfgADNVvZVQU574VxVm56WoDRnIj7sde" +
        "uQkooPp89KpsAMALF07aPz9Rno+xKf8xIOIVGnpkPMPK9gopN+SzZRp7MCYk82IYcg5M3YSN0Oc9TD5FClOBJ4jY7+QjxMutxHvm" +
        "Yh68erW9vfX7pT8bsfKMklnjRxuESqWtl1dpBBkzVjzR27QFiO1NCCN8MdBkJ7Oj4VGYSsmYRAQuZnzJOhw0eqb9a4MR5V2azuNB" +
        "CmXfwLcHC/Pd5Z4I0gonnB0hbhHDJbMYLwa6AHP41SzoAXkjuzSZ0gYcW2ObL40KI+jqPkdoSjr4p/I4VZWwxc5FmFe4GDIS+Uiq" +
        "Pkr0lBy2F3J6+LBoFWfIqQngeK2aP6u1XikD7tED+J+S5G6ce75kcH6iEGDyIc81HpVx2NtfNOh3xduRURLl6rtfOqfyy4LJiCL8" +
        "z3QoT82tmDad7wolkUALpDjKI6UurphMnx850B7EQEXEr7hLsXIpETLaphrag0wPiq2Et8+qL2b9FCMhIAaLYLGAgtk3Cnx7bytQ" +
        "L+gvGq49lZdP3+m+whAI9lhwMBfqsoXUlhVHGvooqp3SoSYvFZTWASe2NibwFbQXdFJ3l51afHVeqlwUO1EZkR2rJwYLEvNdU7ei" +
        "YYSZIsnb6HuyShgovOgvLQnk6rMbKU8dYM7VCrY0Mkspb5iKGKbGVcciM5OgnSvkgp+umTSiNIUpJC1inMYzlKnXJMKNIRRPN7p6" +
        "Ujk5P+fEBz8ROqVx4B8OGwlzKh097VqXCsJhlSUaF6F3tUBNGJ5+xwKaF23Bm01uD7mPy3HWY2ztREAl4eDXRrSMsjrAbw61gVZR" +
        "QhYrkdXTFJZwDpUXlBGMuWbR9JChH6hAyS9RZ5mWzX3tKuMLidsTYGmC90Ifyj4ufKiJa691cQwjd9/TL3X1Wuf426pKzD+Xi5Av" +
        "bir2V6keHyDH6Tzo/gCRTHozn1l8h2unMRA4UIfEEPIeNclpwGyj7ocx3CqAuYVijIKeceZM8rvfsw4dMPoiwjUDeYwxNfHW+ZXc" +
        "D+9TKEFjBj8XdFUi0rnfRSvUuRTfzEYJO7V7W6ldDrLYtZWcQj9HbdIpbyXTUKPelfPFFzlS2fk36TZB9GPpLzne9IGfPrNrF07f" +
        "EWdeXPY336D4uWckJ33G5x7FHB3rfLNSBKeE6kID1898YXuz5SI6njFQPjZ9qjzNZ5HS3IAbh+cyGsbYFnvg4/vDY1L62nV++mg8" +
        "npMbu3udvmx8nvmkhNT0rTVHfnYkcSOssUtFcUVlM0imJZZFMNwh4J94BzRHop8v8EJqnAPsuuI+zD0p5YxTnJlKwv/LV0BEksn8" +
        "ExmyTV6h7/td+EBa6hFyEv4bICACgDzYROonZGpod4e04v3foppkvZAipH6FqDlE2azKUkGkjQMU8GoV73G2IHPPcTDa319LP8in" +
        "JSkxiXjdEmuV7UWZvYhlj/reBtqw/SyGWPZm28cJsfFwxdN0aRa7Hn8S6zVgMq8NMBoEH0LCeiwAgvAmJUAQAgNAOhaVAHM2c2xk" +
        "h2b/ZxAAj/iLgAmbCbYKygrRCleKf4p7+/ODw+vz+9u7e1Oz0+PDm1rLk8Pby6t8JBtsW4P0VCPzlJObe9OjK72EzCOsJKW8DGQs" +
        "G7vrw/wEHAv71DwCquxMhSTTzA1WXAJAYEAgMCAQ1ly9Ux7ayMErOSvOFzWJCl4XhThQHDkwY11yFQzRXFGNdhpsQGDfsqMJoS1/" +
        "ETcWDP+Lm7OL9Z6jelJGNt9TzFEgEF3dQYv9o+b33PMzXY9NLHnJTwprWnhsX/vxJdpnh9lI2Nov9PvlrI9QdcVNw1OsOrPDrHOf" +
        "YuP3MW0iCRVgAJB7cinafIObi2BBIB56GSZdvWmciJv/LkJ2yylhbD8TbCtHwvnwqFHMpbCjRX1IkrIM882YiNI++sGoi24m+mNN" +
        "Cp0IiTRgltv61iRJt7XonmRGSENWreE5U/dmCXrjCaVtsA1p/iwlfOvxhuSGEGKKKsimWTOFAokFij5ytUFfEg9m6iyfmyb5A5HN" +
        "pht2R+IRiDvF17+paiKdLPN9P/8kpwAD9wgwnx5aMAZeTDuIFGZOi186dLjoCwmfxjkAWYwWR6uXsgs5dB9zlVin6WrUs/AefKQV" +
        "okQU2/2MBsX4WuGB8Bl3S/dxQBGuUPeddL/kEDSLyFkv5nZ7bY6rCO25kZ/orsYxYz6r8o8fmP6LIPjZfck0f5sY/MfHWtyU+y6c" +
        "uQEUDcD0wqa69/spol/66eSl/6jXyTt9gOLFRjJ1d4O1GOs+pSkSNDHG4SL+v3Kfyaf27hS4dahHIYTSan7Fw7dXnEAsJUyVnL85" +
        "yts0gLOFLldTzzbHL94xAqn+CUx/hbD26yoS/6eTB8WJbZV+MZu2omKM9yhEpuqylYin5s3oScl7Nnvr8PJjOvNG7/Ih8k3NaRuH" +
        "U8bSfWS+m0GR6c3ci8c8dskcV+PFoVCcwui7ZDGovHv7pzSL91XSeO0sBg2TNNXMgpnCQqhsco3aM9i+R2/3qb36BemtvIzm4soK" +
        "DPlQFsnfpGUGkQaS5Bn4LUS3GAjeBh5lVB87dt1pWjasDBN4SUgfNWgdYjDJGQxa1dFBN8YOqhIpHdGkYS4UA2eI+j/ZcW8u74MK" +
        "+EcPCJs5d7J+HiJz+oBlEjXt6rtiyvUM6K54jataXxXYxyS2Amf9LQgpWroJUL0RNH30asZumHM7AF3gUPmz19wdmNmIBauMYpmD" +
        "yxGrfK4Bk6h0YzisqG30sf6tdf4eStGPTgk3oF+4hyHiLhUpDIxYEZjqVBOph+XPH1vGqlLyw35dsQoAthrvG1acHDgL97gE+ODM" +
        "KDEKF0Tgvcm/ovGIlAQcaG0OsPwxCwr5nXK/3ZWzfuCJuSQG5E+ndlGW5N615UM+8gTCNXoUgF3CrRcO4MH2sn5cw1AGBVIWwHhq" +
        "9kO9CxKzP5o8jw5riWLDkiXVWi9ioh3BEMi+Wl2Lw5Xl40DXu2zjUZ4lFa4ABdL3NIx4XzdRwjt3nilpAb1Z6jzoUI435UeKOpft" +
        "BmMEAOO7Bduo2B07+mU80RVQ/fItvPGCX1uXJkAqDk9tajRYHfkzbnWrLLBsxyAYLqUsZpSz0mKFYxxXLWFdSNKhctXapIDNLQ6f" +
        "LyVy1PJ1WT9cX56uANqTImszEYuxd73R5LA2Z+HV6r37ABdDZbYBV2f57BF5uXd1Nj3QN+W6omkDXvOIVwCskOF+is7vrNViR6Fh" +
        "ItqWxaJ9dzjxzFxXNbts0znxfQn4YYj/GhrTTbWTinkwDrMQQA8EQW1tJ5WIDP0uQKEK4tO2GExrWcVzNtBhyEPMuM95QCWxg6KH" +
        "JbJAYYeUTNpfu5x92gmc39R7H7QjNoXfgoOwDg82zXxE1TjhdrvwPgj4+Sgi984KJz5jPSVUeG56EOQe6HeN6d8wvP6BxUYOBoQ7" +
        "yxM+Z1U0GQlWidtTHO6nvvc9tBVN+pRQVzOlgcW+FE/MYVEAZkClmVxH+9TyucOcVq+opUHbFW1saRZul8+PQpLfMVSpeOwvfhqt" +
        "eQQjTnI1uSQvTvjJ8VD3Yis47Qx+7hKfuzOuFMkOl/v4vBuzcJO67jnY+iHxZHH58nZsOD5Gfw9u0xZGWJbXF7uY+O1JKx5gOvB3" +
        "y3zMd8HlB61JXQe4cf0uxhYcbdZcyf6Og4SsZXCOj0bZS98AErIdW3VXcZt/WQqGEFHj04YWBHE1KvXUut5jJABg3iHaUz4J2Z1j" +
        "CONX/nhMtMj5ZLRL2tQhHr1QCPzPVehM34PtK2VPnCjcSBUFAAAOAAAAAAAAABIAMpAKMBwQE4LqRosAIQsJiQAEDQD1j0gBV/1k" +
        "QAI/4i4AKGwo2CsoK0QnXiX+Je/vy89v8A/vj06uT0+vz07ubG7vT49vDpARDhEuL/Gw762ybk4PLuyOlnNQbpBSl3BxEG+P76+w" +
        "MJARsK7u0Cqr0PETtJAPdRoPyQGBAIDAgEDaq0IU1SldSPv8PBpAuhOlfF2bsrfKymJh62oOd0D2mQDwW3qr18aewuJrrzxP+PRR" +
        "mnUagjlKV4YIhR9Zeln4DzJXTMh6Y0HcMNSTZvlb9IS8Cpt/wRFBclF8VQIIgF3PJ9HO2k38XjdbvA80sI/tPgA8ODPY5pe8IQP7" +
        "MZU9ZB1skQjnzPV6BA2WoRfFLLpzra28s894yBIUE2/eDpzHU7W1g0y+C9+EyW1S2vDbt0S6n9806EGLY2+0QY09nlP+T+xoBkhA" +
        "IIsDUqvjd+0yb8aPyVqG3wWNlN3fspajt0cCWPhtJAo0d24u+6WiiCCRr41RRIWDnn95i4v2l7j7UopQi4u3uD5e2Kx3ZS6VMvzv" +
        "dReVojqbltGD35y8ZZ6M30MtQEPP0v42PhrT4JAiN5y38zFtHWS4SnFsGOrltc/VDrOQlztfMVvZ+m1k0MVPEz2C6Np2cgB8LME3" +
        "4/rxRaugwRc2+lTNvvkPsXvqXjejc0bILoqBE9IshI+L1w64g+m9CuGxNtlbsojhu5xbnU9xx7CY3gw6PDNNBKJKoA7jTh9eSBmX" +
        "WnzhyDmVRBVH/jNbHB1Xd2CDtJwYdSC+oM7RPdj3WN1xZZfEgEF1kh+YmOASfAXrgcKmbfBr+9etKrhU6uVxJwNIRswnLp+RxDRa" +
        "bDPvvPMnTTDflGpkI5v7srrebvvPJXjoyTES0R136t0ACTpoDaHiM+zPXIgR8YRoo5BXoGCEfXDWUaUMS0n+Kk3HQxSj9CXsnSPq" +
        "Qd+sqfjhWka5AHB0eqodUFpMbXYIYBA2j4S4jEbtCr6W2o23/a9yOx4MnRoJMHUHJtFPxeXjYL4N/8vIXu8HZ2KFr1mP/SRUu3A9" +
        "G5C4bN7b1jxSOE8dPAOnGSi1NHu8Se5UcVgRQMSMZPt9w9IHvaEFcoYHyoEpqGOk2r8NkUyHMeijNWtMYyv+Zh2TJiGe1+HJbJtr" +
        "Zo6L61uiuBbT9BHuE3VUK+OOzzwjnO7mc++N+Ll1e8pWn4ciMRy68Fu93oG1jElEfHJNlzGBAeYAF08gdsOR35kW/jlXC0O6OzGV" +
        "1xutdMB+m/WzZ7NTIOocLCmmaJtH8S9WVHaM736BgF9Xu6iSurE2rWTcyfeCI/5S4wjbcaI1WAux6S98FsjrrkxpYoXgIRDLB2Gb" +
        "zAFlhRCYfk2xbRZIQYoKr9+RlR7fGusP4Qr43ukfAJzYSfWfArMSNCIxBr9QB0TkHDtFWqRhy35sdfOItGcZ8BAXKLAQNlXfUF/X" +
        "sMA4LBeFuQ4Na1wgBi1I3vmOoXzjO5Hz9CjkSzXlCPNs7Qp0JKC9LnoIO8BEjkWPAKQYKVrb7oA7k+bD59tUOORVoNJCPRQdu8xq" +
        "/5fA5rN9w0enk4iUqN7TWhcppJiTdLRVDFLzioQCPDo2hN60VDQkYOVv2W5+iMjbZcaYdcv+Kj5uvWaiRJRWTZwdvkihLt+AyCXj" +
        "MJMbT4azwadmOCT6aCtF3yTHQQ69HjIbVLqkXL3zJADhnOfXX/mUAD2ksz+xWku7B9g/C4Ne/UHbUVD4oMUFWhBMOE9QoXN8BQAA" +
        "AA8AAAAAAAAAEgAaAbg5BgAAEAAAAAAAAAASADK0DDAgYA9DQnosAIMvJiREEDQCCxFgAzNjNsOIculEH/QhAAl/iRAAj/iXvz9H" +
        "uT4+wD27tbU8vb89urivOjy+vjw6QEK5Rje/RUS+tUg7OT26NTrYS8G6QUhewEdBQL27P8DBPUDHQL6+rDTESkxOvL7R4MIkBgQC" +
        "AwIBAODA5KyHYYco3C0Z8IQ/CAzUDFiISdb4HC0LqqsvJ0OkD05MaFv6qizMbUYQWYhiXxgnl59CU8J3dUsheG8BcXqqTxspzLtY" +
        "Qo+tmXbRrhHP8vEDBMJNlaesICzSRd65YePXt3xk0g2U6OZ8BOtHo8Mv0WeiJsXVjzwzs+BixLp3PZLqtSgmiY06icCcX3cytTKu" +
        "zUhsbjCtyxAtbnk2zN5R6rVMduT3Cjrr4igMwkemU/60rqnG/UwLmyzPIack60eMspiSQwTA8pTBIAhBoUISadLUqsb/DeGYWYOt" +
        "ypnmZy/tVcREvd8FJczXlPOiIYMMZKIUhi+QnvnZpcJa7Em0WeLmg3oKnT2KqAU32pelQVqMcLhXFTZhITdvgNIxqxovrH4Qknp8" +
        "IwIH90CwY+7k5JVlYn+ozS1+/hPe9v5lMXSP8WyB005kn3tVw54K7kUCYXa6h2+AHsdB3nrkknWEfL1y+7G7JeRZQdiE4obkzRHO" +
        "T45JcboHmPb/yGp33h7AQ95w1mRdmjrOtV5WmRtVEABdl/fW36CGvIWP+iXIbguTJsjVJqEAYtdemgCAEfs5hxQiO1hLA4fSY6x+" +
        "50TpUDxfM2eHC3fi2NVatETacatt5q9vMTvpFQxFl3ch/dbpMw1LSMhU9wRn5oZPR7CTv5KX3JSReBhy/ygDghybo8CKK+om+Qhl" +
        "zKJCOjFpjGNxZ8Dfp+jQOcDRWSwBQooA7FAo8wSeP0AX+jte3HfNaHgGA2asb21BKdN0GWG+YKbFUVIjqJ2TosAsnk7euvqRkLZT" +
        "8YFJKIqfbJ/hEUfFP3TjbuDMm75esc7IhZsnSqylywlPFbNhFisozu0KCpUm2aN5VdfbrYUHJGZjKBhH1rTm+u2/9vTmXErbYdVY" +
        "y+9+sXKstDFE8WFHa+qKuk9td3b500Oey7kr4WmD02b+gJAkbk/o4FQ5qXMO5zfzY8HtcrMsEi/G/7/Hjh/Ye3uBuf/5ozslTxua" +
        "v315bTXbjyrXhCfky+zjU5FqAyFQNT63r0lHPfWYUahJruDpORPLccEnIv2UxH06YZDN5594nfk1dp04Dbp4zxvh/DXrFLYKEaQ8" +
        "bDNQwB3ENmRMCliw7UoGNLPeF6sFYdp9E9pL5Rq/TzVtOFa5nIWsmkFFYaG7u/K7Go1kKsmi6gtQt9HDp4HWwNVbTjjrb+rW8L/v" +
        "zPYokOWRRa2VptwD7F2ae2JaOulGTAymfBMNM8FqhyI6rFXRo6P2uTPazu8sNjdIVuY5WnPGbcp2Smaek7NX1IaC9Fr97vHopzgN" +
        "shy97XB/VFunyMN1nXkBleIDIxRry2D3ipUIQ+NZ4tcL1DDHVYWFWKvIBkgwFVkFQa1OgMTm4PeuDi5LW3bHyie9wiKHbWxkDUgJ" +
        "MVoAMT7ZFZg53AayAAr2QY9+FfAnZJmnXHOSpxDtj9kVCiE8cWTBec4E+aedJzX5502GJH/WQnTj8Xl4BhV+wRtWTni92s2A2PbC" +
        "CBcpWPFjp1GWnPAwNgI1r3a+iU0bNSZxxXkI2tQ0SVoj7BgiyAwWpPHRXL91789K4zSUBzLdaJ4WmJ38Y1ol5HIAJpyU5LoAtpw5" +
        "xuJH0cKAoeU7JcPAkw6qEMqx0eF0nWteXkhve+oElNHgA27IwZustl81llSruxN+kPyN30GXr8gXCLZEqMVIdQq5aMHe3W3fdIvX" +
        "eqVaQ1XWS24UadXqF2L1C00L4TKuiTTnwFAp6QN1a06XGZTniujDabKPWo8BePHtXfbIydehyvmciVn977AYgH4p2dmqWoI5hDmL" +
        "Gx9WxPRG3jN93wGvyM9iO6ldSWTJuet4SkOeG+KEnW5GIYmzJ7PCjlPlRoV4o/OJtuIjLEtL+OuJwYDXMktyp2GeeEtNSTAdOKPt" +
        "xc9nkut7qCJuGFq1UB5lgOJf7ehqag2YMpkXzjDrRaZ4gi8kDUGS8wGN+iBYBAAAEQAAAAAAAAASADLTCDAiBB3jQkaLACi0SYkA" +
        "BA8Aj/lQAWmxeomz/bRAAn/iRAAl/ifvrwqPT8/v727Mjc8Pj68ujSuOjy+PTq2wsG4wzi+xcM9t8o5uD05tTnZTMG6QcpcQklDP" +
        "zw8Pz89PUNBRb68KzZBxM9QPcLRZcAkBgQCAwIBAuFfgAnYhetnuwOJHh4KRVbRJ94xVelDyYd2+ngr9YCGPth2VLolagygJxVY9" +
        "MkoopeTO5DqOediRaa3rV+d29181b1RPXi4W+ljJO2YIacCFwM79IEcmisFPHwBmiWrx7XprC89Xg7tvbghJl4Wkv53EuyVadd9m" +
        "NH2zcPXHe1p5H0chvJ1qcVCGoznNonSptdzGWYpNQwruHbo/hfeWFU7zFck7UXq4A71YoRqiYRi4XzgZYu0vwTTSr6SjZ0qwg4Aw" +
        "0s5rfdBhWceqJ6+s+tCVpMknQcU7gQ+uMGx7xmAQ31/uzI1gE+lK98qYevBlYEkvfVml6IybwyBnv49WGGYXorro5yJTuCuDrse2" +
        "nPOCavjdJ07hd37BP0CKz55+qiPu/i0PkmVPrUe3+RAva0JdlVa4+JPsa0fAJIDlsliDeG4zkTVZdf4RjtNHeQTq3Tmamph3LW6d" +
        "xCpiP095Xu4FvuFFJgJvSj+tEQ5SAON+Ekxmdddds3HW0gsKnbOMBsSChfOWmNNX+3OGDkxO6PaiGRGfiNHEbwoYz1gvbRoyYXGJ" +
        "Dc0dIsP5q1vEeZqajtAc/0QUM2/cYx0pOw/HDSCM4QrfSYVhNF9vNSVAf7RHqcBBELdjkixjy8G48BRJISN2K0n0y+EB07CcHlsB" +
        "jPsATxrqUQaz5eBCmFDgoTEpizckcRRD7MgYeFkyt7aUdSFw/sBasdYzD/U5GYrY1EeQnh7GqzFCcDnfOfLBBaEeUNsQvJTYDq26" +
        "mmg5qu7H1cVnz9JG7JKWJJoyCiLpUuQA2wqiWcM1cpQSTFCBmbsuXztAuUN/y7ep/rJmyFvNGYrqdkNTICz7bYvnV4yDLvo24YsQ" +
        "e8f718y8x273hK6wtz0kgF+yE5sUm9Ckc+eizCNCd9MIDKmod/jl6QstPSo68W8bZounVVlIKluaH7V0+BpK9rkjSAdR4LZmTUZo" +
        "5Zd0CqSXyxgCRYmefLB8J+wy8dpmYJmoQa2u4IVfG4A3V2LFMGAe4odjRgfuTBU0Wh2+U3ksaYFMG76T1rFkZIce/Ajo5LFPa2aj" +
        "DztGEANwDhvPJ2z5unlf7Vq7pP7hj35S7SABH6HlAQEK/vHI+j1fLORkeLuHNl+rFs/LgzgwXTSnh7lqqa9KZ3mmW9XNUPrPeK6/" +
        "ptMH5KDnmsZO7glx0TaAyHJSXsPUsr6N3PLRKhDGwCDp88SypaVFozJOcsv522L1LsidhEDR9hunoOHq2lvuborvImowYWTwBphp" +
        "FOTXrTZg+hrBTFtyCF1DkT9GXqN3M1r+8o5yn0YxwfmEJqLSKpzghQQPMAUAAAASAAAAAAAAABIAGgHYdxIAABMAAAAAAAAAEgAy" +
        "1RcoFagLOQ09E4GKItJiUAEkEDQCdLkgBh/18QAK/4kQAKf4n7+9trw/P8A+PCk5vb8/Pbw3sjs9vr48uUFCOEW4vsVDPrbJuTi9" +
        "ujO72EzCucHJ3L/EQ0A7vb/BP8HCwb/BPSqzxERN0D8+Uuc/JAYEAgMCAQDaVplz9JX2MKqB4UoSk5ydffW/9AGx8+UMDF2RYUSd" +
        "6f4CGkuVri/iq2lXuEuqHnf56Iwf7YM0WFx5aPiPTZ/yInCJKfB+66pFsY613pyV+vdk/fYPakE3RmWxEtpaCBcMHHoZo9JmiZs0" +
        "le0YD55gYUO1jo6MbNg3X3cbiYyBiTAY/Beol51V/+8a3hFB0tUcFvtyAXc3KqcfbAyg6N38ooX/LOEN+kEWyKHJ8ycTvBcrwr5b" +
        "4l7oCMTFOC6ZX++/ptPdjWI4vTQPMZmbxMUp2wsRNiGnRdYboviT4BFVt5qc/cxT9fX+qP++xEpl2V2PB+jK9CnCht46kX/De6m6" +
        "nVVDuWElWlr+f3lWkv8R6IPynu/kwX8y4ET8eNC+9a0n7wWEPKaE6wYABCHJ+XF03qersgl1gLRWK91M+Ycse25mIztl1V0wzEsY" +
        "FQO3TwzPo4nYblSsATg2vVY+VrlmVQiKW9so+1SBNbSZhV7eY9QoaaTr5oy1H5edx4HyLDjxaG4eAtO/Sevqn6s83jop0AiZUzVS" +
        "yJ7FtAsQhXE4BBPbcQOXTVy62m/KbFBbJ7UVtJiwHa2+lFwAGY8arjAZLiF3DRHWnkHNsrWF/A2DfFrkr4JHlvSVCBe/yiqsO3K/" +
        "zAWX9Js5EjEazFWxOFgXnAh0wd2kLlREG9Sea6KkbFCSbLc3UnfMEtZecvTPkg5OqtGm1PRb92aAxLvYIWjddybgW2cTSYrgwZ6E" +
        "QnFQKRPQMaXFgmSKOBA0Lzh0j/BhVHwhKKVKNxQu1jSMV/nPrd94iCPaOEn7rlxhtMkMRz40SXUPZi2t2H0A2/BqjeywvMDojoWI" +
        "Qid32DTmL+ISS1pirvyxq04dAtjkfdXcA+WcTCL4aHIyXTuh3w1XYoqKRLL+ehXH9mgq2xRqUqX2X+p0YafgbRteIHeqEYLegNYZ" +
        "LXO1mwxmFf0Kuns1ICBmwMIzVP8wLH1/mtNutJatnbOYz1NKvgZyKvgPVkhZNKhzrhOKOzv55XySWVoSqN0ufjVxjOlgbyvO9Bjo" +
        "zvfjTGnj4+NrnpaV7c3UHqdwGQFzVR+CF9rEac5IDvO4pCRp8sQgppSfxtLSD1p2TeKr7WQPUym+e6B8Mzp33cJtOQip3ANC7dC6" +
        "r0kjWditU3ENlrNZa+/7Ji6m+6jRtLZte4OV5zzLYvKUq+676JHig7mSlsd0oHug248cULdbcTCpumXVGZM74czvOrCceS6TUWSA" +
        "br31ujyBGv0BLugfGrSFgEVg5daD6KRxSaM/cnfLerXppvxu3nv12V0cyUBF2ml8gxx/J+KGuaRA0u+D8hAFTGbc1qfXHfJHDyCp" +
        "Ox+t/y9uBj+QPGW0nWJmb1hp+0Q99zGMgiW3sTTRujNDPhOEgD8JWSij/8jQ0VSwdGTiOuWiMf5ulIkRpGRFJHpGIC6YbRrq1V7f" +
        "paVfXFGhfjAh3TwR+taBFfAnZiccvPyIMR5kSoDzh8n+n2dYD/5W+okTGPexR2X6Oo5J0sU7MIsOXXU2F2mJbjoI/YMtBG8eCIjc" +
        "ZMr7qX6vNIwKDovh+zVpNZr+mkQvoGGGgBCNrCbJ2HJF0pQqD+0Wxmpce+cMAAPPu+0OBouBEkrs0aRM/NE/0B2aKreTHwAx0aa/" +
        "J6RW+C+yP2VcgcwJTeSbcIn/k5HADNZX5J8zbcY0Y/lV1RDlprNwPLiUPQ+MVcleHA44RR9mVwVXE2JmnfXxAy7dUjWrldi1TTPp" +
        "0Hkql9gI0tButApK9Scv7I8HZAwmh5AJzIf0mUWCx25dWEBm0fG35bg62l1tr2/Ax2+6cHIqLes0HG/97R6lUgc9p/W3AIXL5e47" +
        "dQpKv52NlhQj90y0cMIEi/oUkUm66djue+moWOrAQZh4ZT38LmXsbej8+tRJVXkbU6gEZ1MMj17LSge/U5Ot9Kxn/ZmNTFQIY/KR" +
        "9NTxvFAUfTZ30GJgC8VJP+MQwjoSlElKD6IQ5fSnn5QyF3Y8X4JZETF1rzgiR9818+krNXZAgH1h6Dc+WyDOzyzdQxPGdhJnfJ5u" +
        "KcSC4G5fz/QdgjwPjATcF49iCFe7Sjh+Lc6vBh6Ahi6dKtxcr5b7hSWvIrj/Y2v5s7ti94UjDrxnnfjl5+tfuTdM7tMOlKZcDCLt" +
        "cupHn2W+QZbYzusovYfrqiKZoVyYUV0zUQ8VBVPoytOc1Z4EaKBOIb0sBp57IEIfkLFBfp5jiBhcVj/pwEucbpX7vrEawq0WFWRu" +
        "ZXwOIm2Tx9l3TODh9EbF4Pqc33mJ9jNns8ldi5xccCO1THDjoimPZMj3h8bn8U0SGgLIyD+uOQWO7fynVy9IX9IDlz7Xi0jWR2vX" +
        "vMOEBqUA8VJWw49bRuaRkAq6EXkAewLoxiWIOkdBx9vtMJf9Rsi0JZyYQQZxDF7lmm0p797XcyXVzgp0BufZ4A0mUQt7bkhHIann" +
        "6tzXhu/ukPjKHvaXmiS4g+mPoKNsxK6inWKU8/v25PotYpP++AeAfTu6XvUBrOJh7qKNgy/A3K6rWJZagsY+RevEn3XrzhnJ3an5" +
        "8HcKU8R7bhHHKzaMpqhvBYbHuhDy5yGTPGeQIW9RSNrg+6jOHv1Chd491yPMUJOGsBCN+IP2eE8jkF4edU2ORmGYbUzjsh+CWHeI" +
        "OOVhdKLj45rzoC8TmPwlBk0hqTOTqTSEb0y7kpTUm4RsDbifTDpcTWjO3tyLt+PxtThky/w8jaIxXo7afziBnRxbjcY4y2g9eHAa" +
        "5OKBexvD2wxmMPXb/D0NHaCi18B3TAy92h8MZPEM0f60EEdrJ2DnBKhOZsMyQXRe9WhcY22kKIY+aneHSoawb8fKAO8zE7ZHEOQn" +
        "1NOzmLvdwidItPw8KGkNjGEC02Vax/ncamy6OuysfGSyME34I0K3F0KUPP2NAYvFbxKzCe6uZB94uX+SS0Dvv6bg8qkXixG5oxw2" +
        "EfRF0zB1OkO23d7sPF9qvxV8VMTa+u/OBFL1wAEGtjuiE0jfkbH1XaRZHT2X4pAIVuh8ZOTGbJZwKo/lN2EMeNPgOkHVtjuXTRGG" +
        "oz8vjKlzoYkwBeiCouQV8LZ9X4+dgaEphlXTIVpgNb8z3e79scc9pppfbKTbS7qZ95uOhkBS7M95Rkd8kXVGIC/X/o2g6omu+rdP" +
        "UU6KqoJ+WpsGLzCX4B3c6U2GEXpGmYhVBKzmZ2LBmxhBzkQXJyjiBTbJG1lDRCd0ZKY4aZDyFlXKf4Z+67ARs3W6XYWRQKMuYxjV" +
        "8S7If7y/tdPrZRrVSSSTeQcRpoI0cE+3nNe3I6W1/QGL9SAulOeTgfadmZPZeaRK+SWsatXxnLCG/g4m7aRVnSUsmXuIzpxOLVHJ" +
        "JiLW5QJNV1jtI6/MnyuXVTZmHgAzfvyDGoptvVha1pwpCH8RTluUIjceHtALRP9qx/g7mesNECWMntApHivHUcXLYkxuxXSrsXBm" +
        "8I3d2jn5Q/tKTGoUnjeQBuwyZbqkZiqtBuY6WUSHKMvzf/M9smp74NAnrNzqOH7CSGW+RQ/Kk0LRIUgnok8E8F5tpOcxtUCqV/3c" +
        "u6kD0gMlXJ2nHfTQslr3ACSreuUiq7n/fyc0MBUSCrEmMNI6ZYk9nDbiT8PtMfovCNMNZ8w4ptplJfo4IRUhmExWKqIraHybdsKI" +
        "9+2lCi1zja9ruZllliClChWyYy1YRBkRs4TCG/CzwYyr4hESRE6Dm87P3p8Ib9eQBRaMmQ0ph+9nRYMvUVkQiEwnv7Z7RhE1DSA5" +
        "ksplHcRIVwZoTwsI27MnXx2Wk8goSGVrUk5htiWOJDfybqml2UWWbH63JqklWGK/4o3Mug9YI6qe46/K8JW6LSsilNLsXvC0o52F" +
        "3oG/jhdUjzV9Nrc7FzRRhRhaj6WNsevXfPq8TMI71DKaDTAmQhZzgnosAKOtJiREIDQCqY0gBG/0QQAK/4kQAKf4l749LTw/P8A+" +
        "PK+6vb8/vrw6Mrw+Pz68uz9DukO6QETEvrVKOLk9ujS62ExCOkHJXkDKv7w/vL+/wL7Dxz65vqgww8PRzsHA02fApAYEAgMCAQC3" +
        "L+C3ODJqzo0cGgbAH1HBziP9KnVqZN6o82BaH9Pu/ZQlmg7rgPiZn4pIFfIlwGxlcbkRmZrZXFeM/f8t9Qbgm7G4B1Gc+y4nriHr" +
        "CXOmu433oKomvKg6x3Wt8xuabQg0G8MoCKKo7hkVuvbR0ddfNpsjsx7I38M06oCbJ6mXfqjAInzSQksbsW+9eS6nYOIzg2cxpqpi" +
        "Vf9L/ZoEi2O3ZuxcdOO0r3PLQPnb+u4RmTfP/jO0PDeKwwDJnYUw6SUOBBlCO8XDPdindCn2i2sgLWpjQFmxQdtelUnVKWZ5hGJV" +
        "dnX591o9wi2PN1Us2j3hme2gMpkHJyQg9AxL7XCLJqthiRc46+DhQidseOFZ1jP8Kt0iEyNbNJ5W06YbI3LVeygCecVeFI5/NbYS" +
        "1DGsdYguF11PereaRP8DW4sp5ltaUATrtHXMAyYDIKnsUYrd86/IqTsB/zbBPsN153kmbmw0htVREQm0zY8mdIGELjJb5oOX0KNu" +
        "z4+9WZrIgIipfbwbW0Hm+k2gG+wKfxHQQo61GFhG/3EYrsC7dHReUM79XoUgm1kqfq1/tTQNF90qoHIMewDJq0N0cVjDt75qLlnL" +
        "2Jo0Y+wdP6bqQfDGBKsh3XrmQF2zwI8R1Cev0H8URNpV+PB6pvODpyWLwLtjwkEcKzWe+BIVht83OJIoi76LbGGIjbcxjNKAdbhQ" +
        "ez6TUEuuqbz0czln5BUIApMnARfP9Mrsie+yt6mcooe1SC8kCClIai8fEcXJVMsYyqLLM/YowJmDvab/sryY5cHxefFHzOxljx8t" +
        "ftOMFM4tIREouvV/F6CB7jmMUbBClIC3N9EYifjS4rnlwjnvaK0j9tx++P+E6Xgegu3EEL4gexyCeCAgiZ1q3JvwfqUafYDMf1pb" +
        "xgwm4N1nK9DE+oHvNZ0tbw3bzMHj1kZudFDEesnAVmLuMNaB7Uj+2isYlqKXHjKSIDrylohA9GGGXPcaHH2zATG8+PHrEydUesEv" +
        "lmskneYTUUtirOfoE5g/YtdehpEPKOzopC4f/eGpVI4l5JTI/p2Z0nrP/FGlN3R5gQwyU2I36VKv76csGmZ3hceCqVSjiJhIuo/K" +
        "0kmk2CP2RfRKiIW2XyCRafLUcqVUgom70P0vw1IB0fUoHUy643UO0rj03hmlX4YAG9OF5LFSXk/wBB6TgbywU7Z+XD01BJ/53FYS" +
        "IJPTjVd47DkMkYyt1oDZwD+IXItGSIvWybBsd+JcSFAXPZN6BIVYfcAw4JEB6FD+1geQ0QrVwerfz1MFMl13+1hL2P0rlFWX07Vj" +
        "KgRyASLJOViogjbjaqN3Jn+uSsMi8wODnv7ctnqMogkR/ydhsdKNaservcvnfttoc7hspsNq5wF8P5kYKFIT9z/hleDNkFmn5WZe" +
        "VciUlA0DSvPKQ2w4ZnheZ+CrTzUeLktIBePMNzLC/lL+FRDgvBnZxd+eRDZB1j05CPlUvWS0eHhU1YOUXcy2JbNigO23gmS9WduX" +
        "28Z/FX7XKomz4dW2bff+JkN6JtvyNlERtCUyBVVld8vlCVS2w25JlGldMe9CnmDNJCQKNQQjeLQ1GS49oSJtzxdhSENJewZOK/Hr" +
        "OgWq5GE+524OkhO4wkCD0x+HvCwEcfC4U6mYwKn5Wc1jqqTeyj7T3Lqde/Mngzc9AEV/aY0Q6JNGYmhia5slW2ekLnu9MxomBFaG" +
        "tSPkoIMLQml2xz1x5NS3PQBqQg1Z2WtIhwPQjyDuuUBANKykTX01OgH5BdHHiEAheppxUm5uWU0qAIZwUn48gTKpQT0hOdrKqBrZ" +
        "Lh29ecnNf3rpYYSFplph75YYQGUupwlJEGWQKi81d6I+uOaM6bDLgylgRJb3vg3+oBu6eIgibqsPhl6MxNDeavzRym+BTIWpJyP6" +
        "tf4KcRKEkEXENEBXSf4V/Lq6aC/t2pZFaubz+lFtLJkJUugHC392r7SiG98EljNzxFfJFrnXvbCNgrDGWYvu1zZIsn3K0fs0MegL" +
        "Y+qcCLo0Vjn3lOt7ih1mumdkCm/Fwg1SvSw0X7xhFRtQ4QLkmBbcllcdRkGGACgCgj6vKwP/fVmSa7FUxWW2ObposbOZWkzrecah" +
        "BAAAFAAAAAAAAAASADKcCTAoIA7DgkaLIB6zSYlAB4CA8At5hcAXTZdbFiHXJRhol//XxAAr/iRAAp/ifvz02PD8/v727qbk9vz8" +
        "9vDcxuz2+vjw5QEM5Rbm/RMO/Nki4ub46M7xYTUG6QcndPcRDP746P8K9wURCvsO8KjRFQU3Qv73T6D8kBgQCAwIBN5Hkqp3kc62" +
        "Yi9GPT6s4USeZoJ/C78IovLv3AgbBcP13G5mRq8a49jy5QIqgiXQF/RB2nbmyPXXUQjDgeVPJ+YG6p2/dJaFu7IM5ZSARsad3HsF" +
        "etSBi7FSG2kx4AUU6VrTCxCaP/QlUjAdoJ4YtyLZG9ATm4jZXrDNgjJJSfUpA5Bq/V4+1B/1ai4mMUqydlwJ35IwgESkBJKVcoQ4" +
        "TjH3jTjwVppoqm6hd7e/MFCUTpOnJshHLi80/uHsiNQLFbRo6HOGGJGYU1P6BmfwLJPWXOHXgS//EIhkWp1roci1DwlF/sN4TR+6" +
        "MvoyoABqtHVRJ5LYDJCazcE/j9+lXJ3Oq/NwXJE4Rpefxisg9+ylu6XInsBcxgayJ0jeTGbiyJVvtdO3cko8ghibIxk20dH0C9eV" +
        "JrD3pzhzvY2x+2L+pTFP273D2Eq73uYHCOI2OfZ+GTEM1tyn3hYkJS8LyUrdCIlM9v9jgowQt8G6iEgdYMVNrMthVP1w2/OKZaJI" +
        "3URruY/jjJ+4FYO05DypYRRj/+QVte3kBWu8nMWkioaBMcmPqnDzSf2rFa5TDl4XYS/V3m/j8W1TUMZ4mdT4IBhz9R+r3a9lV1kc" +
        "LVsDBsir18cssMlNdHia2er6yIiNGEpGCTSa2xCWkkz9s59QCEFGiQ5ZI4wOC5C4lOSIIC41tHq6mXmp7YdDjQx5sLuJv2tiVUZB" +
        "mt6hI/yuhsEK+SZBFaTVix4rYtDuTQjfGEk6WYh1YbCk7F6NiNBK/x968xArQFkyqiI/3IhkHT9tr/0jFyCoglFQwcj22RsgXN6T" +
        "bkFJEDlNiUQwnQu2DjScCe4YqKrDT95XxC3VniEummUa//Oi6rTNBz268H245MkMe4O7L39Jzr1ODRYLMDc/CKmQdN28HQtpxTER" +
        "yJ6lllGKpm5Y/zg4kybQAMWmVXyNHUQ9qv0Cb9mP41Vz+dbsjcjkK+SyurXfBqZ2dvb3ADucS1eTR+F3BwSRsvHomRnv0iJqBPaG" +
        "NtQ8rCmCeHCuWdw7qYkO2hYeHCOxDinuMGLBrasneKTHqJclmALwoZSrNgG9vY86wywg1E+WECwcEUaFacjVkYCjtA28feyMXzfx" +
        "oenufe3nmIi8DyzC+BwTxwbcc4TQYWYi9V2KUUZ4tTWRFuEswliVr/6SylIsbWVe8Bt6V5l42NacsN4P8mfamzD5xpvKokdi2B7N" +
        "diyuqRRDp/IV+4tcZ9i8o10t1BZDw4qrMSf3FgK/EikgV+Il+lr+g7WgDG29/C4D8etYpVzB9MOPyvVitoMNh8F87kbqyR/xqcbv" +
        "kzHkgVp7FR4PnxiCAZa6cnHKkF1lMg4lovk3UGgNDBldEUa/1oVyoN17kHDvm0xdEXq1X1wPsApYzWUdEEVoau2B0At2VFAFAAAA" +
        "FQAAAAAAAAASABoB6DEGAAAWAAAAAAAAABIAMqwMMCxEG7IqRotAJrNJiQEEDQDEzVwBuNm5sYodilGWibv9uEACn+IkACf+I+/v" +
        "Tc8Pz/APTsmuT2+vz07NbE6PT49uze/QzlFN8BGxT23Sbg4PLozuljOwTrByd1BRcQ+PLtAwbnBRsJAvr+qtkTFUE2/vVLhwKQGB" +
        "AIDAgEDOf7YfYR+e+CmR64DXjSzZP9Quffw0Ndua65q0F+ZYInWy3JW+reJv9YHm3ODZvPJvhVSWsIhS127YovsUIxTx6IWSYdHL" +
        "blIA91N7dqC4updbAm9Oo5JjaBg9vtbo2TwPUQ82kv4t3Z9Rf+HXgRA5sc4cOOygDgd15e07zk42r4Ubk9SJHrLlMTvFESaDckeZ" +
        "dE7aKCcJUGCz/kA0uHUH6ISd5Ozz00Qlf97XSuzE7K7jGLJYtw0N4hId2Jtj35avjunHTldNr4xOPWNPtP2AiML4LM+R0bOsKCbF" +
        "qlhAxNelI9i21APT/v6D8s7Vozd44j66hQ4OmzvBFQ1IAjEM9zi9VvQFOlNfX+5VJNanemLe575qTzX5O1Y5JVelloHADRH7ydB+" +
        "kg8twpiq5Mw/9KBMftOpyBPQNgLneFOvj5hnUiJkEzuZ7iRE2dLZrtmoDcK3CAaLdrp+fwdfewCtyZa+bnSyic9SA2v4mT79eZYe" +
        "V8SkXDVihUk7bL2veQJhvVwSRXDqdnedRdbGYNhf/WAx/ay8EJsmpYfAto2S/mH1jmB6WnOiZSFyFCgQAgDJL4gBSiDNNZAJzYJk" +
        "ijFkS2kEa1aCU8q3fd5N50miI5k5aqwSnSj0ozMwoSgtsPYocRh8YGUGWl4Yf/Myn/WrNBMrJgC6hBH714xnEFoSV+fS3aftajFm" +
        "jwjNsMeceQZvNuAHGtduTITART5suIaIX9Zo68wR4tn/F1J4To6++92dWuArkhoFKwFkRh9SWRkCcsVsvmKeHF1gZgpY34AYPdnt" +
        "YUxt/n+sEpf8aObbvsiQt0pHXGv1RV64e+moZlnT7I0VUV3bvSqDmH5GoHa5zvY5oD4+bmk6BcYCuBmUEUer6+C4olzm6fTIqxwa" +
        "boq81HAA7JKAz/JWS7Ce2vKO0gpFboK2kzwYundIBqkf11SPzVXi53Jv3R6fAD/4g0smo3qS4n41Ll/6VAhnaVf3VJBtAbRD3BS2" +
        "5I+auGOVqkuQ6RpfGtLN/NC3skdFTvsHgncrLhicYNvJrLPpwFv9is1xitvalXFSQ14Rdmg/wNRlItD17SQhz/eVb0bKzDh8czbk" +
        "TETCeMDNNa4Vw0LM75xLtOsCELL0SOOpC85AwqpIypWsGuFdtje5ktjd8UOLN/C83yBWBI/9lhYQ3XtdprWn5R5YOzTw20fpFcPY" +
        "h9EHRydAt5B+HcKGQbvBbIxHegjSCJYtVNVuVuijwUA0VXBZHmBzml1yoPuEjhiYw2PIpYRMTIOK0RPSXJEcU76gQPSGxpeTtNWF" +
        "zAXrmwoI9QINBKgJjaEdWLxIZDuyqinCkS2NExNmugM6AxIrPRJcdHr0LLl273tvCQ4pwFNG7eQnmXN26cDjvgvjdijRGsJShEa0" +
        "+2NtKyA5Q6Q4ly2NgJr3oayJwTLgVqup9+EXl+M7DWCZzmjS4jv8dRoM/8QEy2y1e+s0i1uhV6ZJyZEPQAOCFUCYAUNvClRyM163" +
        "N8MK+XnkfF/wB0x+V5jxE26MHGL68MfLl9Ps+vnPdTTEDQ6KPKgfUGOjVV4J9PNEzn5eK8teBkz8YMbQ86l4P4+AceUA/VxM9a/W" +
        "eVXhsTk6rfrwJaeK75NbEa4HHYcoGIb8RZ5zBTACrhPWfdlTqpfXW6B7HlUBmIN3cYVvhAI/LGXXLnG1dkSR1S/ahQn6tdgLq065" +
        "o17GKBdgcqKLD+chFDl9yRlv0Yizmb03fFvo1r5H3y2cBBWKLakfLpkpj4iAHbmnjHTe80/f5ZaMwKOkZsXwNuXSkKreiAKqxKMK" +
        "NHQLH5g+uclF5m+B7FwNzS9OMpSoc2qXwr2DNvZ5Qpf2ebUg1GzOw3JD5McUWlWJ/BYMxPoU6mE5LlYHODLxHTbFe83sDn/z8YER" +
        "E3izBo7sosIcY8LEfHmYdma1s1oJ8x7o3964Y/3wBEOF6MA1BAAAFwAAAAAAAAASADKwCDAuCBNyGkaLYCbbSYkABA8A0gJUASTZ" +
        "JbEWHPP88EACv+IkACf+Ie/PLa8P7+/vbw1t70+Pry6tTM7PT49vDo/QzrEOb/FRT21STi4Pzs0uljMwjjASd3BRUS+vry9wEG+P" +
        "sHBvj8rM0JNUEs+w1PhwKQGBAIDAgECx8liXlzsXXbEEqON3TF0aM1ZSwqgOl12/AHjRjOz6vpku+qh6cHTm6/YwjvlaUtJIyQiV" +
        "DIxlSp91PCfkjYgkHztK/eSTL6uNzzWpmjgLdtmtl+YBn8Ou8nDclhpyAXqiHOFiiPgAWnSIYxSHSE7QjfJP6C6vZ448qI/DNkIB" +
        "FMEx+ZjI61OhxssLo3DNMGQmg6XQBZuJSWpYkacITHQ3x0mQmaMaLsPgedcD9ErFfwYP+qG+AhxyPlNJrlbXvXraD8L4aA+9FnU7" +
        "zOStHqTR2nECXJqgG3do12Nzb8wCUAxfikwwG+iPvkgzmp8bzzaexRT/LVqqjy+lTlUhxymYNIDmQUKdhN/8uvfAriwSfjP2h4PS" +
        "2Iy2K44rVsvtlrh8U5QGodrpHfgIXe5vXcuWlo+3qwSKlT0uHnjGzW+OvIL4rLpW9+ROeYvOniGtaX0CvXicBgVvwthViD9dNTgv" +
        "TmHxv1WsUWFM28u88DtztikdI3hbj0b4QpKb9llMdjJ8cTCOsMTVpTwJ0iFBPhteTCrpbxUv9MJMgh5iKpA99n9rkG7+hwX1ynCE" +
        "mSSPqCsDZUR84qo+Vd99aYDMiDKhecgavB+neZCdp6dtE7HJLvUp0iY1MPBHRTs1VZuIGAVlwbti72I4uULa6+jVSevR3P8jcETb" +
        "5atKXTLcZ6kLPwIu8eGYT2uFhCxo0u9ZzN+ffCRws4j/ts2U2zt48CH5ik6n/6etDWG1LI/4hCvXBYxQTwStlk1zkxqUt9IrhK6/" +
        "0V2g5Nj1SJqL4uZLvl47h0LZeY95dOosKtj1sfPcWehvjfgel0YsvS4hEI8umsygKFiJ+V6yW9pGfIinKlAZZkXVL87lOuU2u63W" +
        "Nei6MEpKxEjbU4SUEn+yCf9hlvL+hj6XfkbIWe6YyalzXjoI8Uwp2qdeh/0I3x3oJ81gHl1mPfXFOLXkQqsKezh3qfJj5xuT3s0s" +
        "Okba+Hy04SH8c9f0d1imdoXS1ysNGtWZdd9h1LBqtoQXl+2OXYIK7C/fyxErCiOlrR7Bb/W+HnFXI9KSTCMKd8siTi8hAb828GNW" +
        "QzQ0JvMauK7tVpeekU3+mRcpMt9xsqFJzUzfbjMJUD2tPHn50R/KVZBPtYEDdNaU1qQA5qHZxciXHQlrxj7BAmp+AFPUPc2h0KoS" +
        "DNx4RFuH8G0Jc6BMrEBvt/WIuOkuBcrMYboZoZgAqGD7XC0RbHzLn19qN5PviOSyuAy4sQTqxND0Z52DDxvlkt11EwAucv5T4DgF" +
        "AAAAGAAAAAAAAAASABoBqN4iAAAZAAAAAAAAABIAMt8gKB9gKzIcvQzAPJdTEAAJUoBvm6QAs/6uIAEv8UIAEv8S+CYXp5f4GAfn" +
        "dscXp+f3x1XGh3fH19d3F/hnKLcn+LiH1oknJvfXVqeayZhXKAkL1/iIiBeXp/fn2DhH9+fYNQZoOOop96g6jLfUgMCAQGBAIN0a" +
        "I0/4hyt1pL6OFdQ/HhdUiEtTqtA6nsaWo6gA+bFb3XnS3+Emdp2JZk+O3h/+bvEvedBidr72KHg7vxGytywaAKbTpB1U+z7Fbhn8" +
        "ysZqQm0joEmT5+wt8LH3GLHE6tl0VkbAoKNp+7OOqWQepjwgzmOApS/qK86cYRX8E8tuMarql5gjKBw/gJmD10plS4+FkJev/VOE" +
        "IiF6mCTk4Uv6XYMBc0U8tha2W5nZwe4et5+HTkPPj0/BWfB+1ifOChwA4DO0Cuw84Y48xZA/uB6hxDVXYqTJYQHJGrOjlq6glyf8" +
        "/HzCxwsgWS3uvWdsWF8HoOQDkvX9UzSnlLlWXG1ypmKDojjZ54FsYNvA44r+S6UIlRJXcwodA4909K7/aS206bAj4qm+YSAIJqD0" +
        "lhEY5p0F0vysiwIeikhQF7Wj9K1u7+iMTJTkLQmVOcN/RToFbY2Mq+ZacFGoqrFb9yxrV4Lk9mePvU1U+qrvIAuV4NDvbspUcUoQ" +
        "wxnAGZpU1YfnCigp77HpwR92rFlZHWAJPd90cXNcOdV3fZduY5qRLPIYyPvi1cnqBamrPCOMmHcRkmGnqgVVdk5+J6lyTh1tSfx7" +
        "hSzMVwqvpy0RO7UW0eQUuL2O5rc/00IJSOFHFlCbQ66biwNYgqWylE44arPcwUmOglbE45+mZ5FDjiWjh0fPh816LcJHzI2XxR8F" +
        "m+VLcwjcco91M3YCXOojyhUoTBAG9hbWZ9PfwWHQGkaWEoqrMwcfXfvYF9WqosEFb+VwtSLlm8zVuR1fxBJg3ZOiz+Stj1PkZbTk" +
        "Y1UL+lILs5Cz3qrjxpmtZ0YSAdDOWlBgwLhQ7/nkDgDF5IIbuibhbLQY/LCo80/PFcvGw79hRCGE5r1m04iC927P/BkzMSktRimo" +
        "ttalmf/4jE1g9OaDRGX2z8gedbPBO/q8zoe3MNh/4e9xdOYwQnkOVP73iB56FrdNSmalSTlfK5erZip/0XQqbMA86Nf1CphFBble" +
        "/tfAdlRPcxZbwdZ+p4uhgGdXGdYuNK3RuCJ1hTcZ2Wo5/VyEThTbqk9DqW2P66L5cweKYCknVPXgvZ8ZpPEh8H/uSC7NDIit5IFd" +
        "fEH101oCXqO2JBY2zU4YvZffOOjifQa5xBhEqScUqKis4I/E9/OZWy+a7UtWDwXt120Vs2E0MdFfk6nz8mD6waCckGCeCvk9kE7Q" +
        "wLc6HyK7c8DWz/GMIfxBtebFUlnWO9xGEPsXu7roudVwdQJo6ZdGqtViFXkHIELq0P4ucKhqE426lFR33z9mCYG8LBxfVV8sSUno" +
        "/LKG5/dFBoVDxRRKkLGD4zXfgJ7shEGXeSxrV0gEfOZsDqHesyzL3O1bWpXmbgAi1ax4yz7F4sIS1oOdVKTmyWeOnYFmvS/6nXIn" +
        "XAVjy3ZriVdto4hn4c7dAbW3Cala3hqDbE1IUoTGpU8raK3KLHo/2HyaU5AZCsokKhVtDDLdg9cegUaxjkg/oNIDG+GWR0npErRy" +
        "6ganzQffBFrdL7Pur9FOyB4TigXQ5LE2oibTGgNm4MDiCMP1xgl+Y9Hrrca6cy5iV+9NJkbIaOuw6USFJaQv1bEHwAaowVQ+JZ8s" +
        "sprelCgi0SuYPTtSEXlxV2nSjmCILCnqIfeCEiKOxAy/6YX+dbb0aOuF7QwLBRu6oHGvESpTEqhi2zf9CneM4BQ9DnoyVHMoJodf" +
        "QA1/RfAFyRiLOa6jZ9dB5dXNMv2ziIDPJpT/MHWRIN+S5biffPwPXTXd/NJc9/vqpBoSQjk2DJO8zBbF/pYksFFsN6hIdfduQWw2" +
        "GhAEYu9Ouu0LA3yDu6h0mMcelAJy9gZGtzmZz41UMaL26pwmrKys6etByJxLsDNTr/pN2IZH2tFa1DpF+1IElQh+hLxaYErGJ2oD" +
        "E0TvHkJRsqtlYzrUU4LqH4NFZdegCaESQ8QO62fwN0aj8YHwrLKrX7Spy6Ga2im++bogrgAbKuSx5BLGC6Zyhb46X9PVC+dziTtG" +
        "9gxWSCyZJyP0al7KCMuybu69/EsagZLrCaqLTp2zOcHU2dzsaT8SMMaupHtRgXuAx0SCMGCN9Jk1D2rrBXjy2SUjPsfKQT1VmLFw" +
        "+jZ5FHOkPe08unZ2KmUbWA+rbaeoEJ6iRxk47S26MMiOovVL1asB3dBDgAxciRtgKhpxb5R/VrDWwa0rOZr0y+bw45hjyLLuBdMB" +
        "Yem7ggRKrKnPDg9+s7ISO0a7peJ0m502geeZw44eul41PR0Xts7NQzBkHmdT0mmEV/P5skPMpYPj1iJmrgujSqU0/w8irAndfYXA" +
        "6AOhozSRwFd+vzJOrpKP0q1wYBuLEouS7NHm16t3DlbUGo3fWaRw0UhCV+SmqtJR/SygIRGF8vYNNdXrp/Q2PNTjnu2KVYm3oOXf" +
        "BhrvOJ+lGW8XXGIePw/yRMl2m9lg8r9f2VH9ipbNoD1YRJqrbWlddGcMLGkmqgZBdFjOic+0PF6Kt4M42Goek6tnrEU9XUGPFWyl" +
        "Vk1t4PuQeFyH747pU5z3SvlO/BVqQH7KITWT6uPCckPtEny7LLJySsE90bEqMlJ8UPLOmyiDxlJWEBt5/uZZoo3Teds3010iDHts" +
        "xsF6tSvWEfr1+YzEA5fTfRXLr3yWcXC4lDRYT75vqk53TVuLlk3Sy7Z4Ti/vDWBoIWlXPzaETQPFU1Aq1DQLrFBZ8UgMR2Rlsk8J" +
        "tYzssHaKo0EXHuNglk1TXm2YTRbYVjm5+TwyuEdyVnMDpACU4EuC0pegM46KLrE+Nfyp54dydu8Op7NeaPHvlfaeMN73aEBQi5IQ" +
        "iENHnwLiU+Ynz9Q0EezCZatYLf11i0gsT0/C8uxKjO/8Uao+Za7r6aF6tznBMO0ZY7VbeqdC60BhDLl5TWMcHWNJchqIoSlH9Bg+" +
        "WwNE9hrkEkPGSmvtqT+Y3ijrspmVCOdHQ9k/e9UKaWh4HRNqTeuWI3jFN623/XCnev7QVV1EvcPbv3BJDMBZlSruK49MNRl7O88l" +
        "kKlA8RAUK6g3z2lFJMq0N2TnZ2f4lVtVVlXdWuAzz3XeZB7Gc00wtfHl+2O18jlWegCwjWAueiwek24M5kyWNE4ImERak2Lfv/sg" +
        "CsI7DQAmrnJAYVwwq//aBlgNSFeDfcq0i4gqPGwSWSypjEOGoSP8ZkS5TmKsXb/3epzyGHeTFz3Pqo0Tp4zAkg5Vt//WrMyf15Zt" +
        "PIZqxtkSc5uOawzHX66s1NZByg+v2lsKOJYZjMv8Scthd/7xkgdPc2fB+73aQsAKJTgL/i9/4+Lol+FFGJBrB6Pc6S7e8RrCOrN/" +
        "B8Hd9eUMpz30tZrzWm0m07Nj92Q1ZyDUlUen8cXAQai3m8pPcXP8pmMGbteWWJH+3AepJoVsLHjajpMEMILuMS4S4VHaw2jMmqyv" +
        "z1MICTLOY/1/cKWMl7RTzRz+XE7mAG/noeUiW8YdCEHvuUxkFOarnuzki0hWpt8Wvp2MywmJsTGvT7HlGtXi7Dn/obNBb5vQsIAq" +
        "ujU//6oVu/CvuOoIFD2FVNvJvWT+nmiplzaXQj2YHOrWBK5ycqG4pVX5ffv49bLSXZkRv+gExzDoVcltRq0i1XV/JkUjdeLrSoLv" +
        "i6E1uOy/THTHQ8hHF5Bm/eoH+ru1SEDQMOpGhbKiPpzEiB7TQ+YUNW4KDQOob67YpzCXPh/QvUjuzrDGmZYPqulDmwTE++FGRKOM" +
        "K7VFGA5uEmy2fkAEqOlhzQVL3ZUaLQFs08RNCEnt5xbjFHdg3zKE/84ugfXXDm3srIMhgq3HeM//oABsPxt5xHOct1lO28BcwMV7" +
        "08oXIex2uxfki+rmIdq29++dsRvGmyDNzi7M3FYvzIyJMFzwutePUVJwZz8B4gon28eyVu8v2KYypwhMbIGzMFIQePJCFSeQ2U/v" +
        "BMVEGIDjcQs98glSkct6oCn/3dWWTJ6r13wFBliNvnFYu61tXzQOpQphmYfbJ9+ehkm201ckbJ1XORJqcCp/0CMsdPT7fY1AnLpv" +
        "NXCJkKgYhdPFztNcXh4MtPjt1VxNP3av2xRIPwt/RL63NOSx3d8ZmcRzlwJMVXwKHqOaRvXDEy3SB8hRjj7U7zs3HTHBUWcqJniI" +
        "Cnmu0/LcittV1lE3yBZc8EGsQaqyPy9NLXg3aYAb4WFAevIEVPl18D4SmdirQ8tU2J3sH3ufdT9goYI9V0yWuqjNWv0D8pb4fN81" +
        "9tH4akLfPjv+oRjOEcY8KSyG8DLE0mGiWoJqP4Jk3/reIQdoLa5CyJS2oIaEy49HtN6BuGLXfYmeSB7538BYeppP26H1X+w/8rV+" +
        "r+Q3nvXM7rh+Y0ulWOSyHQdXqkbp1wDoKwqkfNvFEw41dg9B1UOmzCeP4paSFUiL7OGLFEbfI+YNK0m8CTbHRjAcefnXWfanvYx9" +
        "qVDW7ul+W8b+R9kqNoPEC6lKxRAZqh8/MwdCv5pKnbpsvGRoHgb/5roCRZE/5gCEBIKb19Q8xqoS2UjhCBs416DkqqH0DFsrbNJA" +
        "Fip2NUjsQf9M/cLa4bH2S/56yCWDsr8cJnE+sqWT+fDNcxc2OQRWPQAglB2cJCE/4NCQZ+Lc0gVE5erDFMt1MmxFuxlNyRZaE/wq" +
        "tGWlg0LU90Gbi2wWCPoVJ1r7W+QkQf2f8heh3AdTegU31eUgEoYb1Ct5iIHMPZY610jXhnqw28/2Owk0RPG86dkekfpYvpdFHPp8" +
        "rcByo74/g5p/B8b4wDjvLTES88dovprizz/gg4wMIDThcFagac6ppPOKZU/njS7M5WUuQXYqDL7pl2LRFAD5eUMXmUJpfwbostn5" +
        "iGO2Niv1hJZXumfIgdL3YvgnZwm93o6parZAcWJ2QqYfz2F5qTCRr9a0F4RVlqTJOYCoDRk3CJKwz8ckWKg4YSEygMdNH1HpOtUb" +
        "RaPFuIC9hXwUjIoOkaXPqxL8gFqXJt6ij5rhQTkI90vB29RVsYcltXJWNQFRSkLx1wYrCVLr7PzTsl3SLajnRQBWavrBiNxHBDHY" +
        "QgT+P0xXGTfQ1x2s8QQUBNsKD6O/mpccPMBSWU8qytD/2jNLU3bLk69zG5u/0EQv3y1kH2nY2bP5GhRddr3MziCTCWanlWILAnQQ" +
        "/Vu2rwJDiWuF7LcCAlTNMO/8EavoUsO4xkUsjG++GL2LEpP+R7O4d6AlQkxXURF/cUKFprz8UhwCJC++DOFijztysli/3PucHrU0" +
        "4NGGFgCYTIUgvsrTMNE5D2FEG56a2Rj0uprznTXzlfQ8qV89RrJA429ReIXvCr+3GmdTXgnXHkKyqMzUP5K0VpP/zBgzADdvC0/m" +
        "IcZPRQk2EIQ71v6U9YBfvT/lRPqfxTyZR/cZQnLF6LgbH+i1X4R9a4iQIeliEoCC5VqFdhcW90aahPMsXj/AubO4vVYcpOtckDIQ" +
        "tlPFKZb5iHvucH9RHz9eu2Nw/U0Ea9dYMpsYKBtBCzI8PRJATRaTEgIIGgHY2KgCE2IcoiUST/pggAT/xIgAT/xP3tYeHt+f318e" +
        "Vd2fX5/fHhna3p8fnx5dn6IcYtygoeJfGuScnB+dGh3sJiDdIORvICGhX5+eXyFhX6IgX57flBnjoWinX2DnNB/SAwIBAYEAgMlt" +
        "KosyN3nG58+Cv7xBTEn7h49GgrzloiUbS7ewbQgxm4L9b+eR552Jgpng0Ss6EEdi3L8AX7XC9dggPzGtfwHrfvReaWvWdmftV1Hl" +
        "/voXt8ExKALT7bSU+DPehO7Fr/Y2w96gxrAjbKq1d50/mAM2hkjjQq2AbWQnNb2HSwcPpnjdel4/bkgKcfBSJ3OvdUo4/wE5RsCC" +
        "lmMs2eDjjOqz0tu3WBG8sl0Vd2GrdJqdBC2gzDdPoPyyv6EPmoR/OQEJIUsKfR6YeEwI9JN4f4A6gUwyqLmDUSFQRR5ilPlcwjS9" +
        "4TzA8SwCYlKCNFWlWC80HjgbrHCn7NiJvA7ElaP2Hj43nAXjTV+XxzsPrU1Wge/XC6T4I7mb8NA9csJHlIipUwDCTR9c1Vd9G3mS" +
        "qlFooa8Lax1FIOB4uOz9GPy9qhgcP/sB27ajPsWKMcATYWIBfAvGQej963Az7Zk9bCWwktbqndRKCjpjeTqDpG28G+OUDV5Z2Shf" +
        "J7Q5LVM3xekV1A1oOOVW26ZrFkyhSkc7B/9R3QEJeUNbfzKz2+eWK4gEnA94nmfvHU+8LE8/foR8xgjn2GgEVGPAvTh7+7nTbTHu" +
        "Eth7UWduwJrSuFL7nhKK/TPfc0SvWBEi+JJ5QrKEekB4wQa48vxrT7mdxkKQja4rkBo1YT2ZJ19EglK5sWYiXUYr3CQgsczdpPvm" +
        "L58VeLOUBtCrxnyBHaBDfylLFCjYSJbK283oigHlmgW7wM+JJK5+UqnYRu0SjIiOKvaZBhdqSMAICMmHxuFrDoiO3Z8QiObZ8JAU" +
        "ZP0mgpc2LObQEsJYRO0w/DrcHqtybtOWRD+9629lW5T6ldmRbSZJ7TzHTEP6GonfxB9VBYcVlM13V4ntLLpWKytLCSLqF3StVzyr" +
        "NTijQq2JtIRTmRgA/6xSIbxC/MC4O9iILDBvBY4ZcZrym/JRexy6Sa79jwEfw7h6+RMNHXa8XEPkmYHASK+Lro+Ee0T1JFml2F7W" +
        "nb0tEk+CK7OgXJSCef2TbiTkEpLuD9O+pJx95Yae05lDSE0+ZavIAaDrjqVgK8DwPynw2PHJxFXtO97T6r8c23cAfbSISTiriLQF" +
        "mhiEdi8jPa6ktCxDsf6luOIbkYuLGS9s7YmcnXRk9LIOg+rsgHojKjR/hkcPkDUG6J/IToKLWK73rO+ZAt7zbBMD2OMIbsEbe9Ao" +
        "PB/QD6cm9lNxDjC8yC/YP4hDFvWbUVqWdmxtB4NwFvGee7zQd7q7E+iD4w7nWSBH45s6SoL16qhEYTAzjzC0hYJ6FqhLJWFgsu4I" +
        "8mu7SnZbnOUW/hLormnt39ASeA0IeJLR+zQ/mZPdAek8u3jrZsx+EnVEhS7ZUERfUqwm+rmfRwl/CU4eokiLm/LCWbzxnvk1Am5A" +
        "YU5AQG7yBH8Or7o5VN8T2ZUA8TVigY08To/5BcmQfvbaJeCxN8KCPFu5a3+a7+3rWrqqya6XfugfNh5OW3sR+SRXREndO13VcMo5" +
        "qW3wx8/5Cz0bD+Yll+Zg+vy5TCRxMnQacqIA5Tdtu3EAxxs7Vd6SD5MQLBzL5GJOjwQ1my89NtIrhXUnWKHWEER+DxlkxqqppEAH" +
        "Hz3oWvk5opgutS3BHavmY9fLmZffQMVylz23C97HOFkFECbBEcgCw7NA869uYXQlXk4R6VhbtrX5S34o883aU8qoEv8vS5J3oCQd" +
        "a0XddxqU3MifI1Tk+9KxnPWVSriUNqNCb8XNobQiKbS9l6KxdspG/5LlfUy+9W7zCF/CumRxXqTlWBbPmpgyQXJbFnxifaU5JWXN" +
        "49dH2g+Je+6U4g+szoz/EkbCa0bO+dNxRWQj+X2ZECgSqTD8vGgyrljLha5X2rCFnMWpD3iTswx3wrl43P4cA04Qs+zNCrqONJrw" +
        "qt+KfuVaTjZwGdx/H7DJRHQUU2dDYbFwXqQ1V1FhBsia5fAI4atHlWi6TWxZdugdyzsVBeTqX+y7UUCJe/lXntjizyEMo8BOBZkL" +
        "BSZrdytVCQdPSS3e6uHKKm7Dt6UO/Vi+h786VH/kxIboplfaQZVHc2I2ydoxgLtq3Ju53V3Mdte58QqBM0lDAj5UPD1my5HiIR1l" +
        "/pHew9Z24VyY85fMxM6bkpbagbglKgj+BdYdHR0IVRef5qa5aYbNXU9fRdBfQ9RhcdQtGN8+Ql7c+IJHolSDTVPPmV2djdSMq0ff" +
        "M0InX/yta3gm7a3prTW9AHtCD3WIJ8m+UpdH+BUCyVkdGwb1qJxwVm29Zi1xXBV4bYJ0mt8RYNRUDyoI2lgroVelg6BzAHBZPA6i" +
        "POYmsyNVNOP/HquixEZB08iBzbWunXQpm77C6M+vGPzwk3Dhrt9hxULhFDSvDnjg3YuaFdHBqoOnhqbqHoHyIPcZAWzmZhBesiMZ" +
        "xADO4IjCLMQEtYVU9zXXSyjuy6QtgI/2m0cCFgiVJOHIXplSedT//XC9Bvk5HB0tiFNOR301Ic9/E6ik5XJeV4m5BGpk/El+oKOu" +
        "EVCuz2D+y2l8VjCEB8h/pP2wio0DHg6BuvR0TCjeXZ3reO+eWDrX24YuJyhTnWY97NrnkaJbLA+nDJIZOdlIavA4h6IiBBlw6SMF" +
        "RgjjXD1ZXV91cmCWVt3eiA1tKypNgNuBR/WEXgvAaqai1FjFbpnaEDO/8mxLn2BTEKVU3BkcRT73WRd5SzJ57MkYIYQBmjkhyt9V" +
        "8h8Pn/ZXxg6APQ1mgi38gFCYC2uItYjgc6qN9z5diLfxBaG/+bR9AMon2pxfsUwZv3KC+J9X8IECM1RjRBKMdKrNty32ul83GqW+" +
        "AD1JLy6nGdIANCYz5THEPbZ4S5f7WDTLqc3uMBmEhQHWzkBiST0wFTHVu7uE2JjCm0PjB8dqxDEJhSHlt80bMsyKDbhZWAe1HKIg" +
        "D1iswvUszidyowNUdKQLOPFP4AuYcZvDwQ6Var6egR67g2Ow4go8bkHGcvzxYK+B6qTyu0QPp9UDjYqO2fJ1Kc5yqnnMNvhBFu6i" +
        "gTH/ZZ+Vu9o67TJKHqTnHPOVBbF65VRDP1G2jqhOhvOYpQmVy3S6wPluuBnxF5S+qIHQWmb8Ij8dFBedQ5LtJcTqq6auV4lgKuk+" +
        "hrb/z0bhwWzQVn5vKLrA3XvU3BPlmr7Q2ZzYcin1tDzM8S6r/tdoR8Y0ruvmBoCBBhh6VLmFsSdCf0yU+Jc1beFp2Dt34FMfqLy0" +
        "OiXZD8NU1reIbtQxKnZU2qGtOvu2UrPQzeLYRrbT9sHsBqWEtufhoBMCienXIvkUxswkLXfwWbJRcFnfztQHVF6/y25qsXu4xP/f" +
        "vKZlMfv6JiHefdPv5d/sGtA5ixxnf0Blc+WthOaZP0+Jez851bOzeC36KIheF8VWBkhcG4VlC2U98cjJn9Yv+8jmLoyryZm8Kyvo" +
        "AgtLpd/dUr0O7oWeh/VyfadmggjGJvaPABIHwRHKXPprHvQOp6dGRFhJ/H15tRX9Ce3y1yHoB9jRJfFHP5+DFDPbuZCnc9+J4FoS" +
        "9ho8n8Em0Qi6aRxV5lbZAGbSULGwMijfejrZ/0ky2Zik3RHGizFIwNY7PDiPx721N8vAUEP69hq8s8mSukMdu2EmrcJGgD7XfIVM" +
        "pipl/Md1mxCpKxHpVzAZDEH+DTt7CYqT8DM6mCHKAflBmwrkqMEtMrRNrKaDBY8GTrIeJOkLcHww3LlKX3aafclEmv6zRnmz5jgj" +
        "3gPiKbmrMjxYfI/r7DuB/8jKo2GR6D6wx69XdACXgJ7z+TF0ZnXPp5IOVIMpn+uAsfI2fNaZbE193rD6k8sofiBZxMtxCBfCe8KK" +
        "4YmjcAHpmJCU7f5RNmcOYM+Kz92cKaZtE0DN27fm23ork5zt2fKCjghx+NwpvFtv4mhGJcul8D9hGkkizbWzzUiIXWkNvXc/sPzY" +
        "OFygYrXD7/Hsqd0/+pVMfuMoVCjzKyY0KXTqt5mBuPl5mZAsSHA/vt2G3sz9f/3MCW1gMtkMMDIgFmTIRowAJotLiQkEDQD5oVwB" +
        "tNm1sYIdelF+vav9qEACn+IkACn+Je/u7k8Pz/APbsnub2+vz07M7G6vb49u7fBwjjFN77GQz61yrm4vTq0u9bNQbpAyV3AREO/w" +
        "Dm7xT4/Q0PAPcAqMUNF0E6/QVRmwaQGBAIDAgEDjC0wgwvMO2Ge8pO7GxkotMdflJEbZvqz1IF9g+ShYgzQxzjX9V7d5HmF6bpZV" +
        "Q9w+fYg5DELx9PdGYNr549tntk3eoDiLYEv8cglO2sDm/eKuXibLffvb+li8ZwEFteEd9AlZ63r42c0QZvEtzz3igIFZhqHX6pgh" +
        "M7VSFBpikut5LtNAGdcj+fmGFW5z1GSOHLtSCeDKjvSZnM7eXrFrpbUmcVFuwUWGPPUDcq5SuVmIHyxjY+IJJIyQVljl3T2cCMyT" +
        "CtaPHdCcuFo2F5LeJ4YOZvuhiybaaMltXcMW8wZDh8cHKMdBIQmcAUNNR5m4Wgx3FbI/nIznl69dAftZAkhk/Cbs/U74I8BhpzV1" +
        "ZlamHM+NYmxa5Y/HFn9rfytoYR/pEagdvGooE7RcSj7zmacmhrV7/vOTEwtlbwv8AppnZfkoBJjxUtoti1pmA3IU8Kj/uQZ9r7ib" +
        "eahR8dYkrgM40VwgKnoyT3B+NB1ItZYF+NYCmnCEB1MvSbwtDDTEggyFoL2XY6OOMo6DfRCKN2edh5GP+2fuw+5zfowEqCd0dQ1W" +
        "IvG0XLZD9tigzvc+3NbfPHJEfejT9aQQ5/pcefKBolU0FDL7PDUs43Cmkzh015hZk4d69VfCwsAqCanubUMKqn7BXPQzO3vy74Ue" +
        "KTp+fmD4ZL3d2K2A+60NkUBzfyOvfrgdtkkM7Bu4xqBVRKZQwdAcC8nd6ntU0IznYXEpvfZBqqjPkFfcOsUUlU+zkEIf3o7TYeYb" +
        "fRB8BghYOsvv/jD0JBnZFaxt91eyDmYjX5q0RnBnhpB1W94bmItFIOVMCZPJd1PrWzX5AjAzrPm708efzpgsbsBoAXRaNOAU9vjO" +
        "C4JZGJVoJbMLAblE7uPG9+xLNYVLuXdRJiRm0KuxKOchvdXJ1YTa0FiBF9vUM2FhfUEtrVkJpR7P3sjK6tLxJFITKhBboTfT0J2q" +
        "A4YfmjVocsQXANxNa0kf3lTJzgwFlCkTqiWdBth31ghfFZq2e6ga9h7D9lA7QC2jBfei3vp4YpT3VXeNwbwyswAlGquNULba3rgW" +
        "SDvOhNf5sFFSypHiGXSddl+u7q4I8Ik17OUFnRKimiWBYh9bQQAuM0mlMnKe9KTihc+iVi5MKAilSJLqMQFBsFrBFEfdS1tmTGj7" +
        "q0Y8h4alSxh8dgS6cugrdMMaCp007y0CDFcjDTgiBb1aeSh6lyJ7/CSNSlP0fIxCUUoZlH9x/sLPYlr2jEjmIYfSoiabSuca9eIh" +
        "ug5h0pYvs+lF+JU0iHN2mUgEWRvUZMIV+J1QzPYClbgjBo5TPi1/H3uybxQNNDG8LqEUrRKTnl5QCJT7xmWhG2DMbar85aqmF7HV" +
        "MIzh+PnB7UKoQ332DumODs6+bwY3OJhnC0J+qOlQyoKyjkeGolO8OVOILzwTqGlTgTnlXeJtKh/XhH0qgBFzCEz571dvzc9Cn3mo" +
        "vDmRPQrsd372IS3OQ6dPj8eiGVfL8c+veeazygnHJGvsRykF610Hu6F2OpT1SO9sR5Vl5ygYIyKel28H3Uc9iehQI0kMmByc+nFV" +
        "yQri0menSmJ+2bw9VFj2EawGAiV46G0jfMCRhs9+bws15WY+IRCs4v380jheqNPfVb9dHjvrIlLRd5T7whwa5Aaw3mzoR1dANIsV" +
        "9FrBq0zdFkAke44RhWKClgog26VV4f0ftAgjx2x89WoX1IXvmmAss5z/h8sKk5Zusnc99cEDxWCb0LXJNpHoqFbhZvcJ3OUDbvTC" +
        "2l8hR5Di8vFkWJ3TyZk5c+8nL6zlvmkwqxFgfX8VxRmoFjWkrxl2Yst3lQeT/tXuw/AVbPzbni///duB7E0a1390CwqibR6AUeMs" +
        "mtMAYkhysDsodxIEIZwk3D5e+gW0EsoqFqG2frPAV0TkNGkcKTN5zHOm9fAH3zzuMuxXjDoXwhkvS1V0TsoSJGvHIqtlHFpn/KlM" +
        "7DCHaJi8chBWhc7LA3RnmyWsFZK1xE/O2dn1aoZABAAAGgAAAAAAAAASADK7CDA0EB7EyEaMICarS4kACA8AhtZIAV/9VEACn+JE" +
        "ACv+JfAObw8PsBBPjypuj2+v748NTS7vb8+PDnAQrjGOEDExD83x7g5PTszOVpOv7vBSV5AQsa+PbxAQMRCPsBFPjqnMsLFUE3Bv" +
        "tVnQCQGBAIDAgEDNDBbIYVib1I2NEURBGIek24JqbK/VjsJThAeUQNH5l+ENsYE2bvkituNcDCf1XQxQCXbsSjOhAP29tJfUPD+G" +
        "NDY+RXPFSCG+LzS7q9uLSh/cSfPEomIvytkn4auH6Vh+JnSLXy7gjdhd7XWuIQl2khHX4lz8nFvMgKUYn3LjcE3cVMQdg0C/ACj/" +
        "rNinUBt2GIKoJ7DN6nPrNBkTSTcBM6Wj43D4scsVSrOjGlnsCBl27TLkwrWa2NgNzxjFXA3Qw2f+CB8aivLxRkmIv0lha3qZAoQo" +
        "Qb7IrWLzBnwXFIfYcm7zBjL2cTPsgeaKzSrY+v9zaRetVT1QimaPVgO89ZufXAfquE/7C2LBmHH3vZhQ1cNEhhY2cac16x9IJAdO" +
        "HbwYqHV//C/4jd5dpO/JcGwawHb7AIEdEc0EihWsvXXkbMcN9sBjsSoMpr5K0Sk8s5ww6sVY2tYsAiqg5OmX9fqCxU95XEd9WMn8" +
        "XOl72GNi+1P3w7RN85XwJVNCD/XmWTvPUbNAFFLWCFFOIi4J8oaFV99O1Kg2XussJJ4cYzojH+KFd8gt4u0RGGJ/MbVSmBD4puR0" +
        "RSZLg/DgJdN9y5YkS4qCIXfXeM/BJ2340nwFc9gVMZTwENRPk/Toq11nAXSEGq5S5WUpDtFe/+MmSzC7Huoon+UOoa4FG2uqsY/c" +
        "mVqkiJ4ogFZavRHkACwi2/hNXZwHPPCV28F89xP10eTSczibwFtKwK7XqVFUdU3BwABs1BrW2xqt/aGeJgxrPOmyYFcVpvWFlnpN" +
        "ebGnNH21Za3TLHGfY3MXQlff5okmFr7Ukpu9et8WJkjx49zP9DFkn3ajG/Wy6I7qFZsXQwqYtwthCiH2fFlIFKEes7ow4agIyRhS" +
        "izeE4IiTi+jFU3bFV/aCwD2zrLQUvwqEpdF+UVzSNdL+KL3WLv/XTcxcTHR1pYUoH7QtXRoOpCpctLVEwHW5Q414uCwhg1oogQQR" +
        "I99y/lFFOFrc5pgWfTgkMVGY6PMjW4t8GoA0ypIdHS7HUYbwEzyIdw8sjb+iiIELuDyBXD1pVXpipPu8dUkA0kjDOmzkU5UW/KYl" +
        "x4YnABJW7ImkoxqJ0aLTnCiR77e0Kj6/mmT2tZuDZm8i+b4ZedWl1FJS0qNvgpdfet2CCABS24HqqXjdNcop43WatS/YvO4rQfHm" +
        "NyL9o8ESU9tDRAPZMWAJrOfrrrQGJTDd2y1iMmmm91KKs7x5pnah5n7aHe4BEsKD24jPb4vjJNDL2psRRpnO/TXE6NUf1XypUeRk" +
        "wZVGqAUAAAAbAAAAAAAAABIAGgG4Sw0AABwAAAAAAAAAEgAyxBIoHQIHujQ9FUGJqwJCQEEDQCUC1ABeNl5sWP9aEACX+JEACP+J" +
        "e+Lru8P0BAPbivt7y+vzy5LjG6vb49uzbBQrhEuL/EQ723Srk3Prm0O9fMwrnCSVy/xETAu7y+wT4/QsJCQL0rNEXD0M0/vlRiwK" +
        "QGBAIDAgEMxHJ0iCyxHdJtnqaAYeYFAX+xTbDhHjVTYowm2vo013fMK8PxkgevoKU4GLtAwS1cT9fqiEb+nR/rgCVQPc4eQ0RecV" +
        "eWx1tuEsYvXZu+8YDFjkwbMVwDgyneG9PlV4Zn1VImG0owYXmQtUc5RXWjPw6jkDWnsQKPhEs2hLcHuxpUyKb8iYRrs4sqhdPHFF" +
        "JS+oazPq51qYd2+kosnoED03gFh6ygXCr/dCuXh752AoQTp218VhFE27XQYCMq0Jd2gqOPhidLPchQ7PHsDGtTrkC/gwoddFyD/b" +
        "3wm1XjHLdQnkiykdYE4IbTdAWYLCljZH5grnF1fJdz/boYJQTQSk9EDdtKysCOcNYAvA/xOJer0syOtm8U/JzoaL1Z5A+U0achjh" +
        "zws3nkzRBbZntODvgY5RiiSv8juRYISlZFCZYa0BMi++YVYCKO38pe5gHjgEBV4wiKY4iLVfwQ/Zs7k0lbXlvH9GQCcVRN66u3oW" +
        "7cgsfO2aS2SPve1+uBnGJZMkW+EUfOkNcSo0DJJA4A1mZnWqSigu75FtrPCeMCgkxw4BVB5QiNUHVnToAnuz3a33SgO9+0WRAlcU" +
        "0yHfq5tw+Vmxdkma0psL0sbu6zvpJ+eWyb6w905WcjdQUD8UxKh65iQDtxsie8OZd2fuIRknnh+RB0sk7rtiTj0i4vwquNmND7Dw" +
        "fysdhEadbkarOpqyaoH9EF+VSboaN+bCt7feSVyyaihw1B4JyHeRX2YR6FGfDgwR690UaPLSKRvDgODb4cVD6B5eVFg2QpwRh9RN" +
        "OnqP2jdUJgBgzRmkV8d+xiJ4nbgxKTosuJsW1gpJajz8ASFGsdwydll/DNT/SWJsl7fD9WtNFRstZotx/RWVkGU1My2uWaFVWgfH" +
        "rnCBessesfCGCARVQ+oQ6VcAGP/dGwM0j4fnhX1zJot/+VaSI9gxaT3TGustjlH9n7/BMci9Y9DxmnN+I9J3ZrYvb6pr87qV2YS5" +
        "JVHXYPZgKJp+TARpfYxeWK13FhEYSe4b2dDfo8LDKS0vOW+5OVQSEiT82zgUg8h4pM+NgBaH0DeutNfnbyJZ/YdiRnY2USQY5QrL" +
        "g3CTEZXzCF2oveup0KLcvhYF57w8O9ybClrU8tmpUREb3X5GBPQ6Rw/X/slD1vl+aIjVbuKDbJynO32v/OoAj51BdsXPsUZ+moaY" +
        "bIQkwddt5yrQd8bMNBwojvJck/QPbXZVfBYCBgQQYybin9HdzIAR+ZLobC9IT37ohdp5T07DYdiT4qq2p54JT7r4ZXpBD0WsXM1H" +
        "mVV1cdZyMVZqYnu9peZIvGa5rKDxcEUBH50gouRIW9NW6vBE9qbFMbAUwPcHkmeBnvXNJ3z62CGc3p/NpGXnsjN159BiJCS/ZVgF" +
        "DcsqwMhD0qFVNJ9UHlIO6A10wFWndXPBC+yMk4FlNv6Zlh9e8HTNkamGBYlVUmnjtWSWfVgyHJk67H6X3s3Wz1hPz5956AXcXShy" +
        "5Dhe4vwi4YACppe3k5yRwJDlwEnjVaV9TGHJPeBnQzIVET4P6QWvXzkEtvb6zTqbXVtQ6tQ8siEVPgqO0BgHwQScXn/RNNXErVod" +
        "ec8KDbgY7wu5Qgy1ybAkMvEkYaQ8xqw5RSMFVEsTbLCivcsoF16AJ2seLnwRzOpW/QTsvq9ffVT5NtRarDshdlprw5BX1QU6eLi6" +
        "q+5o40MYGrdIk7ss5ckN5+3YZWfepR6VEUAs2N4g75Qn9XQXfJto8pzjt7uL7gdpbe0xpaWp3EBgvRsI+w5tM/0bKky508lwbgvY" +
        "kfpEnr63pkNoCSqx39srQWmYyaqLlOAwZKDz9JpZj2AaOaIZxms5kKHqAA+z4vG6foYim7H/jbMRiaA5qbI9fJpHlbaBsLv6imBS" +
        "snAUKYuxlMyZLyAObvniKQV8ii7AjCmZhwE1+OXQ0Z+pth4Mgvmz9KJ3dMQpMUp6epnKf7SBHDNdIDKPstiuaG6cJUGj2kmzaJ2q" +
        "Jtd94+Ah5feIMTfMLLorpsw43Qp0XEyGuGcn+YZFAHITboVl20l2MIBJfBsUwEKpCpK6tE7d9ZXQJbNGgC2QN59D3kVPJgCQPAXv" +
        "Xji0whobWKsIjIyNl/I9Vfxwptkysw6Lrc9KQXtto2JxdboNfxp29qG6EpmzG4REVv8sIJuw+yt9d3UIzwRAcZqCP6zxMBIeSN1/" +
        "Et9Gj5fB9PiA6lpYQv/s4XR0jLEHiwXAzXY4dstbKUESWFX7qMbesfDIWBRh2Cov7L7p97E2fhHnXvknFOyD5ur3aNVxduVR5OGF" +
        "d9tbpKi2zqUa2W9oVWj1w5lrM5pgZsmCNX5ztfKkkXNNWB3dlJzim68DPKgPgUV0y8666fOe60Yaeio9RoxS+5FXfgUhkzJK8/Xa" +
        "NHIFn7lyZ2fPfDKJ8XvXR4HGoPZoFCJw02N25kPNjByh+IxGmMFC5FPwYEBAkA0ejbG8vW76HNZHcRKhBKoPtvB5RRAHgK1CV99z" +
        "YSV4o4rZiAzCRWr1lP7XHRZeim8GrgQ5FQWiaJXlJNFG8ORrS2RQkkjhiR0/CQggarwu6ek5dGTdQln1VruGKI65UUSgWxgg2lQA" +
        "8WmgCLKaOG4O+X7flJtFpELG6itCojZMKVZ5I28pyIFwywDP/LKgNTuOBYnA6QVY4D0+29vrgKUg4Mu2VmkIONgdZAiZm7qvwYTr" +
        "0dh3nwzw58zk/DRHYwMMbaQ/gf0XfjwEN0H72VPwOh+oMRxRyTzwNR+xkwjBhJWJla1e4kXqFEbkehbEp+CT3yrDw62hFxZFusVY" +
        "n/qiD8JObWnMnsnNKEV8LzSCrHlOxR+v/+ttTfw4nPVetAqf+jvYeCv////+8LA//lJew2bLfffWdbEMkzDQUF8bgAAAAABbF4hh" +
        "DZiXlYJeKadjz7tI+2+d++Eqf9aLjkAuFr8SLEPqpP/2qHpLxEwpsyaKA0XVlbRYKTMms0/X6WnpUp61w7imuNOhCkQKtFey7/DT" +
        "eBcF/xRy4ptKgDL/BzA4SA91CEaMYC6ECQkABAcAoUBIAXf9eEACf+JEACP+J++LTu8P0BAvjowOD0+v705r7I7Pb4+OzdAQrlFO" +
        "b/DxD61yLk4P7o0PFhMQzlCSVzAQ8LCPDw/wb++wsQ/QD2us0dB0E4/OtJlQKQGBAIDAgEDfwpeZ/OWR5tptl+hkG4upRhVCK65C" +
        "0RekAXYDPoWdGKOfFUGEZry29MLpeH3ACZUFuApJyL04zqOaRJ1moaseD9UTo6iTplYQrz9jMwlncI+O/91B7tDT9sZN0M9oBN4x" +
        "M4KAZ0pd1Ta5lcxuMxjLcNDemu6RJLftG5QvnskwLYnxKUxFEUE1oUqFJQrEcIqOz9XFWq3JpTL6DohlLyrMPqzB8ufrkjjCYrrX" +
        "LtukYn8psNoEP1lFneu4HR2EYZZI3YCRrqnFDmdHN9fFf3kE4pN/MPM8p9wpiCOaXikt1sJRxuvffwsFfKuI6j4HjOhlZPp3BHOS" +
        "YqT6wBmN3TG3O0cAaQtODke/A+MvQlEDH1BBK7xvcPo/MpiqyjpDqjG2zsS6bZzrFOdvQGAAjBxD0Y7HwyMlu9v+kbxQgmhRu6tK" +
        "MZig0pORnLTgtAGqV5GYeEsfewUeIoNhghL38p26wEM7ILGw9fppC6rIfn0PNJBRQzu8pa/Op6DeKHaJyk62devXRBPETVzvwAE1" +
        "fFmIutPNOibzhqmWIMnl61LDz9wdutQk7i3dqQR3igprDG6EA51H8nW0FPUy2HNYXDMKmXl5oysTSPUIbjYWYsUegF4KckNpn43p" +
        "/v4t0xq4mYAWHGgY3gQ5ELtzczTPqOc6CEUbLrwOmV4dV+C53PAiO5y1Rn2JnGkJ3D6HAgF/7UkrmeSYE0AfTh7pUCzqwaANKlZL" +
        "dlzUfM28UnkoH3lm+8f8Ku3jYYXKgX1/2ujsArx2qmdZp9Qdk1bXboDXuLkv8trhko9avcsk7fi2F7QZ4q8AQ1LLdTj3t+2b+iCX" +
        "98fhWvvp8k+a6UhBadhy6cRuNtS6DI6LIz9hJ++Xh1731E9ytpFK8kyifPV3WvDDYjUm+SPmvlle+bgGGXkjSUIG7tmnnC5MZItE" +
        "iNSeKq9dPnzM8Md00LGNYnNl4D+EV0Zpf+TPnMbjONbBBbnUztGochNq6lw6NHn3haQ0E6zgRgLdleZ8nneCwO/JrOH8260wUJAI" +
        "G6mZRNx6O396ikIVr17MlC3MhcKYZVG5LOTTutIE7PHF1Ve9W14vPqrPNHyT8Ryuts8qzIK5wouIL56vGIfJGUtaDnLVJDRWKjq3" +
        "HEjb03ang8V2nvE+t8VJ5R1QvIEmbOlLsgzdAtBTGc0aOcPNXXooFJ9Q/Cn7vKfCgyY4z5olrUrpz/k4xAUAAAAdAAAAAAAAABIA" +
        "GgHIzwMAAB4AAAAAAAAAEgAyygcwPGAStHBGjIAaw4sJAAQPAK51XAGU2ZWxeh1eUYrxm/2YQAI/4mQAI/4l72tO7zAQEA9t7C3v" +
        "T8/vLessbo+Pr46tj9DuEY4P8ZEvbfHubi8OzQ52UzBusFIXr9GRb88uzxCur5ERENAvSm0QsJRTkBB1mRAJAYEAgMCAQKo7mSxV" +
        "FY5TQ9tOxAInL8d6OzHfYpErvQfUqY9RZto2J3asyIr7XJulsFTmAwjc91cO0Cx//xlBJL/3WPyBQs9VOegyvTApttxzkteKXtCN" +
        "vDTdYVJ0mKIqq1TDk4Cb3K4Q6Tzj5Z1R7Gas8r9kg4MAfvo3SOC6ZkPChS8Z4Y9Bk6fpl0VUXVxiatswZ3VtUP6OHLVncnYx/qr5" +
        "ZSYxBmd7sopcEC4nk5cfhyeuiXhtibSreRIf30Sgdn2cdNi/bmsFQLSrdCQ7qpmT3aHnxnaPNGrD5Me5HEgS18/xy3FHPTHoWSws" +
        "leCsaNiJnqVJtblcJHfLZ0iXPOyYzKw8sNqsjlSg0+IsNsle53zWuD3YVN8kstRmW30otGkquF5VcR8yhC7fAhQFwT0L826SKTx0" +
        "RrAzaU5ykZBREA3XCFKdXrFAOTJME5QMDmtEGjdbUjNv5xI5vqNgepngou3MenS54+3azyJBiH8Us586/3mv4gX7ULN1Wno4U7wx" +
        "hgMN3B0GCx2XxX6rBeyIDAict9SJnyuotJ6JzG5DGKu7dMWimdrngNlGMzS/fS/x+30UONp5n2IN+Ww051XigMwBjOrCwIaEGfcC" +
        "t05wrWOL7fVbKq1s+NNCixCOjdjbhuKBZlBsqj0AF4eUo3f9/v6t5fGM6TDruMk1vljzMRQOiRjrJkpZoC50yIxFsobXqXbiFIs7" +
        "118aCQUWta+zhnTid6a4c5oGO5bTAExxdlc4GNfRbzc8OwWAHtlQWkqkFQOsTu2y11d54k0Ym5dixO4cs7gc9lQz0x6RKBbQWe3q" +
        "x8jepoaS34d/A8jc0s4WwBhhVt7aqT8H8a/rS3QS7jEVg4NUY9x+dNYx3NHcohrFky3Qzu9oBcZCLn62cwpeWWUjoZuaA0wE6PXu" +
        "JhoAlLrarBM2P9Xwrny+sR023iq702HMxjfiq7O9oZm1l4i/pTXiOxSFd+KnEF3nkMjj4XGGLebgCJEu6JCsGYVOscgCTNdkkAgC" +
        "DVYe8MKcAAGYtncKuuYi8dh7p530bC8peXxAAx0PkgnGwDknHUxbWMK2eMd4yy1326MPR+3lnvNNmTpBM4UiQqpP6g9UEQsg5mOF" +
        "0I4BzEmptFGSFSF3ATqwBQAAAB8AAAAAAAAAEgAaAYg=";

    private static readonly string[] TenBitGrainClipDigests = [
        "8070e863f1bad4b5c7527e44aedc6054b82918163794d2fccf31c235ed818a1f",
        "420ecfd3bc7685171a260743833baaa132e710c95eacf07d78491b58c65daffa",
        "1bff2f221eccf782eb289e94a3c3ff94248c79d37f949cdd9d78eb00dcd30e3c",
        "de21c813b83f2a0ad7b6d628e53e9031abb08b102007e1772ccec8c4a7fed449",
        "9bec4a1c760a27898afc3c4dd47fb18d73111ad9b5582cac1838f851a249380f",
        "adb89fe11f9fc7a3946af9c07469da8c2e2cae6b998fffaecae7e3a071280e54",
        "d1638385d5748149d04543331b7d0e95095e65fe95629e442dee2c9c9fcc56a0",
        "b939080fa59f331d4f0a8abbf8d90c415c938c0e9a35b149e8903f37b90934ab",
        "2bbdbceac3f4d13b2049c914c7d3ffae568c3ee264d89d15523aec505fbf05ac",
        "685a6a95392ba802f0667cc62c82b6d00bc6efd291ab8092e6dfc393003fe7ac",
        "5bc652a71634fa7a5bb37659637a335402c83ad888dbcca5d1a6dd7ddfe8f18b",
        "18f9ca56919df67adca0bd701b830b5d55ba8a2343f5b9616763dba5a867574b",
        "396fb90194a40c6059292a1a0bc459b55cca204f7b71b08a1bfcdae1595db359",
        "8d987914500b51e7f6cf5e69e7070170c988d795e1fa93138fe70b77c4d69617",
        "1158bdd810ffba6f8fff4e33738f52aa271c3fccb3c6847b28785fdb75cad524",
        "91e50f1239a67878cfb442733116bfae090827638c00f7d3c71034befefc0e46",
        "bb5340e32e2a88727475c04dae6a39169d99cbdb46836a4781333cf133e5f521",
        "01e669cf887a21db03c1d1fae273c8210f17a9d426e0544bd3ed3e642e8f9687",
        "9b57a8c7e34081e196af66f08444c6744488aafb2d8a61f22fc1b0e5b911e39f",
        "2034d5b6e34401f38270e3008356ffa11af75c6fe770d19519af78dcc64c76f0",
        "0905daf46a6f1ff647d301c3ce568d82583b4fb6c3bf887711645b4fd376df7b",
        "db781280f5e58706b315aa2a2e490f4cf446e92d54aa87662f50344aa257b1c9",
        "747e0d78b59528e24e51aa6d68b1ed27fc64a5dbb8343d84e4f8556216a0140a",
        "6f34c3aae1148a2454d336fd08c8d917e46439e640f3cc6ec2cc771bef4678b1",
        "382669ee6af87c4e20a66d71c1e2ca504ccf6a5d3cfd3eaf2de51d86903efea0",
        "e147815d18d9e4d782514fcbd0b1f731f83dd2411ea92ccf3d4643ac41ea8975",
        "8151fc722bda33f01948c685cd2d46dfefdda895341a8b864af0269c02180e23",
        "4857b41d41bfbfbe6101cacc3812acf41cda4ad4a395987927afbf7fae05ce8e",
        "34a23521d2ab635421782418d46e9ecd98bee82e0cf041778d319281b1fa3601",
        "b77bd9e14d04249ace6b373580e9d612b3cfa83c4d4e824d416a056af9e9bcc3",
        "dc7102f91c29539b9473ecc1161b749779dcae38ee68be39f66c2bdd51fbc755",
        "a2d135d168743d140cba8d1d54a332de4a3041928f9549ca8b7f9b20f3f270f3",
    ];

    [Fact]
    public void DecodeDisplayFrames_ChromaGrainClip_MatchesDav1dExactly()
        => DecodeAndCompare(ChromaGrainClipIvfBase64, ChromaGrainClipDigests, bytesPerSample: 1);

    [Fact]
    public void DecodeDisplayFrames_TenBitGrainClip_MatchesDav1dExactly()
        => DecodeAndCompare(TenBitGrainClipIvfBase64, TenBitGrainClipDigests, bytesPerSample: 2);

    private static void DecodeAndCompare(string ivfBase64, string[] frameDigests, int bytesPerSample)
    {
        using MemoryStream stream = new(Convert.FromBase64String(ivfBase64));
        List<Av1DisplayFrame> frames = Av1DecoderCore.DecodeDisplayFrames(stream);

        Assert.Equal(frameDigests.Length, frames.Count);
        for (int i = 0; i < frames.Count; i++)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendCropped(hash, frames[i].Luma, bytesPerSample);
            AppendCropped(hash, frames[i].ChromaU, bytesPerSample);
            AppendCropped(hash, frames[i].ChromaV, bytesPerSample);
            string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            Assert.True(frameDigests[i] == digest, $"frame {i}: plane digest mismatch");
        }
    }

    private static void AppendCropped(IncrementalHash hash, Av1Plane plane, int bytesPerSample)
    {
        byte[] row = new byte[plane.CropWidth * bytesPerSample];
        for (int y = 0; y < plane.CropHeight; y++)
        {
            for (int x = 0; x < plane.CropWidth; x++)
            {
                ushort v = plane.Samples[(y * plane.Width) + x];
                row[x * bytesPerSample] = (byte)v;
                if (bytesPerSample == 2)
                {
                    row[(x * 2) + 1] = (byte)(v >> 8);
                }
            }

            hash.AppendData(row);
        }
    }
}
