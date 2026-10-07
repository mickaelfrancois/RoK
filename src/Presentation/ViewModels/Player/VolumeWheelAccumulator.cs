namespace Rok.ViewModels.Player;

/// <summary>
/// Converts mouse wheel deltas into volume steps, keeping the sub-notch remainder of high-resolution wheels.
/// </summary>
public sealed class VolumeWheelAccumulator
{
    /// <summary>Wheel delta of one standard notch.</summary>
    public const int NotchDelta = 120;

    /// <summary>Volume change, in percent, applied for one notch.</summary>
    public const int StepPercent = 5;

    private int _remainder;

    /// <summary>
    /// Accumulates a wheel delta and returns the new volume (0-100).
    /// </summary>
    /// <param name="currentVolume">The current volume, between 0 and 100.</param>
    /// <param name="wheelDelta">The wheel delta; positive when scrolling up.</param>
    /// <returns>The volume clamped to 0-100 after applying the whole notches.</returns>
    public double Apply(double currentVolume, int wheelDelta)
    {
        if (wheelDelta == 0)
            return currentVolume;

        if (Math.Sign(wheelDelta) != Math.Sign(_remainder))
            _remainder = 0;

        _remainder += wheelDelta;

        int notches = _remainder / NotchDelta;
        _remainder -= notches * NotchDelta;

        return Math.Clamp(currentVolume + (notches * StepPercent), 0, 100);
    }

    /// <summary>
    /// Converts a volume to a whole percentage between 0 and 100.
    /// </summary>
    /// <param name="volume">The volume to convert.</param>
    /// <returns>The rounded percentage.</returns>
    public static int ToPercent(double volume) =>
        (int)Math.Round(Math.Clamp(volume, 0, 100), MidpointRounding.AwayFromZero);
}