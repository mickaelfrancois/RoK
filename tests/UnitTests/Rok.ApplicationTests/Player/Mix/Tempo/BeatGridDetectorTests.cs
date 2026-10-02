using Rok.Application.Player.Mix;
using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class BeatGridDetectorTests
{
    private const int Rate = 11025;

    [Theory(DisplayName = "detect_finds_tempo_and_first_beat_of_clicks")]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(174)]
    public void Detect_FindsTempoAndPhase(double bpm)
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(bpm, 30, Rate, 0.137);

        // Act
        var detection = BeatGridDetector.Detect(clicks, Rate, 0, null);

        // Assert
        Assert.NotNull(detection.Bpm);
        Assert.InRange(detection.Bpm.Value, bpm * 0.99, bpm * 1.01);
        Assert.NotNull(detection.FirstBeatSeconds);
        Assert.InRange(detection.FirstBeatSeconds.Value, 0.137 - 0.02, 0.137 + 0.02);
        Assert.True(detection.BpmConfidence >= MixThresholds.MinBeatConfidence);
    }

    [Theory(DisplayName = "detect_folds_sixty_and_two_hundred_forty_bpm_to_one_hundred_twenty")]
    [InlineData(60)]
    [InlineData(240)]
    public void Detect_FoldsExtremeTempos(double bpm)
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(bpm, 30, Rate, 0.137);

        // Act
        var detection = BeatGridDetector.Detect(clicks, Rate, 0, null);

        // Assert
        Assert.NotNull(detection.Bpm);
        Assert.InRange(detection.Bpm.Value, 120 * 0.99, 120 * 1.01);
    }

    [Fact(DisplayName = "detect_reports_nothing_for_white_noise")]
    public void Detect_ReturnsNone_ForNoise()
    {
        // Act
        var detection = BeatGridDetector.Detect(SyntheticSignal.WhiteNoise(30, Rate), Rate, 0, null);

        // Assert
        Assert.Null(detection.Bpm);
        Assert.Null(detection.FirstBeatSeconds);
        Assert.True(detection.BpmConfidence < MixThresholds.MinBeatConfidence);
    }

    [Fact(DisplayName = "detect_reports_nothing_for_silence")]
    public void Detect_ReturnsNone_ForSilence()
    {
        // Act
        var detection = BeatGridDetector.Detect(new float[Rate * 30], Rate, 0, null);

        // Assert
        Assert.Null(detection.Bpm);
        Assert.Null(detection.FirstBeatSeconds);
    }

    [Fact(DisplayName = "detect_reports_nothing_when_the_signal_is_shorter_than_the_minimum")]
    public void Detect_ReturnsNone_WhenSignalTooShort()
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(120, MixThresholds.MinTempoSeconds - 1, Rate);

        // Act
        var detection = BeatGridDetector.Detect(clicks, Rate, 0, null);

        // Assert
        Assert.Null(detection.Bpm);
        Assert.Null(detection.FirstBeatSeconds);
    }

    [Fact(DisplayName = "detect_keeps_the_known_bpm_exactly_and_computes_only_the_phase")]
    public void Detect_UsesKnownBpm()
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(120, 30, Rate, 0.137);

        // Act
        var detection = BeatGridDetector.Detect(clicks, Rate, 0, 120);

        // Assert
        Assert.Equal(120d, detection.Bpm);
        Assert.NotNull(detection.FirstBeatSeconds);
        Assert.InRange(detection.FirstBeatSeconds.Value, 0.137 - 0.02, 0.137 + 0.02);
    }

    [Fact(DisplayName = "detect_dates_the_first_beat_in_absolute_track_time")]
    public void Detect_UsesAbsoluteTime_ForAnOutroWindow()
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(120, 30, Rate, 0.137);

        // Act
        var detection = BeatGridDetector.Detect(clicks, Rate, 170, null);

        // Assert
        Assert.NotNull(detection.FirstBeatSeconds);
        Assert.True(detection.FirstBeatSeconds >= 170);
        Assert.InRange(detection.FirstBeatSeconds.Value, 170 + 0.137 - 0.02, 170 + 0.137 + 0.02);
    }

    [Fact(DisplayName = "detect_on_a_curve_matches_detect_on_mono")]
    public void Detect_OnCurve_MatchesDetectOnMono()
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(120, 30, Rate, 0.137);
        var curve = OnsetCurve.Compute(clicks, Rate);

        // Act
        var fromMono = BeatGridDetector.Detect(clicks, Rate, 170, null);
        var fromCurve = BeatGridDetector.Detect(curve, 170, null);

        // Assert
        Assert.Equal(fromMono, fromCurve);
    }

    [Theory(DisplayName = "is_long_enough_requires_the_minimum_duration_and_a_positive_rate")]
    [InlineData(88200, 11025, true)]
    [InlineData(88199, 11025, false)]
    [InlineData(88200, 0, false)]
    public void IsLongEnough_ChecksLengthAndRate(int count, int rate, bool expected)
    {
        // Act
        var result = BeatGridDetector.IsLongEnough(count, rate);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "detect_works_at_a_twelve_kilohertz_rate")]
    public void Detect_WorksAtTwelveKilohertz()
    {
        // Arrange
        var clicks = SyntheticSignal.Clicks(128, 30, 12000, 0.2);

        // Act
        var detection = BeatGridDetector.Detect(clicks, 12000, 0, null);

        // Assert
        Assert.NotNull(detection.Bpm);
        Assert.InRange(detection.Bpm.Value, 128 * 0.99, 128 * 1.01);
    }
}