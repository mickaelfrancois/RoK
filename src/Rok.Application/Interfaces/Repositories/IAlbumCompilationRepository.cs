using Rok.Application.Features.Albums.Compilation;

namespace Rok.Application.Interfaces.Repositories;

public interface IAlbumCompilationRepository
{
    /// <summary>
    /// In one transaction: stores the compilation status of the album with the origin Manual, stores the pending tag
    /// writes and recomputes the counters of the artist and genre involved. Everything is rolled back on failure.
    /// </summary>
    Task ApplyAsync(AlbumCompilationDbChange change, CancellationToken cancellationToken = default);
}