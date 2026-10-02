namespace Rok.Application.Player.Mix;

/// <summary>
/// Pure curve of the bass swap: the bass of the incoming track is cut until the swap position of the mix, the bass of
/// the outgoing track is cut after it, and the switch is a short ramp centred on that position (the middle of the
/// mix unless a position is given).
/// </summary>
public static class BassSwapCurve
{
    /// <summary>Duration of the switch ramp, in seconds.</summary>
    public const double RampSeconds = 0.5;

    /// <summary>Shortest mix, in seconds, on which the bass swap applies.</summary>
    public const double MinMixSeconds = 2;

    /// <summary>Corner frequency of the low shelf, in hertz.</summary>
    public const double CutoffHz = 200;

    /// <summary>Gain of the low shelf at full cut, in decibels.</summary>
    public const double AttenuationDb = -24;

    /// <summary>
    /// Returns <paramref name="requestedSeconds"/> when it is finite and leaves half a ramp on each side inside the mix,
    /// otherwise the middle of a mix of <paramref name="mixSeconds"/> seconds.
    /// </summary>
    public static double ResolveSwapAt(double? requestedSeconds, double mixSeconds)
    {
        double margin = RampSeconds / 2;

        if (requestedSeconds is { } value && double.IsFinite(value) && value >= margin && value <= mixSeconds - margin)
            return value;

        return mixSeconds / 2;
    }

    /// <summary>Tells whether the bass swap applies to a mix of <paramref name="mixSeconds"/> seconds.</summary>
    public static bool Applies(double mixSeconds) => mixSeconds >= MinMixSeconds;

    /// <summary>
    /// Returns the cut rate of the low shelf, from 0 (neutral) to 1 (full <see cref="AttenuationDb"/>), at
    /// <paramref name="elapsedSeconds"/> into a mix of <paramref name="mixSeconds"/> seconds.
    /// </summary>
    public static double Cut(EBassSwapRole role, double elapsedSeconds, double mixSeconds) =>
        Cut(role, elapsedSeconds, mixSeconds, mixSeconds / 2);

    /// <summary>
    /// Returns the cut rate of the low shelf at <paramref name="elapsedSeconds"/> into a mix of
    /// <paramref name="mixSeconds"/> seconds, the switch ramp being centred on <paramref name="swapAtSeconds"/>.
    /// </summary>
    public static double Cut(EBassSwapRole role, double elapsedSeconds, double mixSeconds, double swapAtSeconds)
    {
        if (!Applies(mixSeconds))
            return 0;

        double elapsed = Math.Clamp(elapsedSeconds, 0, mixSeconds);
        double rampStart = swapAtSeconds - (RampSeconds / 2);
        double progress = Math.Clamp((elapsed - rampStart) / RampSeconds, 0, 1);

        return role == EBassSwapRole.Incoming ? 1 - progress : progress;
    }
}