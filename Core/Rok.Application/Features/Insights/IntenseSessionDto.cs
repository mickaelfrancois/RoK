namespace Rok.Application.Features.Insights.Query;

public record IntenseSessionDto
{
    public long DurationSeconds { get; init; }
    public int TrackCount { get; init; }
    public string DominantGenre { get; init; } = string.Empty;
}
