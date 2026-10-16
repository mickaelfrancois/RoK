using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Interfaces.Repositories;

public interface IArtistRenameRepository
{
    /// <summary>
    /// In one transaction: sets the artist name, keeps the previous name as the pending-rename marker (an existing marker is kept),
    /// resets the API retry date, repoints the playlist pictures and stores the rewritten smart playlist criteria.
    /// Everything is rolled back on failure.
    /// </summary>
    Task ApplyAsync(ArtistRenameDbChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the pending-rename marker once every file carries the new name.
    /// </summary>
    Task<bool> CompleteAsync(long artistId, CancellationToken cancellationToken = default);
}