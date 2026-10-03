using Rok.Application.Dto;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class MixScanReportTests
{
    private static TrackDto Track(long id) => new() { Id = id, Title = $"T{id}", MusicFile = $@"C:\music\{id}.mp3" };

    private static TrackScanResult Result(long id, TrackScanStatus status, TrackAnalysisEntity? row = null, string? reason = null) =>
        new(Track(id), status, row, reason);

    private static TrackAnalysisEntity Row(
        long id,
        BpmSource? source,
        double? confidence = null,
        bool intro = true,
        bool outro = true,
        double? introPhase = 0.5,
        double? outroPhase = 0.5,
        double? score = null) =>
        new()
        {
            TrackId = id,
            BpmSource = source,
            Bpm = source is null ? null : 120,
            BpmConfidence = confidence,
            IntroTempoAnalysed = intro,
            OutroTempoAnalysed = outro,
            IntroBeatPhase = introPhase,
            OutroBeatPhase = outroPhase,
            OutroMixPointScore = score
        };

    [Fact(DisplayName = "report_aggregates_coverage_bpm_and_mix_points")]
    public void Aggregate_Results_CountsCoverageBpmAndMixPoints()
    {
        // Arrange
        TrackScanResult[] results =
        [
            Result(1, TrackScanStatus.Skipped, Row(1, BpmSource.Tag, score: 0.9)),
            Result(2, TrackScanStatus.Analysed, Row(2, BpmSource.Detected, 0.42, score: 0.1)),
            Result(3, TrackScanStatus.Analysed, Row(3, BpmSource.Detected, 0.97, intro: false, introPhase: null, score: null)),
            Result(4, TrackScanStatus.MissingFile, null, "file not found"),
            Result(5, TrackScanStatus.Failed, null, "boom"),
            Result(6, TrackScanStatus.Interrupted, Row(6, null, intro: false, outro: false, introPhase: null, outroPhase: null))
        ];

        // Act
        var summary = MixScanReport.Aggregate(results, 7);

        // Assert
        Assert.Equal(6, summary.Analysable);
        Assert.Equal(7, summary.LiveExcluded);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(2, summary.Analysed);
        Assert.Equal(1, summary.MissingFile);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Interrupted);
        Assert.Equal(2, summary.CompleteRows);
        Assert.Equal(1, summary.BpmFromTag);
        Assert.Equal(2, summary.BpmDetected);
        Assert.Equal(2, summary.IntroGrid);
        Assert.Equal(3, summary.OutroGrid);
        Assert.Equal(3, summary.MixPoints.Total);
        Assert.Equal(2, summary.MixPoints.WithCandidate);
        Assert.Equal(1, summary.MixPoints.Retained);
        Assert.Equal(2, summary.ConfidenceHistogram.Sum(b => b.Count));
        Assert.Equal((0.4, 1), summary.ConfidenceHistogram[4]);
        Assert.Equal((0.9, 1), summary.ConfidenceHistogram[9]);
        Assert.Equal(1.0, summary.ConfidenceSweep[0].Rate);
        Assert.Equal(0.15, summary.ConfidenceSweep[0].Threshold);
        Assert.Equal(0.5, summary.ConfidenceSweep[^1].Rate);
    }

    [Fact(DisplayName = "report_lists_the_counts_and_states_dry_run")]
    public void FormatReport_DryRun_ListsCountsAndStatesMode()
    {
        // Arrange
        var summary = MixScanReport.Aggregate([Result(1, TrackScanStatus.Analysed, Row(1, BpmSource.Tag, score: 0.6))], 2);

        // Act
        var dry = MixScanReport.FormatReport(summary, false);
        var written = MixScanReport.FormatReport(summary, true);

        // Assert
        Assert.Contains(dry, l => l.Contains("dry run"));
        Assert.Contains(written, l => l.Contains("rows written"));
        Assert.Contains(dry, l => l.Contains("live excluded: 2"));
        Assert.Contains(dry, l => l.Contains("Complete rows") && l.Contains("1/1") && l.Contains("100.0%"));
    }

    [Fact(DisplayName = "progress_line_shows_rate_and_eta")]
    public void FormatProgress_Progress_ShowsRateAndEta()
    {
        // Arrange
        var progress = new MixScanProgress(50, 200, TimeSpan.FromSeconds(10));

        // Act
        var line = MixScanReport.FormatProgress(progress);

        // Assert
        Assert.Equal("50/200 (25%)  5.0 tracks/s  ETA 00:00:30", line);
    }

    [Fact(DisplayName = "progress_line_has_unknown_eta_before_any_progress")]
    public void FormatProgress_NothingDone_HasUnknownEta()
    {
        // Arrange
        var progress = new MixScanProgress(0, 10, TimeSpan.Zero);

        // Act
        var line = MixScanReport.FormatProgress(progress);

        // Assert
        Assert.Equal("0/10 (0%)  0.0 tracks/s  ETA --:--:--", line);
    }

    [Fact(DisplayName = "failures_file_lists_missing_and_failed_tracks")]
    public void FormatFailures_Results_ListsOnlyMissingAndFailed()
    {
        // Arrange
        TrackScanResult[] results =
        [
            Result(1, TrackScanStatus.Analysed),
            Result(2, TrackScanStatus.MissingFile, null, "file not found"),
            Result(3, TrackScanStatus.Skipped),
            Result(4, TrackScanStatus.Failed, null, "the file could not be decoded"),
            Result(5, TrackScanStatus.Interrupted)
        ];

        // Act
        var lines = MixScanReport.FormatFailures(results);

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal("#2\tMissingFile\tC:\\music\\2.mp3\tfile not found", lines[0]);
        Assert.Equal("#4\tFailed\tC:\\music\\4.mp3\tthe file could not be decoded", lines[1]);
    }
}