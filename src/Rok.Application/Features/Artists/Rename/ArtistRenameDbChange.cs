namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Everything the database must record when an artist is renamed, applied in one transaction.
/// </summary>
/// <param name="ArtistId">Id of the renamed artist; it never changes.</param>
/// <param name="OldName">Name before the rename (kept as <c>renamePendingFrom</c> until the tags are written).</param>
/// <param name="NewName">Name after the rename.</param>
/// <param name="Criteria">Rewritten smart playlist criteria; only the playlists that changed.</param>
public sealed record ArtistRenameDbChange(long ArtistId, string OldName, string NewName, IReadOnlyList<PlaylistCriteriaChange> Criteria);

/// <summary>
/// New serialized criteria of one smart playlist.
/// </summary>
public sealed record PlaylistCriteriaChange(long PlaylistId, string GroupsJson);