using System.Globalization;

namespace Rok.ViewModels.Player.Services;

/// <summary>
/// Builds the short remaining-time text shown on the sleep timer button of the player bar.
/// </summary>
public static class SleepTimerBadgeFormatter
{
    private const int SecondsPerMinute = 60;

    /// <summary>
    /// Formats the remaining sleep time: whole minutes (rounded up) from one minute, seconds below.
    /// </summary>
    /// <param name="isActive">Whether the sleep timer is running.</param>
    /// <param name="remainingSeconds">The remaining time in seconds; negative values are treated as zero.</param>
    /// <param name="minutesFormat">Composite format receiving the minutes, for example "{0} min".</param>
    /// <param name="secondsFormat">Composite format receiving the seconds, for example "{0} s".</param>
    /// <returns>The badge text, or an empty string when the timer is inactive.</returns>
    public static string Format(bool isActive, int remainingSeconds, string minutesFormat, string secondsFormat)
    {
        if (!isActive)
            return string.Empty;

        int seconds = Math.Max(remainingSeconds, 0);

        if (seconds < SecondsPerMinute)
            return string.Format(CultureInfo.CurrentCulture, secondsFormat, seconds);

        int minutes = (int)Math.Ceiling(seconds / (double)SecondsPerMinute);

        return string.Format(CultureInfo.CurrentCulture, minutesFormat, minutes);
    }
}