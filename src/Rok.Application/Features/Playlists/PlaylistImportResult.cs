namespace Rok.Application.Features.Playlists;

public enum PlaylistImportStatus
{
    Imported,
    Skipped
}

public sealed record PlaylistImportResult(
    PlaylistImportStatus Status,
    long? PlaylistId,
    string? FinalName,
    int MatchedCount,
    int IgnoredCount);