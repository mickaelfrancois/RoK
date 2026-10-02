using Rok.Application.Player.Mix;
using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class TempoMatchTests
{
    [Theory(DisplayName = "octave_equivalence_accepts_close_double_and_half_tempos")]
    [InlineData(120, 121, true)]
    [InlineData(120, 60, true)]
    [InlineData(120, 240, true)]
    [InlineData(120, 125, false)]
    public void IsOctaveEquivalent_AtTwoPercent(double a, double b, bool expected)
    {
        // Act
        var result = TempoMatch.IsOctaveEquivalent(a, b, 0.02);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "octave_equivalence_rejects_non_positive_tempos")]
    [InlineData(0, 120)]
    [InlineData(120, 0)]
    [InlineData(-1, -1)]
    public void IsOctaveEquivalent_RejectsNonPositive(double a, double b)
    {
        // Act
        var result = TempoMatch.IsOctaveEquivalent(a, b, 0.02);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "octave_tempos_match_within_three_percent")]
    [InlineData(87, 174)]
    [InlineData(174, 87)]
    [InlineData(120, 121)]
    public void IsOctaveEquivalent_AtThreePercent_Matches(double a, double b)
    {
        // Act
        var result = TempoMatch.IsOctaveEquivalent(a, b, MixThresholds.BeatAlignTempoTolerance);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "distinct_tempos_do_not_match_within_three_percent")]
    [InlineData(120, 128)]
    [InlineData(128, 120)]
    public void IsOctaveEquivalent_AtThreePercent_DoesNotMatch(double a, double b)
    {
        // Act
        var result = TempoMatch.IsOctaveEquivalent(a, b, MixThresholds.BeatAlignTempoTolerance);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "stretch_ratio_within_eight_percent_is_returned")]
    public void StretchRatio_WithinMax_IsReturned()
    {
        // Act
        var result = TempoMatch.StretchRatio(128, 128 / 1.07, MixThresholds.MaxTempoStretch);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1.07, result.Value, 9);
    }

    [Theory(DisplayName = "stretch_ratio_beyond_eight_percent_is_null")]
    [InlineData(1.09)]
    [InlineData(0.91)]
    public void StretchRatio_BeyondMax_IsNull(double ratio)
    {
        // Act
        var result = TempoMatch.StretchRatio(128, 128 / ratio, MixThresholds.MaxTempoStretch);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "stretch_ratio_folds_octaves")]
    public void StretchRatio_OctaveApart_FoldsToTheClosestFactor()
    {
        // Act
        var result = TempoMatch.StretchRatio(87, 174 * 1.05, MixThresholds.MaxTempoStretch);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1 / 1.05, result.Value, 9);
    }

    [Theory(DisplayName = "stretch_ratio_below_the_minimum_is_one")]
    [InlineData(128, 128)]
    [InlineData(128, 128.05)]
    public void StretchRatio_BelowMinimum_IsOne(double outgoing, double incoming)
    {
        // Act
        var result = TempoMatch.StretchRatio(outgoing, incoming, MixThresholds.MaxTempoStretch);

        // Assert
        Assert.Equal(1d, result);
    }

    [Theory(DisplayName = "invalid_bpm_gives_null")]
    [InlineData(0, 120)]
    [InlineData(120, 0)]
    [InlineData(-1, 120)]
    public void StretchRatio_NonPositiveTempo_IsNull(double outgoing, double incoming)
    {
        // Act
        var result = TempoMatch.StretchRatio(outgoing, incoming, MixThresholds.MaxTempoStretch);

        // Assert
        Assert.Null(result);
    }
}