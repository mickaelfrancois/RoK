using System.Globalization;
using Rok.Application.Player.Mix;
using Rok.Domain.Enums;

namespace Rok.MetadataTool;

/// <summary>Counts and distributions of a <c>mix-scan</c> run.</summary>
/// <param name="Analysable">Number of tracks the scan looked at (live tracks excluded).</param>
/// <param name="LiveExcluded">Number of live tracks left out of the scan.</param>
/// <param name="Skipped">Tracks whose stored row was already valid and complete.</param>
/// <param name="Analysed">Tracks decoded without failure.</param>
/// <param name="MissingFile">Tracks whose file was not found.</param>
/// <param name="Failed">Tracks whose analysis or storage failed.</param>
/// <param name="Interrupted">Tracks cut short by a cancellation.</param>
/// <param name="CompleteRows">Rows whose intro and outro were both analysed.</param>
/// <param name="BpmFromTag">Rows whose tempo comes from the file tag.</param>
/// <param name="BpmDetected">Rows whose tempo was found by the detector.</param>
/// <param name="IntroGrid">Rows with a beat phase on the intro.</param>
/// <param name="OutroGrid">Rows with a beat phase on the outro.</param>
/// <param name="MixPoints">Distribution of the mix point scores of the analysed outros.</param>
/// <param name="ConfidenceHistogram">Number of detected tempos per confidence bucket of 0.1.</param>
/// <param name="ConfidenceSweep">Share of the detected tempos kept, for each confidence threshold.</param>
internal sealed record MixScanSummary(
    int Analysable,
    int LiveExcluded,
    int Skipped,
    int Analysed,
    int MissingFile,
    int Failed,
    int Interrupted,
    int CompleteRows,
    int BpmFromTag,
    int BpmDetected,
    int IntroGrid,
    int OutroGrid,
    MixPointSummary MixPoints,
    IReadOnlyList<(double From, int Count)> ConfidenceHistogram,
    IReadOnlyList<(double Threshold, double Rate)> ConfidenceSweep);

/// <summary>Pure aggregation and formatting of the output of <c>mix-scan</c>.</summary>
internal static class MixScanReport
{
    private const double ConfidenceBucketWidth = 0.1;
    private const int ConfidenceBucketCount = 10;
    private const double SweepStep = 0.05;
    private const int SweepCount = 10;

    /// <summary>Aggregates the results of a scan.</summary>
    /// <param name="results">One result per scanned track.</param>
    /// <param name="liveExcluded">Number of live tracks left out of the scan.</param>
    /// <returns>The summary.</returns>
    public static MixScanSummary Aggregate(IReadOnlyList<TrackScanResult> results, int liveExcluded)
    {
        var rows = results.Where(r => r.Row is not null).Select(r => r.Row!).ToList();
        var samples = rows.Where(r => r.OutroTempoAnalysed).Select(r => new MixPointSample(r.OutroMixPointScore)).ToList();
        var confidences = rows.Where(r => r.BpmSource == BpmSource.Detected && r.BpmConfidence is not null).Select(r => r.BpmConfidence!.Value).ToList();

        return new MixScanSummary(
            results.Count,
            liveExcluded,
            Count(results, TrackScanStatus.Skipped),
            Count(results, TrackScanStatus.Analysed),
            Count(results, TrackScanStatus.MissingFile),
            Count(results, TrackScanStatus.Failed),
            Count(results, TrackScanStatus.Interrupted),
            rows.Count(r => r.IntroTempoAnalysed && r.OutroTempoAnalysed),
            rows.Count(r => r.BpmSource == BpmSource.Tag),
            rows.Count(r => r.BpmSource == BpmSource.Detected),
            rows.Count(r => r.IntroBeatPhase is not null),
            rows.Count(r => r.OutroBeatPhase is not null),
            MixPointStats.Aggregate(samples, MixThresholds.MinMixPointScore),
            BuildHistogram(confidences),
            BuildSweep(confidences));
    }

