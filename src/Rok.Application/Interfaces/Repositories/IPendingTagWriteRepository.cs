using System.Data;
using Rok.Domain.Enums;

namespace Rok.Application.Interfaces.Repositories;

/// <summary>
/// Stores which tracks still have tag fields to write to their file. Rows are inserted by the edit and merge
/// transactions of the album repositories; this repository reads them and clears them as files are written.
/// </summary>
public interface IPendingTagWriteRepository
{
    /// <summary>Gets the pending rows of the tracks of an album. Rows of deleted tracks are ignored.</summary>
    Task<IReadOnlyList<PendingTagWrite>> GetByAlbumAsync(long albumId, CancellationToken cancellationToken = default);

    /// <summary>Counts the tracks of an album that have a pending row.</summary>
    Task<int> CountByAlbumAsync(long albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the fields to the row of a track (a row that exists keeps its fields, the new ones are added).
    /// Pass the transaction when one is open on the shared connection.
    /// </summary>
    Task AddAsync(long trackId, ETagWriteFields fields, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);

    /// <summary>In one transaction: stores the file date and size of the track, then deletes its pending row.</summary>
    Task CompleteAsync(long trackId, DateTime fileDate, long size, CancellationToken cancellationToken = default);

    /// <summary>Deletes the rows whose track does not exist anymore.</summary>
    Task<int> DeleteOrphansAsync(CancellationToken cancellationToken = default);
}