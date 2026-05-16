namespace Rok.Application.Features.Insights;

public record IntenseSessionDto
{
    public long DurationSeconds { get; init; }
    public int TrackCount { get; init; }
    public string DominantGenre { get; init; } = string.Empty;
}
