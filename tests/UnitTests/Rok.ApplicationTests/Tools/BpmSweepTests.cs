using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class BpmSweepTests
{
    private const double Tolerance = 0.02;

    [Fact(DisplayName = "sweep_rejects_windows_below_threshold_and_keeps_those_at_or_above")]
    public void Aggregate_RejectsBelowThreshold()
    {
        // Arrange
        SweepWindow[] windows =
        [
            new(true, 0.10, 120, 120),
            new(true, 0.20, 120, 120),
            new(true, 0.30, 120, 120)
        ];

        // Act
        var rows = BpmSweep.Aggregate(windows, [0.05, 0.20, 0.30], Tolerance);

        // Assert
        Assert.Equal(3, rows[0].Intro.NotRejected);
        Assert.Equal(2, rows[1].Intro.NotRejected);
        Assert.Equal(1, rows[2].Intro.NotRejected);
    }

    [Fact(DisplayName = "sweep_computes_accuracy_among_not_rejected_windows_with_octave_tolerance")]
    public void Aggregate_ComputesAccuracyAmongNotRejected()
    {
        // Arrange
        SweepWindow[] windows =
        [
            new(false, 0.5, 60, 120),
            new(false, 0.5, 90, 120),
            new(false, 0.01, 120, 120)
        ];

        // Act
        var row = BpmSweep.Aggregate(windows, [0.1], Tolerance)[0];

        // Assert
        Assert.Equal(3, row.Outro.Total);
        Assert.Equal(2, row.Outro.NotRejected);
        Assert.Equal(1, row.Outro.Correct);
        Assert.Equal(0.5, row.Outro.CorrectRate, 3);
        Assert.Equal(2.0 / 3.0, row.Outro.NotRejectedRate, 3);
    }

    [Fact(DisplayName = "sweep_always_rejects_windows_without_a_detected_tempo")]
    public void Aggregate_RejectsWindowsWithoutTempo()
    {
        // Arrange
        SweepWindow[] windows = [new(true, 0, null, 120)];

        // Act
        var row = BpmSweep.Aggregate(windows, [0.05], Tolerance)[0];

        // Assert
        Assert.Equal(1, row.All.Total);
        Assert.Equal(0, row.All.NotRejected);
        Assert.Equal(0, row.All.CorrectRate);
    }

    [Fact(DisplayName = "sweep_splits_intro_and_outro_and_sums_them_in_global")]
    public void Aggregate_SplitsIntroAndOutro()
    {
        // Arrange
        SweepWindow[] windows =
        [
            new(true, 0.4, 100, 100),
            new(false, 0.4, 150, 100),
            new(false, 0.4, 100, 100)
        ];

        // Act
        var row = BpmSweep.Aggregate(windows, [0.1], Tolerance)[0];

        // Assert
        Assert.Equal(1, row.Intro.Total);
        Assert.Equal(2, row.Outro.Total);
        Assert.Equal(3, row.All.NotRejected);
        Assert.Equal(2, row.All.Correct);
    }

    [Fact(DisplayName = "sweep_default_thresholds_run_from_005_to_030_by_0025")]
    public void DefaultThresholds_CoverTheRange()
    {
        // Act
        var thresholds = BpmSweep.DefaultThresholds;

        // Assert
        Assert.Equal(11, thresholds.Count);
        Assert.Equal(0.05, thresholds[0], 6);
        Assert.Equal(0.30, thresholds[^1], 6);
    }
}