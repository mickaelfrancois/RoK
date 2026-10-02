namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Comparison of tempos that tolerates octave errors.</summary>
public static class TempoMatch
{
    /// <summary>Tells whether two tempos are equal within a tolerance, or differ by a factor of two.</summary>
    /// <param name="a">First tempo, in beats per minute.</param>
    /// <param name="b">Second tempo, in beats per minute.</param>
    /// <param name="tolerance">Relative tolerance, for example 0.02 for 2 %.</param>
    public static bool IsOctaveEquivalent(double a, double b, double tolerance)
    {
        if (a <= 0 || b <= 0)
            return false;

        return Within(a, b, tolerance) || Within(a, b * 2, tolerance) || Within(a, b / 2, tolerance);
    }

    /// <summary>
    /// Playback-rate factor to apply to the incoming track so that its tempo matches the outgoing one, folding octave
    /// errors (the factor closest to 1 among tempo, double and half tempo wins).
    /// </summary>
    /// <param name="outgoingBpm">Tempo of the outgoing track, in beats per minute.</param>
    /// <param name="incomingBpm">Tempo of the incoming track, in beats per minute.</param>
    /// <param name="maxStretch">Largest accepted relative change, for example 0.08 for 8 %.</param>
    /// <returns>
    /// The factor (above 1 speeds the incoming track up); 1 when the change is under
    /// <see cref="MixThresholds.MinTempoStretch"/>; null when a tempo is not positive or the change exceeds
    /// <paramref name="maxStretch"/>.
    /// </returns>
    public static double? StretchRatio(double outgoingBpm, double incomingBpm, double maxStretch)
    {
        if (outgoingBpm <= 0 || incomingBpm <= 0)
            return null;

        var best = outgoingBpm / incomingBpm;

        foreach (var factor in new[] { 2.0, 0.5 })
        {
            var candidate = outgoingBpm * factor / incomingBpm;

            if (Math.Abs(candidate - 1) < Math.Abs(best - 1))
                best = candidate;
        }

        var change = Math.Abs(best - 1);

        if (change > maxStretch)
            return null;

        return change < MixThresholds.MinTempoStretch ? 1 : best;
    }

    private static bool Within(double value, double reference, double tolerance) =>
        Math.Abs(value - reference) <= reference * tolerance;
}