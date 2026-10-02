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

    private static bool Within(double value, double reference, double tolerance) =>
        Math.Abs(value - reference) <= reference * tolerance;
}