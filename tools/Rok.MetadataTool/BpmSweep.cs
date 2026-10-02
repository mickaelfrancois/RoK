using Rok.Application.Player.Mix.Tempo;

namespace Rok.MetadataTool;

/// <summary>One analysed window: the raw tempo confidence and tempo, before any rejection threshold.</summary>
/// <param name="Intro">True for the head of the track, false for the tail.</param>
/// <param name="Confidence">Raw tempo confidence (0 when nothing could be estimated).</param>
/// <param name="DetectedBpm">Detected tempo, or <c>null</c> when nothing could be estimated.</param>
/// <param name="TagBpm">Tempo from the file tag.</param>
internal sealed record SweepWindow(bool Intro, double Confidence, double? DetectedBpm, double TagBpm);

/// <summary>Counts for a set of windows at one threshold.</summary>
internal sealed record SweepStats(int Total, int NotRejected, int Correct)
{
    public double NotRejectedRate => Total == 0 ? 0 : (double)NotRejected / Total;

    public double CorrectRate => NotRejected == 0 ? 0 : (double)Correct / NotRejected;
}

/// <summary>Results at one threshold.</summary>
internal sealed record SweepRow(double Threshold, SweepStats Intro, SweepStats Outro, SweepStats All);

/// <summary>Pure aggregation of the confidence-threshold sweep.</summary>
internal static class BpmSweep
{
    /// <summary>Thresholds from 0.05 to 0.30 by steps of 0.025.</summary>
    public static IReadOnlyList<double> DefaultThresholds { get; } = Enumerable.Range(0, 11).Select(i => 0.05 + (i * 0.025)).ToArray();

    /// <summary>Computes, for each threshold, the not-rejected rate and the accuracy among the not-rejected windows.</summary>
    /// <param name="windows">Analysed windows.</param>
    /// <param name="thresholds">Thresholds to evaluate; a window is rejected when its confidence is below the threshold.</param>
    /// <param name="tolerance">Relative tolerance of <see cref="TempoMatch"/>.</param>
    /// <returns>One row per threshold, in the order given.</returns>
    public static IReadOnlyList<SweepRow> Aggregate(IReadOnlyCollection<SweepWindow> windows, IEnumerable<double> thresholds, double tolerance)
    {
        List<SweepRow> rows = [];

        foreach (double threshold in thresholds)
        {
            SweepStats intro = Count(windows.Where(w => w.Intro), threshold, tolerance);
            SweepStats outro = Count(windows.Where(w => !w.Intro), threshold, tolerance);

            rows.Add(new SweepRow(threshold, intro, outro, new SweepStats(intro.Total + outro.Total, intro.NotRejected + outro.NotRejected, intro.Correct + outro.Correct)));
        }

        return rows;
    }

    private static SweepStats Count(IEnumerable<SweepWindow> windows, double threshold, double tolerance)
    {
        int total = 0;
        int notRejected = 0;
        int correct = 0;

        foreach (SweepWindow window in windows)
        {
            total++;

            if (window.DetectedBpm is not { } bpm || window.Confidence < threshold)
                continue;

            notRejected++;

            if (TempoMatch.IsOctaveEquivalent(bpm, window.TagBpm, tolerance))
                correct++;
        }

        return new SweepStats(total, notRejected, correct);
    }
}