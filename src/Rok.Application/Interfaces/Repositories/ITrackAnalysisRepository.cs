using Rok.Domain.Entities;

namespace Rok.Application.Interfaces.Repositories;

/// <summary>
/// Stores the Mix analysis of tracks. Implementations must be safe to call from any thread: each call
/// uses its own short-lived database connection.
/// </summary>
public interface ITrackAnalysisRepository
{
    /// <summary>Gets the stored analysis of a track.</summary>
    /// <param name="trackId">Identifier of the track.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The row, or <see langword="null"/> when the track was never analysed.</returns>
    Task<TrackAnalysisEntity?> GetAsync(long trackId, CancellationToken ct);

    /// <summary>Inserts or replaces the analysis of a track.</summary>
    /// <param name="entity">The row to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The track does not exist (foreign key violation).</exception>
    Task UpsertAsync(TrackAnalysisEntity entity, CancellationToken ct);
}