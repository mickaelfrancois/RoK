namespace Rok.Application.Player;

/// <summary>Bounds of the crossfade duration chosen in the options.</summary>
public static class CrossfadeDuration
{
    public const int MinSeconds = 1;

    public const int MaxSeconds = 12;

    public const int DefaultSeconds = 5;

    /// <summary>Brings a stored duration back into [<see cref="MinSeconds"/>, <see cref="MaxSeconds"/>].</summary>
    public static int Clamp(int seconds) => Math.Clamp(seconds, MinSeconds, MaxSeconds);
}