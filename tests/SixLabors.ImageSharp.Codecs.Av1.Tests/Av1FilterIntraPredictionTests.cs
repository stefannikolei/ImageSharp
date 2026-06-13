// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Av1.Prediction;

namespace SixLabors.ImageSharp.Codecs.Av1.Tests;

/// <summary>
/// Validates the recursive filter-intra predictor against dav1d 1.4.1's <c>ipred_filter</c> for random
/// reference edges, covering all five tap sets across the supported block sizes.
/// </summary>
public class Av1FilterIntraPredictionTests
{
    [Theory]
    [InlineData(4, 0, 68, "III8/Q==", "MPkOxw==", "H1s4sry6kcMjRkl5n6CPkA==")]
    [InlineData(8, 2, 28, "Liu4Vp2AbBI=", "uu6jwthUWng=", "w8L/1/vs4rXz8v/9////7KalrKusrKyjw8PGxsfHx8LZ2dra29vb2FRUVVVVVVVUWlpbW1tbW1t4eHh4eHh4eA==")]
    [InlineData(16, 4, 121, "Qr3yIQbwhHdi8PPLTXZNxw==", "Rf1vhN+a18Wz0HasDo9Tpw==", "G46/DADGaV9Qz9OyR25MuMP//4Zl/6+fiu7u03SJcMNOlJ08KLh3bmK/wq5gd2OuaKCiV0S7gHxxv7+0cX5xrbvh3ZZ+3qafkdHPw4eQhLeCqK92ZLiPi4G5u7WDiIOsutTWoYzQqaOXxca/kpWQs67Dy5yKw6aflby+upSUlK+htr6Wh7qhm5O1t7SUlJWtvcrQrJ3Erqifu7y6npudr3GHknhxmoqIhKGkpI6OkqOir7KbkrCfnJetra6bmJuoGThFOj5lXF9ifYGGen2CkIuYk4WBmomKiZuZnZGQkpxXamlhYnxwc3WIh4yEhYiSoaifko2ej46NmpeakZCSmQ==")]
    [InlineData(8, 3, 120, "mzTK9U8uIgo=", "hm0NhYtjVJ4=", "kmKcwYJfRC59bYWjj3hlTDlMY4KEfXFhbmFjdHp7dWt9cWxxdXh2cWtsbG5xdHRyXmNmam1wcnKHeXNycXFxcQ==")]
    public void Predict_MatchesDav1d(int size, int filt, int topLeft, string aboveBase64, string leftBase64, string expectedBase64)
    {
        byte[] above = Convert.FromBase64String(aboveBase64);
        byte[] left = Convert.FromBase64String(leftBase64);
        byte[] expected = Convert.FromBase64String(expectedBase64);

        byte[] dst = new byte[size * size];
        Av1FilterIntraPrediction.Predict(above, left, (byte)topLeft, size, filt, dst);

        Assert.Equal(expected, dst);
    }
}
