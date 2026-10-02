using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class BeatPhaseEstimatorTests
{
    private const int Rate = 11025;

    [Theory(DisplayName = "estimate_finds_the_first_click_within_twenty_milliseconds")]
    [InlineData(90, 0.137)]
    [InlineData(120, 0.137)]
    [InlineData(120, 0.41)]
    [InlineData(174, 0.2)]
    public void Estimate_FindsFirstBeat(double bpm, double firstClick)
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.Clicks(bpm, 30, Rate, firstClick), Rate);

        // Act
        var phase = BeatPhaseEstimator.Estimate(curve, bpm);

        // Assert
        Assert.NotNull(phase);
        Assert.InRange(phase.OffsetSeconds, firstClick - 0.02, firstClick + 0.02);
        Assert.True(phase.Confidence > 0.3);
    }

    [Fact(DisplayName = "estimate_returns_null_for_silence")]
    public void Estimate_ReturnsNull_ForSilence()
    {
        // Arrange
        var curve = OnsetCurve.Compute(new float[Rate * 10], Rate);

        // Act
        var phase = BeatPhaseEstimator.Estimate(curve, 120);

        // Assert
        Assert.Null(phase);
    }

    [Fact(DisplayName = "estimate_returns_null_for_an_invalid_tempo_or_empty_curve")]
    public void Estimate_ReturnsNull_ForInvalidInput()
    {
        // Arrange
        var clicks = OnsetCurve.Compute(SyntheticSignal.Clicks(120, 10, Rate), Rate);
        var empty = new OnsetCurve([], 0.0058, 0.01);

        // Act / Assert
        Assert.Null(BeatPhaseEstimator.Estimate(clicks, 0));
        Assert.Null(BeatPhaseEstimator.Estimate(empty, 120));
    }

    [Fact(DisplayName = "refine_tempo_corrects_a_small_tempo_error")]
    public void RefineTempo_CorrectsSmallError()
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.Clicks(120, 30, Rate, 0.137), Rate);

        // Act
        var refined = BeatPhaseEstimator.RefineTempo(curve, 121.5);

        // Assert
        Assert.InRange(refined, 119.5, 120.5);
    }
}