    /// <summary>Formats the final report.</summary>
    /// <param name="summary">The aggregated results.</param>
    /// <param name="write">Whether the rows were written to the database.</param>
    /// <returns>The lines to print.</returns>
    public static IReadOnlyList<string> FormatReport(MixScanSummary summary, bool write)
    {
        var culture = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            string.Empty,
            write ? "Mix scan report (rows written)" : "Mix scan report (dry run, nothing written)",
            $"  Tracks scanned     : {summary.Analysable}  (live excluded: {summary.LiveExcluded})",
            $"  Already complete   : {summary.Skipped}",
            $"  Analysed           : {summary.Analysed}",
            $"  Missing files      : {summary.MissingFile}",
            $"  Failed             : {summary.Failed}",
            $"  Interrupted        : {summary.Interrupted}",
            $"  Complete rows      : {summary.CompleteRows}/{summary.Analysable} ({Percent(summary.CompleteRows, summary.Analysable)})",
            $"  BPM from tag       : {summary.BpmFromTag} ({Percent(summary.BpmFromTag, summary.Analysable)})",
            $"  BPM detected       : {summary.BpmDetected} ({Percent(summary.BpmDetected, summary.Analysable)})",
            $"  Intro beat grid    : {summary.IntroGrid} ({Percent(summary.IntroGrid, summary.Analysable)})",
            $"  Outro beat grid    : {summary.OutroGrid} ({Percent(summary.OutroGrid, summary.Analysable)})",
            $"  Mix points (score >= {MixThresholds.MinMixPointScore.ToString("F2", culture)}) : {summary.MixPoints.Retained}/{summary.MixPoints.Total} outros ({summary.MixPoints.WithCandidate} with a candidate)",
            "  Mix point score histogram:"
        };

        foreach (var (from, count) in summary.MixPoints.Histogram)
            lines.Add($"    {from.ToString("F1", culture)}+ : {count}");

        lines.Add("  Mix point threshold sweep (share of outros kept):");

        foreach (var (threshold, rate) in summary.MixPoints.Sweep)
            lines.Add($"    >= {threshold.ToString("F2", culture)} : {(rate * 100).ToString("F1", culture)}%");

        lines.Add("  Detected BPM confidence histogram:");

        foreach (var (from, count) in summary.ConfidenceHistogram)
            lines.Add($"    {from.ToString("F1", culture)}+ : {count}");

        lines.Add($"  Detected BPM confidence sweep (min beat confidence {MixThresholds.MinBeatConfidence.ToString("F2", culture)}):");

        foreach (var (threshold, rate) in summary.ConfidenceSweep)
            lines.Add($"    >= {threshold.ToString("F2", culture)} : {(rate * 100).ToString("F1", culture)}%");

        return lines;
    }

    /// <summary>Formats the single progress line.</summary>
    /// <param name="progress">The progress of the scan.</param>
    /// <returns>The line, without line break.</returns>
    public static string FormatProgress(MixScanProgress progress)
    {
        var culture = CultureInfo.InvariantCulture;
        var percent = progress.Total == 0 ? 100 : (int)(progress.Processed * 100L / progress.Total);
        var seconds = progress.Elapsed.TotalSeconds;
        var rate = seconds > 0 ? progress.Processed / seconds : 0;
        var eta = "--:--:--";

        if (rate > 0)
        {
            var remaining = TimeSpan.FromSeconds((progress.Total - progress.Processed) / rate);
            eta = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        return string.Create(culture, $"{progress.Processed}/{progress.Total} ({percent}%)  {rate:F1} tracks/s  ETA {eta}");
    }

    /// <summary>Lists the tracks that could not be analysed.</summary>
    /// <param name="results">The results of the scan.</param>
    /// <returns>One tab-separated line per missing or failed track.</returns>
    public static IReadOnlyList<string> FormatFailures(IEnumerable<TrackScanResult> results) =>
        results
            .Where(r => r.Status is TrackScanStatus.MissingFile or TrackScanStatus.Failed)
            .Select(r => $"#{r.Track.Id}\t{r.Status}\t{r.Track.MusicFile}\t{r.Reason}")
            .ToList();

    private static int Count(IReadOnlyList<TrackScanResult> results, TrackScanStatus status) =>
        results.Count(r => r.Status == status);

    private static string Percent(int part, int total) =>
        total == 0 ? "0.0%" : (part * 100.0 / total).ToString("F1", CultureInfo.InvariantCulture) + "%";

    private static IReadOnlyList<(double From, int Count)> BuildHistogram(IReadOnlyList<double> confidences)
    {
        var buckets = new int[ConfidenceBucketCount];

        foreach (var confidence in confidences)
        {
            var bucket = Math.Clamp((int)Math.Floor((confidence / ConfidenceBucketWidth) + 1e-9), 0, ConfidenceBucketCount - 1);
            buckets[bucket]++;
        }

        return buckets.Select((count, i) => (Math.Round(i * ConfidenceBucketWidth, 1), count)).ToList();
    }

    private static IReadOnlyList<(double Threshold, double Rate)> BuildSweep(IReadOnlyList<double> confidences)
    {
        var sweep = new List<(double Threshold, double Rate)>();

        for (var i = 0; i < SweepCount; i++)
        {
            var threshold = Math.Round(MixThresholds.MinBeatConfidence + (i * SweepStep), 2);
            var kept = confidences.Count(c => c >= threshold);

            sweep.Add((threshold, confidences.Count == 0 ? 0 : (double)kept / confidences.Count));
        }

        return sweep;
    }
}