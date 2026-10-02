using Rok.Application.Player.Mix;
using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class TempoEstimatorTests
{
    private const int Rate = 11025;

    [Theory(DisplayName = "estimate_finds_the_click_tempo_within_one_percent")]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(174)]
    public void Estimate_FindsTempo(double bpm)
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.Clicks(bpm, 30, Rate, 0.137), Rate);

        // Act
        var estimate = TempoEstimator.Estimate(curve);

        // Assert
        Assert.NotNull(estimate);
        Assert.InRange(estimate.Bpm, bpm * 0.99, bpm * 1.01);
        Assert.True(estimate.Confidence >= MixThresholds.MinBeatConfidence);
    }

    [Theory(DisplayName = "estimate_folds_extreme_tempos_into_the_usual_range")]
    [InlineData(60)]
    [InlineData(240)]
    public void Estimate_FoldsTempo(double bpm)
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.Clicks(bpm, 30, Rate, 0.137), Rate);

        // Act
        var estimate = TempoEstimator.Estimate(curve);

        // Assert
        Assert.NotNull(estimate);
        Assert.InRange(estimate.Bpm, 120 * 0.99, 120 * 1.01);
    }

    [Fact(DisplayName = "estimate_has_low_confidence_for_white_noise")]
    public void Estimate_LowConfidence_ForNoise()
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.WhiteNoise(30, Rate), Rate);

        // Act
        var estimate = TempoEstimator.Estimate(curve);

        // Assert
        Assert.True(estimate is null || estimate.Confidence < MixThresholds.MinBeatConfidence);
    }

    [Fact(DisplayName = "estimate_returns_null_for_silence")]
    public void Estimate_ReturnsNull_ForSilence()
    {
        // Arrange
        var curve = OnsetCurve.Compute(new float[Rate * 30], Rate);

        // Act
        var estimate = TempoEstimator.Estimate(curve);

        // Assert
        Assert.Null(estimate);
    }

    [Fact(DisplayName = "estimate_returns_null_for_a_curve_too_short_for_the_slowest_tempo")]
    public void Estimate_ReturnsNull_ForShortCurve()
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.Clicks(120, 2, Rate), Rate);

        // Act
        var estimate = TempoEstimator.Estimate(curve);

        // Assert
        Assert.Null(estimate);
    }

    [Theory(DisplayName = "fold_brings_a_tempo_into_the_usual_range")]
    [InlineData(240, 120)]
    [InlineData(35, 70)]
    [InlineData(150, 150)]
    [InlineData(200, 100)]
    public void Fold_BringsIntoRange(double bpm, double expected)
    {
        // Act
        var folded = TempoEstimator.Fold(bpm);

        // Assert
        Assert.Equal(expected, folded, 1e-9);
    }
}