using System.Globalization;

namespace Rok.ViewModels.Listening.Services;

public static class ListeningQueueSummaryFormatter
{
    private const string Separator = " · ";

    public static string Format(ListeningQueueSummary summary, ListeningSummaryLabels labels, CultureInfo culture)
    {
        string overview = FormatOverview(summary, labels, culture);

        if (overview.Length == 0)
        {
            return string.Empty;
        }

        string endTime = FormatEndTime(summary, labels, culture);

        return endTime.Length == 0 ? overview : overview + Separator + endTime;
    }

    public static string FormatOverview(ListeningQueueSummary summary, ListeningSummaryLabels labels, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(culture);

        if (summary.TrackCount <= 0)
        {
            return string.Empty;
        }

        string count = string.Format(culture, summary.TrackCount == 1 ? labels.TrackSingular : labels.TrackPlural, summary.TrackCount);

        return summary.TotalSeconds > 0
            ? count + Separator + FormatDuration(summary.TotalSeconds, labels, culture)
            : count;
    }

    public static string FormatEndTime(ListeningQueueSummary summary, ListeningSummaryLabels labels, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(culture);

        if (summary.TrackCount <= 0 || summary.EndTime is not { } endTime)
        {
            return string.Empty;
        }

        return string.Format(culture, labels.EndsAt, endTime.ToString("t", culture));
    }

    private static string FormatDuration(long totalSeconds, ListeningSummaryLabels labels, CultureInfo culture)
    {
        var totalMinutes = Math.Max(1L, (long)Math.Round(totalSeconds / 60d, MidpointRounding.AwayFromZero));
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours == 0)
        {
            return string.Format(culture, labels.Minutes, minutes);
        }

        return minutes == 0
            ? string.Format(culture, labels.Hours, hours)
            : string.Format(culture, labels.HoursMinutes, hours, minutes);
    }
}