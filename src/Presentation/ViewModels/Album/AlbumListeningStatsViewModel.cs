using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Features.Albums;

namespace Rok.ViewModels.Album;

/// <summary>
/// Presentation model of the album listening statistics panel:
/// formatted stat values, monthly sparkline bars and album listening progression.
/// </summary>
public partial class AlbumListeningStatsViewModel : ObservableObject
{
    private const double MaxBarHeight = 40;
    private const double MinVisibleBarHeight = 2;

    [ObservableProperty]
    public partial bool ShowStats { get; set; }

    [ObservableProperty]
    public partial bool ShowNeverListened { get; set; }

    [ObservableProperty]
    public partial bool ShowProgression { get; set; }

    [ObservableProperty]
    public partial string ListenCountValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DurationValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SinceValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PeakHourValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ListenedTrackCount { get; set; }

    [ObservableProperty]
    public partial int TotalTrackCount { get; set; }

    [ObservableProperty]
    public partial string ProgressionLabel { get; set; } = string.Empty;

    public ObservableCollection<MonthlyBarViewModel> MonthlyBars { get; } = [];

    public void SetStats(AlbumListeningStatsDto stats)
    {
        ShowStats = stats.CompletedListenCount > 0;
        ShowNeverListened = stats.CompletedListenCount == 0;

        ListenCountValue = stats.CompletedListenCount.ToString();
        DurationValue = FormatDuration(stats.TotalDurationPlayedSeconds);
        SinceValue = stats.FirstListenedAt?.ToLocalTime().ToString("MMMM yyyy", CultureInfo.CurrentCulture) ?? string.Empty;
        PeakHourValue = stats.PeakHourRange;

        UpdateMonthlyBars(stats.MonthlyListens);
    }

    public void SetProgression(int listenedTrackCount, int totalTrackCount)
    {
        ListenedTrackCount = listenedTrackCount;
        TotalTrackCount = totalTrackCount;
        ProgressionLabel = $"{listenedTrackCount}/{totalTrackCount}";
        ShowProgression = totalTrackCount > 0;
    }

    private void UpdateMonthlyBars(List<MonthlyListenCountDto> monthlyListens)
    {
        MonthlyBars.Clear();

        int maxCount = monthlyListens.Count > 0 ? monthlyListens.Max(m => m.Count) : 0;

        foreach (MonthlyListenCountDto month in monthlyListens)
        {
            double height = month.Count > 0
                ? Math.Max(MinVisibleBarHeight, MaxBarHeight * month.Count / maxCount)
                : MinVisibleBarHeight;

            string monthName = new DateTime(month.Year, month.Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);

            MonthlyBars.Add(new MonthlyBarViewModel
            {
                Height = height,
                Tooltip = $"{monthName} : {month.Count}"
            });
        }
    }

    private static string FormatDuration(long totalSeconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(totalSeconds);

        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours} h {time.Minutes:D2} min";

        return $"{time.Minutes} min";
    }
}

/// <summary>
/// One bar of the monthly listens sparkline.
/// </summary>
public class MonthlyBarViewModel
{
    public double Height { get; init; }

    public string Tooltip { get; init; } = string.Empty;
}
