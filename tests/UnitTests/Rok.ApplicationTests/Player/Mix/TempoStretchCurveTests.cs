using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class TempoStretchCurveTests
{
    private const double Overlap = 10;
    private const double Return = 15;

    [Fact(DisplayName = "tempo_is_the_outgoing_tempo_during_the_overlap")]
    public void TempoAt_DuringOverlap_IsTheRatio()
    {
        // Act
        var start = TempoStretchCurve.TempoAt(1.06, Overlap, Return, 0);
        var almostEnd = TempoStretchCurve.TempoAt(1.06, Overlap, Return, Overlap - 0.001);

        // Assert
        Assert.Equal(1.06, start);
        Assert.Equal(1.06, almostEnd);
    }

    [Theory(DisplayName = "tempo_ramps_linearly_back_over_the_return")]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1)]
    public void TempoAt_DuringReturn_IsLinear(double fraction)
    {
        // Arrange
        const double ratio = 1.06;

        // Act
        var tempo = TempoStretchCurve.TempoAt(ratio, Overlap, Return, Overlap + Return * fraction);

        // Assert
        Assert.Equal(ratio + (1 - ratio) * fraction, tempo, 9);
    }

    [Fact(DisplayName = "tempo_is_original_after_the_return")]
    public void TempoAt_AfterReturn_IsOne()
    {
        // Act
        var tempo = TempoStretchCurve.TempoAt(0.95, Overlap, Return, Overlap + Return + 5);

        // Assert
        Assert.Equal(1, tempo);
    }

    [Fact(DisplayName = "is_complete_after_overlap_plus_return")]
    public void IsComplete_OnlyAfterOverlapPlusReturn()
    {
        // Assert
        Assert.False(TempoStretchCurve.IsComplete(Overlap, Return, Overlap + Return - 0.01));
        Assert.True(TempoStretchCurve.IsComplete(Overlap, Return, Overlap + Return));
    }

    [Fact(DisplayName = "return_lasts_eight_incoming_bars")]
    public void ReturnSeconds_At128Bpm_IsFifteenSeconds()
    {
        // Act
        var seconds = TempoStretchCurve.ReturnSeconds(128);

        // Assert
        Assert.Equal(15, seconds, 9);
    }

    [Theory(DisplayName = "clamp_ratio_keeps_the_planned_range")]
    [InlineData(1.0, 1.0)]
    [InlineData(1.07, 1.07)]
    [InlineData(0.93, 0.93)]
    [InlineData(1.2, 1.08)]
    [InlineData(0.5, 0.92)]
    public void ClampRatio_BoundsToTheRange(double ratio, double expected)
    {
        // Act
        var clamped = TempoStretchCurve.ClampRatio(ratio);

        // Assert
        Assert.Equal(expected, clamped, 9);
    }

    [Theory(DisplayName = "clamp_ratio_rejects_non_finite")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ClampRatio_NonFiniteOrNonPositive_IsOne(double ratio)
    {
        // Act
        var clamped = TempoStretchCurve.ClampRatio(ratio);

        // Assert
        Assert.Equal(1, clamped);
    }
}