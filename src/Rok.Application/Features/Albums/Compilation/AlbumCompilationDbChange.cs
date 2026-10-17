using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Compilation;

/// <summary>
/// Everything the database must record when the compilation status of an album is set by hand, applied in one transaction.
/// </summary>
/// <param name="AlbumId">Album whose status changes.</param>
/// <param name="IsCompilation">New compilation status.</param>
/// <param name="ArtistId">Artist the album keeps; its counters are recomputed. <c>null</c> when the album has no artist.</param>
/// <param name="GenreId">Genre of the album; its counters are recomputed.</param>
/// <param name="PendingWrites">Tracks whose file still has the flag to write; stored in the same transaction.</param>
public sealed record AlbumCompilationDbChange(
    long AlbumId,
    bool IsCompilation,
    long? ArtistId,
    long? GenreId,
    IReadOnlyList<PendingTagWrite> PendingWrites);