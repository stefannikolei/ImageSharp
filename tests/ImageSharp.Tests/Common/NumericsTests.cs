// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Common;

public class NumericsTests
{
    private static readonly int[] NormalizeSpanLengthValues =
    [
        0,
        1,
        3,
        4,
        5,
        7,
        8,
        9,
        15,
        16,
        17,
        31,
        32,
        33,
        63,
        64,
        65,
        127,
        128,
        129,
        2048
    ];

    private ITestOutputHelper Output { get; }

    public NumericsTests(ITestOutputHelper output) => this.Output = output;

    public static TheoryData<int> IsOutOfRangeTestData = new() { int.MinValue, -1, 0, 1, 6, 7, 8, 91, 92, 93, int.MaxValue };

    /// <summary>
    /// Gets lengths that exercise scalar execution and the supported SIMD widths.
    /// </summary>
    public static TheoryData<int> NormalizeSpanLengths => new(NormalizeSpanLengthValues);

    private static uint DivideCeil_ReferenceImplementation(uint value, uint divisor) => (uint)MathF.Ceiling((float)value / divisor);

    [Fact]
    public void DivideCeil_DivideZero()
    {
        uint expected = 0;
        uint actual = Numerics.DivideCeil(0, 100);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 100)]
    public void DivideCeil_RandomValues(int seed, int count)
    {
        Random rng = new(seed);
        for (int i = 0; i < count; i++)
        {
            uint value = (uint)rng.Next();
            uint divisor = (uint)rng.Next();

            uint expected = DivideCeil_ReferenceImplementation(value, divisor);
            uint actual = Numerics.DivideCeil(value, divisor);

            Assert.True(expected == actual, $"Expected: {expected}\nActual: {actual}\n{value} / {divisor} = {expected}");
        }
    }

    private static bool IsOutOfRange_ReferenceImplementation(int value, int min, int max) => value < min || value > max;

    [Theory]
    [MemberData(nameof(IsOutOfRangeTestData))]
    public void IsOutOfRange(int value)
    {
        const int min = 7;
        const int max = 92;

        bool expected = IsOutOfRange_ReferenceImplementation(value, min, max);
        bool actual = Numerics.IsOutOfRange(value, min, max);

        Assert.True(expected == actual, $"IsOutOfRange({value}, {min}, {max})");
    }

    /// <summary>
    /// Verifies that normalization divides every element by the supplied sum.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(NormalizeSpanLengths))]
    public void NormalizeMatchesScalarFormula(int length)
    {
        float[] actual = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = (i + 1) * 0.125F;
            expected[i] = actual[i] / 7.5F;
        }

        Numerics.Normalize(actual, 7.5F);

        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(expected[i]), BitConverter.SingleToInt32Bits(actual[i]));
        }
    }
}
