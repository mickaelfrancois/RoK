using Rok.Application.Player.Mix;
using Rok.Application.Player.Mix.Tempo;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Player.Mix;

public class MixPointDetectorTests
{
    private const double Bpm = 120;
    private const double Bar = 2.0;
    private const double FirstDownbeat = 1.0;
    private const double Window = 0.05;
    private const int PreWindows = 20;
    private const int BarWindows = 40;

    [Fact(DisplayName = "energy_rupture_at_bar_9_is_the_retained_candidate")]
    public void Detect_EnergyRupture_IsTheRetainedCandidate()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(12, (0, -30), (8, -15)));
        var curve = FlatCurve(envelope);

        // Act
        var point = MixPointDetector.Detect(envelope, curve, 0, Grid(), MusicEnd(12));

        // Assert
        Assert.NotNull(point);
        Assert.Equal(FirstDownbeat + (8 * Bar), point.Seconds, Window);
        Assert.True(point.Score >= MixThresholds.MinMixPointScore);
    }

    [Fact(DisplayName = "flat_signal_has_no_candidate_above_the_threshold")]
    public void Detect_FlatSignal_HasNoRetainedCandidate()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(12, (0, -20)));
        var curve = FlatCurve(envelope);

        // Act
        var point = MixPointDetector.Detect(envelope, curve, 0, Grid(), MusicEnd(12));

        // Assert
        Assert.True(point is null || point.Score < MixThresholds.MinMixPointScore);
    }

    [Fact(DisplayName = "one_quiet_bar_is_a_dip_candidate")]
    public void Detect_QuietBar_IsADipCandidate()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(12, (0, -20), (6, -35), (7, -20)));
        var curve = FlatCurve(envelope);

        // Act
        var point = MixPointDetector.Detect(envelope, curve, 0, Grid(), MusicEnd(12));

        // Assert
        Assert.NotNull(point);
        Assert.Equal(FirstDownbeat + (6 * Bar), point.Seconds, Window);
        Assert.True(point.Score >= MixThresholds.MinMixPointScore);
    }

    [Fact(DisplayName = "candidate_too_close_to_the_music_end_is_ignored")]
    public void Detect_CandidateTooCloseToTheEnd_IsIgnored()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(12, (0, -30), (11, -15)));
        var curve = FlatCurve(envelope);
        var musicEnd = FirstDownbeat + (12 * Bar);

        // Act
        var point = MixPointDetector.Detect(envelope, curve, 0, Grid(), musicEnd);

        // Assert
        Assert.True(point is null || point.Seconds + MixThresholds.MixPointMinRoomSeconds <= musicEnd);
        Assert.NotEqual(FirstDownbeat + (11 * Bar), point?.Seconds);
    }

    [Fact(DisplayName = "boundary_8_bars_after_a_rupture_scores_more_than_its_neighbours")]
    public void Detect_BoundaryOnThePhrase_WinsOverAStrongerChangeOffPhrase()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(20, (0, -40), (4, -28), (12, -19)));
        var curve = FlatCurve(envelope);

        // Act
        var point = MixPointDetector.Detect(envelope, curve, 0, Grid(), MusicEnd(20));

        // Assert
        Assert.NotNull(point);
        Assert.Equal(FirstDownbeat + (12 * Bar), point.Seconds, Window);
    }

    [Fact(DisplayName = "spectral_change_without_energy_change_is_a_candidate")]
    public void Detect_OnsetChange_IsACandidate()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(12, (0, -20)));
        var changeAt = FirstDownbeat + (8 * Bar);
        var values = new float[(int)(envelope.LevelsDb.Length * Window / 0.01)];

        for (var i = 0; i < values.Length; i++)
            values[i] = (i * 0.01) + 0.01 >= changeAt ? 1f : 0f;

        var curve = new OnsetCurve(values, 0.01, 0.01);

        // Act
        var point = MixPointDetector.Detect(envelope, curve, 0, Grid(), MusicEnd(12));

        // Assert
        Assert.NotNull(point);
        Assert.Equal(changeAt, point.Seconds, Window);
        Assert.True(point.Score >= MixThresholds.MinMixPointScore);
    }

    [Fact(DisplayName = "mix_point_is_null_without_downbeat")]
    public void Detect_WithoutDownbeat_ReturnsNull()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(12, (0, -30), (8, -15)));
        var grid = new BeatGrid(Bpm, FirstDownbeat, 1, BpmSource.Detected);

        // Act
        var point = MixPointDetector.Detect(envelope, FlatCurve(envelope), 0, grid, MusicEnd(12));

        // Assert
        Assert.Null(point);
    }

    [Fact(DisplayName = "mix_point_is_null_when_less_than_two_bars_fit")]
    public void Detect_TooShortOutro_ReturnsNull()
    {
        // Arrange
        var envelope = BuildEnvelope(Bars(1, (0, -30)));

        // Act
        var point = MixPointDetector.Detect(envelope, FlatCurve(envelope), 0, Grid(), MusicEnd(1));

        // Assert
        Assert.Null(point);
    }

    private static BeatGrid Grid() => new(Bpm, FirstDownbeat, 1, BpmSource.Detected, FirstDownbeat);

    private static double MusicEnd(int bars) => FirstDownbeat + (bars * Bar) + 1;

    private static OnsetCurve FlatCurve(RmsEnvelope envelope) =>
        new(new float[(int)(envelope.LevelsDb.Length * Window / 0.01)], 0.01, 0.01);

    private static double[] Bars(int count, params (int From, double Level)[] steps)
    {
        var levels = new double[count];
        var level = steps[0].Level;

        for (var bar = 0; bar < count; bar++)
        {
            foreach (var step in steps)
            {
                if (step.From == bar)
                    level = step.Level;
            }

            levels[bar] = level;
        }

        return levels;
    }

    private static RmsEnvelope BuildEnvelope(double[] barLevels)
    {
        var levels = new List<float>();

        levels.AddRange(Enumerable.Repeat((float)barLevels[0], PreWindows));

        foreach (var level in barLevels)
            levels.AddRange(Enumerable.Repeat((float)level, BarWindows));

        levels.AddRange(Enumerable.Repeat((float)barLevels[^1], PreWindows));

        return new RmsEnvelope(0, Window, [.. levels], levels.Count * Window);
    }
}