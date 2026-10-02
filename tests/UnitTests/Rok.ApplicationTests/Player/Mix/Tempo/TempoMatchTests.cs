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
}