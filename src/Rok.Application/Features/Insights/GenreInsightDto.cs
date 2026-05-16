namespace Rok.Application.Features.Insights;

public class GenreInsightDto
{
    public int Rank { get; set; }
    public string GenreName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int LongSessionCount { get; set; }
    public int PeakHour { get; set; } = -1;
    public string DominantTitle { get; set; } = string.Empty;
    public double PlayPercentage { get; set; }

    public string RankText => $"{Rank}.";
    public string PlayPercentageText => $"{PlayPercentage:F0}%";

    public string PeakHourRange => PeakHour >= 0
        ? $"{PeakHour:D2}h - {(PeakHour + 3) % 24:D2}h"
        : string.Empty;
}