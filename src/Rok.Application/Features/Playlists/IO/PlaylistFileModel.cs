namespace Rok.Application.Features.Playlists.IO;

public sealed record PlaylistFileModel(
    string Name,
    IReadOnlyList<PlaylistFileEntry> Entries);

public sealed record PlaylistFileEntry(
    string FilePath,
    string? Title,
    string? Artist,
    TimeSpan? Duration);