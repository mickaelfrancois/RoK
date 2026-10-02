using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class DownbeatEstimatorTests
{
    private const int Rate = 11025;
    private const double Bpm = 120;
    private const double AccentedClick = 0.137;

    [Theory(DisplayName = "downbeat_of_an_accented_4_4_click_track_is_found_within_20_ms")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Estimate_FindsTheAccentedBeat(int firstBeatIndex)
    {
        // Arrange
        var period = 60.0 / Bpm;
        var curve = OnsetCurve.Compute(SyntheticSignal.AccentedClicks(Bpm, 30, Rate, AccentedClick, 4, 3.0), Rate);
        var firstBeat = AccentedClick + (firstBeatIndex * period);
        var expected = firstBeatIndex == 0 ? AccentedClick : AccentedClick + (4 * period);

        // Act
        var downbeat = DownbeatEstimator.Estimate(curve, 0, Bpm, firstBeat);

        // Assert
        Assert.NotNull(downbeat);
        Assert.Equal(expected, downbeat.Value, 0.020);
    }

    [Fact(DisplayName = "downbeat_is_dated_in_absolute_track_time")]
    public void Estimate_UsesAbsoluteTime()
    {
        // Arrange
        var curve = OnsetCurve.Compute(SyntheticSignal.AccentedClicks(Bpm, 30, Rate, AccentedClick, 4, 3.0), Rate);

        // Act
        var downbeat = DownbeatEstimator.Estimate(curve, 170, Bpm, 170 + AccentedClick);

        // Assert
        Assert.NotNull(downbeat);
        Assert.Equal(170 + AccentedClick, downbeat.Value, 0.020);
    }

    [Fact(DisplayName = "downbeat_is_null_on_an_empty_curve")]
    public void Estimate_ReturnsNull_ForAnEmptyCurve()
    {
        // Arrange
        var curve = new OnsetCurve([], 0.01, 0.01);

        // Act
        var downbeat = DownbeatEstimator.Estimate(curve, 0, Bpm, 0.1);

        // Assert
        Assert.Null(downbeat);
    }

    [Fact(DisplayName = "downbeat_is_null_on_a_flat_curve")]
    public void Estimate_ReturnsNull_ForAFlatCurve()
    {
        // Arrange
        var curve = OnsetCurve.Compute(new float[Rate * 10], Rate);

        // Act
        var downbeat = DownbeatEstimator.Estimate(curve, 0, Bpm, 0.1);

        // Assert
        Assert.Null(downbeat);
    }
}