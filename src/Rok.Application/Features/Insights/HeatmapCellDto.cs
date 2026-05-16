namespace Rok.Application.Features.Insights;

public record HeatmapCellDto
{
    public int DayOfWeek { get; init; }
    public int Hour { get; init; }
    public int Count { get; init; }
}