namespace Rok.Application.Features.Insights;

public class AlbumInsightDto
{
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int LongSessionCount { get; set; }
    public int PeakHour { get; set; } = -1;
    public string DominantTitle { get; set; } = string.Empty;

    public string PeakHourRange => PeakHour >= 0
        ? $"{PeakHour:D2}h - {(PeakHour + 3) % 24:D2}h"
        : string.Empty;
}
