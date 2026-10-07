namespace Rok.Commons;

/// <summary>
/// Computes the width available to the track title in the playback bar.
/// </summary>
public static class PlayerTitleWidth
{
    /// <summary>
    /// Returns the maximum title width: the free space between the title and the playback buttons
    /// (kept <paramref name="gap"/> away from them), bounded by the right edge of the track column,
    /// minus the rating block when it is visible. Never smaller than <paramref name="minWidth"/>.
    /// </summary>
    /// <param name="titleLeft">Left edge of the title.</param>
    /// <param name="controlsLeft">Left edge of the playback buttons row.</param>
    /// <param name="columnRight">Right edge of the track info column.</param>
    /// <param name="scoreBlockWidth">Width taken by the rating block (0 when hidden).</param>
    /// <param name="gap">Minimum distance kept before the playback buttons.</param>
    /// <param name="minWidth">Lower bound of the result.</param>
    public static double Compute(double titleLeft, double controlsLeft, double columnRight, double scoreBlockWidth, double gap, double minWidth)
    {
        if (!double.IsFinite(titleLeft)
            || !double.IsFinite(controlsLeft)
            || !double.IsFinite(columnRight)
            || !double.IsFinite(scoreBlockWidth)
            || !double.IsFinite(gap))
            return minWidth;

        double right = Math.Min(controlsLeft - gap, columnRight);

        return Math.Max(minWidth, right - titleLeft - scoreBlockWidth);
    }
}