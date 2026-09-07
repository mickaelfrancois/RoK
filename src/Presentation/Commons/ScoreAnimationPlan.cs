namespace Rok.Commons;

/// <summary>
/// Describes the feedback gesture played on a rating row when its score changes.
/// Amplitudes are calibrated for the whole star row, not for a single star.
/// </summary>
/// <param name="ScaleTo">Peak scale factor of the row.</param>
/// <param name="WobbleDegrees">Peak rotation of the row, in degrees. Zero disables the wobble.</param>
/// <param name="FlashSelectedColor">Whether the selected star color overshoots before settling back.</param>
/// <param name="Duration">Total duration of the gesture.</param>
public sealed record ScoreAnimationPlan(double ScaleTo, double WobbleDegrees, bool FlashSelectedColor, TimeSpan Duration)
{
    /// <summary>
    /// Builds the gesture for a score transition, or <see langword="null"/> when nothing should play.
    /// </summary>
    /// <param name="previousScore">Score before the change. A value of zero or less means unrated.</param>
    /// <param name="newScore">Score after the change. A value of zero or less means unrated.</param>
    public static ScoreAnimationPlan? For(int previousScore, int newScore)
    {
        if (Normalize(newScore) == Normalize(previousScore))
            return null;

        if (Normalize(newScore) == Unrated)
            return new ScoreAnimationPlan(0.92, 0, false, TimeSpan.FromMilliseconds(140));

        return Normalize(newScore) switch
        {
            1 or 2 => new ScoreAnimationPlan(1.06, 0, false, TimeSpan.FromMilliseconds(120)),
            3 => new ScoreAnimationPlan(1.10, 0, true, TimeSpan.FromMilliseconds(160)),
            _ => new ScoreAnimationPlan(1.16, 3, true, TimeSpan.FromMilliseconds(200)),
        };
    }

    private const int Unrated = 0;

    private static int Normalize(int score) => score < Unrated ? Unrated : score;
}