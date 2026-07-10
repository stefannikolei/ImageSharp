// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the deblocking loop-filter sample primitive against dav1d 1.4.1's <c>loop_filter</c>. Each
/// case filters one edge of a 16x16 plane (the edge is centred at row/column 8) and compares the result
/// against dav1d's own output, covering the narrow (width 4), flat-6, flat-8 and flat-16 branches in
/// both the vertical (dir 1) and horizontal (dir 0) orientations.
/// </summary>
public class Av1LoopFilterTests
{
    [Theory]
    [InlineData(
        4,
        1,
        60,
        20,
        4,
        "ZGNlYmJmYmRuam5ramptbWJjYmZlYmZia25qbm5tamtiZmNkZWNmYm5sbmtqbm5rZGJmYmZiZmNtbm1sbW5tbGRjY2NiZmRmbWxtbG5qam5lY2RjZWViYm5ubGxsbm1uZWJiZGViYmRubWxtbGptbGNmYmViY2Rja21tbWprbW1mZGNlZmRlZG1ra2pra2trYmVmY2RkYmNtbmxubmxrbmZiZWZlZWVlam1tamtqa21jYmRmYmJiZmtuamxuamprZmVjZGRmZGVqam1tbW1samNiZGRlY2Zia25sa25qbmxiZGZkY2RjZm5ubGtua2ttY2NmZWRiYmRtbGtubG1sbA==",
        "ZGNlYmJmYmRuam5ramptbWJjYmZlYmZia25qbm5tamtiZmNkZWNmYm5sbmtqbm5rZGJmYmZiZmNtbm1sbW5tbGRjY2NiZmRmbWxtbG5qam5lY2RjZWViYm5ubGxsbm1uZWJiZGViYmRubWxtbGptbGNmYmViY2Rja21tbWprbW1mZGNlZmRnZ2ppa2pra2trYmVmY2RkZGdpbGxubmxrbmZiZWZlZWZnaGxtamtqa21jYmRmYmJjaGltamxuamprZmVjZGRmZGVqam1tbW1samNiZGRlY2Zia25sa25qbmxiZGZkY2RjZm5ubGtua2ttY2NmZWRiYmRtbGtubG1sbA==")]
    [InlineData(
        6,
        0,
        40,
        20,
        8,
        "d3d3eHd5eXh4eXd5d3l5d3h5eHl5eHl4eXh3d3h4eHh4eXd5d3d3d3d4d3d5eXh5eXl3eHh5eXh5eHh4d3h5eXh5eXd4eHh5eXh5eHh4eXl5eXh4eXd4eXd5eHh4eHl5eXl5eXl5eHh5d3h5eHl5d3h5d3d5d3d5eXd4eXd5d3l6e3p6ent8enp7e3p6fHp6enp6enx6e3t6enx6fHx6e3x6enp6ent8fHx8ent7e3p7e3x8fHp7e3x8ent6enx8e3p6e3p8fHt7fHt6e3t7fHt8enx8enx6e3p6enp6e3x6fHx6enp8e3p7enx6fHx8e3t8e3t8enp6e3t6enx8eg==",
        "d3d3eHd5eXh4eXd5d3l5d3h5eHl5eHl4eXh3d3h4eHh4eXd5d3d3d3d4d3d5eXh5eXl3eHh5eXh5eHh4d3h5eXh5eXd4eHh5eXh5eHh4eXl5eXh4eXd4eXd5eHh4eHl5eXl5eXl5eHh5eHh5eHl5d3h5d3d5d3d5eXh5eXd5d3l6e3p6ent8enp5enp6fHp6enp6enx6e3t6eXt6fHx6e3x6enp6ent8fHx8ent7e3p7e3x8fHp7e3x8ent6enx8e3p6e3p8fHt7fHt6e3t7fHt8enx8enx6e3p6enp6e3x6fHx6enp8e3p7enx6fHx8e3t8e3t8enp6e3t6enx8eg==")]
    [InlineData(
        8,
        1,
        50,
        25,
        8,
        "T1FRT1BRUFFTUVNRUlJTUU9RUFFRUFBRUVFTUVNSU1FRT09RT1BPUFJTU1JTUlJTUVBPUE9PT1BRUlNSU1JSU1BRUFFRUFFPUlNRUlNTU1FRUFFRUU9RUVFTU1JSUVFSUVBPUE9QT09SUlJRUVNTUVBRUVBRUFFPUVJRUVFTU1FPUFBRUE9RT1JSUlFSUlJTUFFRUVFPUVFSUlNTU1FSUlBRUFFQT1BRUlFSU1NTUVFRUVBQUFFQUVNSU1JRU1FTT1BQUVBQUVFSUVJRUlJTUlBQT09RUVFPUlNRU1JRUlFRUFFRT09RUFJTUVJRUVJTUU9RUFBPT09TUVJTUVJSUw==",
        "T1FRT1BRUFFTUVNRUlJTUU9RUFFRUFBRUVFTUVNSU1FRT09RT1BPUFJTU1JTUlJTUVBPUE9PT1BRUlNSU1JSU1BRUFFRUFFPUlNRUlNTU1FRUFFRUU9RUVFTU1JSUVFSUVBPUE9QT09SUlJRUVNTUVBRUVBRUFFPUVJRUVFTU1FPUFBRUE9SUFFRUlFSUlJTUFFRUVFPUVFSUlNTU1FSUlBRUFFQT1BRUlFSU1NTUVFRUVBQUFFRUVJSUlJRU1FTT1BQUVBQUVFSUVJRUlJTUlBQT09RUVFPUlNRU1JRUlFRUFFRT09RUFJTUVJRUVJTUU9RUFBPT09TUVJTUVJSUw==")]
    [InlineData(
        16,
        0,
        60,
        28,
        8,
        "WVpZW1paWVlZWVpbWllZW1taWllZWllZW1paWVlaWltbWllbWltaW1lZWVpaWVtaWVpbW1pbWVpaW1paWllZWlpZWVlaW1pbW1taW1pZW1lZWllbW1paWVpaW1taW1tZWllZW1laW1tZWVpZWlpZWVpbWVpbW1pZWlpaWVtaW1tbXF1bXFxbXFxdW1xdW1xbXFtcXFxdW1xbXFtbW1tbXVtdXVtdW11cXVtcW1tdXFxdW1xbW1xcXFtbXFxbXVxbXFtbW1xcW11bW1xcW1xbXF1bXVxdXV1dW1xcW1xbXVtcXF1cXVxcXVtdXVxcXFxbXF1dW1tcXVtcXVtbXV1cXQ==",
        "WVpZW1paWVlZWVpbWllZW1taWllZWllZW1paWVlaWltbWllbWltaW1pZWVpaWVtaWVpbW1pbWVpaW1paWllZWlpZWVlaW1pbW1taW1pZW1lZWllbW1paWVtaWltaW1tZWllZW1laW1tbWlpaWlpZWVpbWVpbW1pZW1tbWltaW1tbXF1bXFxbXFtcW1tdW1xbXFtcXFxdW1xbW1taW1tbXVtdXVtdW11cW1tcW1tdXFxdW1xbW1xcXFtbXFxbXVxbXFtbW1xcW11bW1xcW1xbXF1bXVxdXV1dW1xcW1xbXVtcXF1cXVxcXVtdXVxcXFxbXF1dW1tcXVtcXVtbXV1cXQ==")]
    public void FilterEdge_MatchesDav1d(int wd, int dir, int e, int i, int h, string inputBase64, string expectedBase64)
    {
        ushort[] plane = Av1TestData.Widen(Convert.FromBase64String(inputBase64));
        ushort[] expected = Av1TestData.Widen(Convert.FromBase64String(expectedBase64));

        int strideA = dir == 0 ? 1 : 16;
        int strideB = dir == 0 ? 16 : 1;
        Av1LoopFilter.FilterEdge(plane, (8 * 16) + 8, strideA, strideB, e, i, h, wd);

        Assert.Equal(expected, plane);
    }
}
