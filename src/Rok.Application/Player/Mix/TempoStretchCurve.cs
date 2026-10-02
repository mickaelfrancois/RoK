namespace Rok.Application.Player.Mix;

/// <summary>Tempo of the incoming track over a stretched Mix crossfade and the return that follows it.</summary>
public static class TempoStretchCurve
{
    /// <summary>
    /// Tempo factor <paramref name="elapsedSeconds"/> after the start of the mix: <paramref name="ratio"/> during the
    /// overlap, then a linear return to 1 over <paramref name="returnSeconds"/>, then 1.
    /// </summary>
    public static double TempoAt(double ratio, double overlapSeconds, double returnSeconds, double elapsedSeconds)
    {
        if (elapsedSeconds < overlapSeconds)
            return ratio;

        if (returnSeconds <= 0 || elapsedSeconds >= overlapSeconds + returnSeconds)
            return 1;

        var fraction = (elapsedSeconds - overlapSeconds) / returnSeconds;

        return ratio + (1 - ratio) * fraction;
    }

    /// <summary>Tells whether the tempo is back to its original value <paramref name="elapsedSeconds"/> after the start.</summary>
    public static bool IsComplete(double overlapSeconds, double returnSeconds, double elapsedSeconds) =>
        elapsedSeconds >= overlapSeconds + Math.Max(0, returnSeconds);

    /// <summary>Duration of the return to the original tempo: <see cref="MixThresholds.TempoReturnBars"/> bars of the incoming track.</summary>
    public static double ReturnSeconds(double incomingBpm)
    {
        if (incomingBpm <= 0)
            return 0;

        return MixThresholds.TempoReturnBars * MixThresholds.BeatsPerBar * 60 / incomingBpm;
    }

    /// <summary>Keeps a ratio inside the planned range; a value that is not finite or not positive gives 1.</summary>
    public static double ClampRatio(double ratio)
    {
        if (!double.IsFinite(ratio) || ratio <= 0)
            return 1;

        return Math.Clamp(ratio, 1 - MixThresholds.MaxTempoStretch, 1 + MixThresholds.MaxTempoStretch);
    }
}