using Rok.Application.Features.Albums.Merge;

namespace Rok.Application.Interfaces.Repositories;

public interface IAlbumMergeRepository
{
    /// <summary>
    /// In one transaction: stores the merged fields of the kept album, moves the tracks of the merged album (and their listening
    /// history) to it, gives them the artist of the kept album, shifts their discs when asked, merges tags and equalizer preset,
    /// stores the pending tag writes, deletes the merged album and recomputes the counters of the album, artists and genres
    /// involved. Everything is rolled back on failure.
    /// </summary>
    Task ApplyAsync(AlbumMergeDbChange change, CancellationToken cancellationToken = default);
}