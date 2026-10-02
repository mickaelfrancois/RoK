namespace Rok.MetadataTool;

/// <summary>Best mix point score of one analysed outro.</summary>
/// <param name="Score">Score of the best candidate, or <c>null</c> when there is no beat grid or no candidate.</param>
internal sealed record MixPointSample(double? Score);

/// <summary>Distribution of the mix point scores of a library.</summary>
/// <param name="Total">Number of analysed outros.</param>
/// <param name="WithCandidate">Number of outros that have a candidate, whatever its score.</param>
/// <param name="Retained">Number of outros whose score reaches the threshold.</param>
/// <param name="Histogram">Number of candidates per score bucket of 0.1; the last bucket also holds the scores above its start.</param>
/// <param name="Sweep">Share of the outros retained, for each threshold of the sweep.</param>
internal sealed record MixPointSummary(
    int Total,
    int WithCandidate,
    int Retained,
    IReadOnlyList<(double From, int Count)> Histogram,
    IReadOnlyList<(double Threshold, double RetainedRate)> Sweep);

/// <summary>Pure aggregation of the mix point scores measured by <c>bpm-check</c>.</summary>
internal static class MixPointStats
{
    private const double BucketWidth = 0.1;
    private const int BucketCount = 15;
    private const double SweepFrom = 0.15;
    private const double SweepStep = 0.05;
    private const int SweepCount = 10;

    /// <summary>Counts the retained outros, buckets the scores and sweeps the threshold.</summary>
    /// <param name="samples">One sample per analysed outro.</param>
    /// <param name="threshold">Score from which a mix point is retained.</param>
    /// <returns>The summary.</returns>
    public static MixPointSummary Aggregate(IReadOnlyCollection<MixPointSample> samples, double threshold)
    {
        int withCandidate = 0;
        int retained = 0;
        int[] buckets = new int[BucketCount];

        foreach (MixPointSample sample in samples)
        {
            if (sample.Score is not { } score)
                continue;

            withCandidate++;

            if (score >= threshold)
                retained++;

            int bucket = Math.Clamp((int)Math.Floor((score / BucketWidth) + 1e-9), 0, BucketCount - 1);
            buckets[bucket]++;
        }

        List<(double From, int Count)> histogram = [];

        for (int i = 0; i < BucketCount; i++)
            histogram.Add((Math.Round(i * BucketWidth, 1), buckets[i]));

        List<(double Threshold, double RetainedRate)> sweep = [];

        for (int i = 0; i < SweepCount; i++)
        {
            double value = Math.Round(SweepFrom + (i * SweepStep), 2);
            int count = samples.Count(s => s.Score is { } score && score >= value);

            sweep.Add((value, samples.Count == 0 ? 0 : (double)count / samples.Count));
        }

        return new MixPointSummary(samples.Count, withCandidate, retained, histogram, sweep);
    }
}