using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class MixPointStatsTests
{
    [Fact(DisplayName = "aggregate_counts_retained_and_buckets_scores")]
    public void Aggregate_CountsRetainedAndBucketsScores()
    {
        // Arrange
        MixPointSample[] samples =
        [
            new(0.0), new(0.05), new(0.36), new(0.52), new(0.52), new(1.4), new(1.5), new(null), new(null)
        ];

        // Act
        var summary = MixPointStats.Aggregate(samples, 0.35);

        // Assert
        Assert.Equal(9, summary.Total);
        Assert.Equal(7, summary.WithCandidate);
        Assert.Equal(5, summary.Retained);
        Assert.Equal(15, summary.Histogram.Count);
        Assert.Equal((0.0, 2), summary.Histogram[0]);
        Assert.Equal((0.3, 1), summary.Histogram[3]);
        Assert.Equal((0.5, 2), summary.Histogram[5]);
        Assert.Equal((1.4, 2), summary.Histogram[14]);
        Assert.Equal(7, summary.Histogram.Sum(b => b.Count));
    }

    [Fact(DisplayName = "aggregate_sweeps_thresholds_from_015_to_060_over_all_outros")]
    public void Aggregate_SweepsThresholds()
    {
        // Arrange
        MixPointSample[] samples = [new(0.2), new(0.4), new(0.6), new(null)];

        // Act
        var summary = MixPointStats.Aggregate(samples, 0.35);

        // Assert
        Assert.Equal(10, summary.Sweep.Count);
        Assert.Equal(0.15, summary.Sweep[0].Threshold, 1e-9);
        Assert.Equal(0.60, summary.Sweep[^1].Threshold, 1e-9);
        Assert.Equal(0.75, summary.Sweep[0].RetainedRate, 1e-9);
        Assert.Equal(0.5, summary.Sweep.Single(s => Math.Abs(s.Threshold - 0.35) < 1e-9).RetainedRate, 1e-9);
        Assert.Equal(0.25, summary.Sweep[^1].RetainedRate, 1e-9);
    }

    [Fact(DisplayName = "aggregate_of_nothing_is_empty")]
    public void Aggregate_Empty_GivesZeros()
    {
        // Act
        var summary = MixPointStats.Aggregate([], 0.35);

        // Assert
        Assert.Equal(0, summary.Total);
        Assert.Equal(0, summary.Retained);
        Assert.All(summary.Sweep, s => Assert.Equal(0, s.RetainedRate));
    }
}