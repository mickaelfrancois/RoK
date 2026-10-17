using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Interfaces.Repositories;

public interface IAlbumEditRepository
{
    /// <summary>
    /// In one transaction: creates the missing artist and genre, stores the name, year, artist and genre of the album, moves its
    /// tracks (and their listening history) to the new artist and genre, stores the pending tag writes and recomputes the counters
    /// of the artists and genres involved. Everything is rolled back on failure.
    /// </summary>
    Task<AlbumEditApplied> ApplyAsync(AlbumEditDbChange change, CancellationToken cancellationToken = default);
}