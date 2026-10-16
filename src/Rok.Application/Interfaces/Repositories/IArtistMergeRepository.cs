using Rok.Application.Features.Artists.Merge;

namespace Rok.Application.Interfaces.Repositories;

public interface IArtistMergeRepository
{
    /// <summary>
    /// In one transaction: stores the merged fields and the pending-rename marker on the kept artist, moves tracks, albums,
    /// listening history, tags and equalizer presets to it, folds homonym albums, repoints playlist pictures, stores the rewritten
    /// smart playlist criteria, recomputes the counters and deletes the source artist last. Nothing relies on cascade or set-null
    /// rules. Everything is rolled back on failure.
    /// </summary>
    Task ApplyAsync(ArtistMergeDbChange change, CancellationToken cancellationToken = default);
}