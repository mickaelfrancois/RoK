using System.Globalization;

namespace Rok.ViewModels.Player.Services;

/// <summary>Formats the elapsed time and the duration shown by the player.</summary>
public static class PlaybackTimeFormatter
{
    /// <summary>
    /// Formats <paramref name="value"/> as <c>mm:ss</c>, or as <c>h:mm:ss</c> when <paramref name="value"/> or
    /// <paramref name="reference"/> lasts one hour or more, so the elapsed time and the duration of a track share
    /// the same format and no hour is ever dropped.
    /// </summary>
    public static string Format(TimeSpan value, TimeSpan reference)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        if (value.TotalHours < 1 && reference.TotalHours < 1)
            return value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

        int hours = (int)value.TotalHours;

        return string.Create(CultureInfo.InvariantCulture, $"{hours}:{value.Minutes:00}:{value.Seconds:00}");
    }
}