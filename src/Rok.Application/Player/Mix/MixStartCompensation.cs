namespace Rok.Application.Player.Mix;

/// <summary>Compensation of the time spent opening the incoming output of a Mix crossfade.</summary>
public static class MixStartCompensation
{
    /// <summary>
    /// Computes how much incoming content to skip, and the duration and bass swap offset that keep the end of the mix
    /// and the swap on the planned outgoing bars.
    /// </summary>
    /// <param name="referenceSeconds">Outgoing position the plan was computed for; null disables the compensation.</param>
    /// <param name="outgoingPositionSeconds">Outgoing position once the incoming output is open.</param>
    /// <param name="tempoRatio">Playback-rate factor of the incoming track.</param>
    /// <param name="durationSeconds">Planned duration of the mix.</param>
    /// <param name="bassSwapAtSeconds">Planned bass swap offset from the start of the mix.</param>
    public static (double SkipSeconds, double DurationSeconds, double BassSwapAtSeconds) Compute(
        double? referenceSeconds,
        double outgoingPositionSeconds,
        double tempoRatio,
        double durationSeconds,
        double bassSwapAtSeconds)
    {
        if (referenceSeconds is null)
            return (0, durationSeconds, bassSwapAtSeconds);

        var maxLag = Math.Max(0, durationSeconds - MixThresholds.MinMixSeconds);
        var lag = Math.Clamp(outgoingPositionSeconds - referenceSeconds.Value, 0, maxLag);

        return (lag * tempoRatio, durationSeconds - lag, bassSwapAtSeconds - lag);
    }
}