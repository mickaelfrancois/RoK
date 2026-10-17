using Rok.Application.Features.Artists.Rename;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Everything the database must record when two artists are merged, applied in one transaction.
/// </summary>
/// <param name="SourceId">Id of the artist that disappears.</param>
/// <param name="SourceName">Name of the artist that disappears.</param>
/// <param name="TargetId">Id of the artist that stays.</param>
/// <param name="TargetName">Name of the artist that stays.</param>
/// <param name="Merged">Final field values of the kept artist (see <see cref="ArtistMergeResult"/>).</param>
/// <param name="AlbumGroups">Homonym albums folded into one; empty when there is none.</param>
/// <param name="PendingFrom">Value of the pending-rename marker of the kept artist (the source name while some files still carry it); <c>null</c> when no file is left to write.</param>
/// <param name="Criteria">Rewritten smart playlist criteria; only the playlists that changed.</param>
/// <param name="ConvertedCompilationIds">Compilations whose tracks all belong to the kept artist once merged; they become normal albums of the kept artist.</param>
public sealed record ArtistMergeDbChange(
    long SourceId,
    string SourceName,
    long TargetId,
    string TargetName,
    ArtistEntity Merged,
    IReadOnlyList<AlbumMergeGroup> AlbumGroups,
    string? PendingFrom,
    IReadOnlyList<PlaylistCriteriaChange> Criteria,
    IReadOnlyList<long>? ConvertedCompilationIds = null);