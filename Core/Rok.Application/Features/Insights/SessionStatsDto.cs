namespace Rok.Application.Features.Insights.Query;

public record SessionStatsDto
{
    public long MaxDurationSeconds { get; init; }
    public double AverageTracksPerSession { get; init; }
    public double NocturnalSessionPercentage { get; init; }
    public int MostCommonStartHour { get; init; } = -1;
    public IntenseSessionDto? MostIntenseSession { get; init; }
    public int MostActiveDayOfWeek { get; init; } = -1;
    public int MostActiveDaySessionCount { get; init; }
}
