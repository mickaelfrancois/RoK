using System.Globalization;

namespace Rok.ViewModels.Listening.Services;

public static class ListeningQueueSummaryFormatter
{
    private const string Separator = " · ";

    public static string Format(ListeningQueueSummary summary, ListeningSummaryLabels labels, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(culture);

        if (summary.TrackCount <= 0)
        {
            return string.Empty;
        }

        var segments = new List<string>(3)
        {
            string.Format(culture, summary.TrackCount == 1 ? labels.TrackSingular : labels.TrackPlural, summary.TrackCount)
        };

        if (summary.TotalSeconds > 0)
        {
            segments.Add(FormatDuration(summary.TotalSeconds, labels, culture));
        }

        if (summary.EndTime is { } endTime)
        {
            segments.Add(string.Format(culture, labels.EndsAt, endTime.ToString("t", culture)));
        }

        return string.Join(Separator, segments);
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