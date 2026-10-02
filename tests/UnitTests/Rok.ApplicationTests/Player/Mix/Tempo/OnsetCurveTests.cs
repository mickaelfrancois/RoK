using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class OnsetCurveTests
{
    private const int Rate = 11025;

    [Fact(DisplayName = "onset_curve_is_empty_for_a_signal_shorter_than_a_frame")]
    public void Compute_ReturnsEmpty_WhenSignalTooShort()
    {
        // Act
        var curve = OnsetCurve.Compute(new float[100], Rate);

        // Assert
        Assert.Empty(curve.Values);
    }

    [Fact(DisplayName = "onset_curve_is_flat_for_silence")]
    public void Compute_IsZero_ForSilence()
    {
        // Act
        var curve = OnsetCurve.Compute(new float[Rate * 3], Rate);

        // Assert
        Assert.NotEmpty(curve.Values);
        Assert.All(curve.Values, v => Assert.Equal(0f, v));
    }

    [Fact(DisplayName = "onset_curve_frames_are_dated_at_their_centre")]
    public void TimeOf_UsesFrameCentre()
    {
        // Act
        var curve = OnsetCurve.Compute(new float[Rate], Rate);

        // Assert
        Assert.Equal(128.0 / Rate, curve.TimeOf(0), 1e-9);
        Assert.Equal(64.0 / Rate, curve.HopSeconds, 1e-9);
        Assert.Equal((128.0 + (10 * 64)) / Rate, curve.TimeOf(10), 1e-9);
    }

    [Fact(DisplayName = "onset_curve_peaks_at_the_click_times")]
    public void Compute_PeaksAtClicks()
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(120, 10, Rate, 0.137);

        // Act
        var curve = OnsetCurve.Compute(clicks, Rate);

        // Assert
        for (var n = 2; n < 15; n++)
        {
            var expected = 0.137 + (n * 0.5);
            var window = Enumerable.Range(0, curve.Values.Length)
                .Where(i => Math.Abs(curve.TimeOf(i) - expected) < 0.05)
                .ToArray();
            var best = window.MaxBy(i => curve.Values[i]);

            Assert.True(Math.Abs(curve.TimeOf(best) - expected) < 0.02, $"click {n} peak at {curve.TimeOf(best)}, expected {expected}");
        }
    }

    [Fact(DisplayName = "value_at_interpolates_and_is_zero_out_of_range")]
    public void ValueAt_InterpolatesAndIsZeroOutOfRange()
    {
        // Arrange
        var curve = new OnsetCurve([0f, 2f, 4f, 0f], 0.1, 0.5);

        // Act & Assert
        Assert.Equal(2.0, curve.ValueAt(0.6), 1e-9);
        Assert.Equal(3.0, curve.ValueAt(0.65), 1e-9);
        Assert.Equal(0.0, curve.ValueAt(0.49));
        Assert.Equal(0.0, curve.ValueAt(0.81));
        Assert.Equal(0.0, new OnsetCurve([], 0.1, 0.5).ValueAt(0.5));
    }

    [Fact(DisplayName = "mean_between_averages_the_frames_inside_the_range")]
    public void MeanBetween_AveragesFramesInRange()
    {
        // Arrange
        var curve = new OnsetCurve([1f, 2f, 3f, 4f], 0.1, 0.5);

        // Act & Assert
        Assert.Equal(2.5, curve.MeanBetween(0.55, 0.75), 1e-9);
        Assert.Equal(2.5, curve.MeanBetween(0, 10), 1e-9);
        Assert.Equal(0.0, curve.MeanBetween(5, 6));
        Assert.Equal(0.0, curve.MeanBetween(0.6, 0.6));
    }

    [Fact(DisplayName = "onset_curve_is_never_negative")]
    public void Compute_IsNeverNegative()
    {
        // Act
        var curve = OnsetCurve.Compute(SyntheticSignal.WhiteNoise(4, Rate), Rate);

        // Assert
        Assert.All(curve.Values, v => Assert.True(v >= 0));
    }
}