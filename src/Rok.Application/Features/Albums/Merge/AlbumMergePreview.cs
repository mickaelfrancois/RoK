using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// What merging two albums would do, shown before the user confirms.
/// </summary>
/// <param name="SourceId">Id of the album that disappears.</param>
/// <param name="SourceName">Name of the album that disappears.</param>
/// <param name="TargetId">Id of the album that stays.</param>
/// <param name="TargetName">Name of the album that stays; the files are rewritten with it.</param>
/// <param name="SourceArtist">Artist of the album that disappears.</param>
/// <param name="TargetArtist">Artist of the album that stays; the tracks of the merged album take it.</param>
/// <param name="ArtistChanges">True when the merged album belongs to another artist than the kept one.</param>
/// <param name="SourceTrackCount">Number of tracks of the merged album.</param>
/// <param name="TargetTrackCount">Number of tracks of the kept album.</param>
/// <param name="FileCount">Number of files that receive at least one field.</param>
/// <param name="FieldConflicts">Fields filled on both albums with different values; the kept album's value wins.</param>
/// <param name="DiscConflicts">Tracks that share a disc and a track number across the two albums.</param>
/// <param name="SuggestedDiscOffset">Discs to add to the merged tracks to remove the collisions; 0 when there is none.</param>
/// <param name="SeparateFolders">True when a track of the merged album sits in another folder than the kept album.</param>
/// <param name="OrphanArtist">Artist of the merged album that would keep no track, null otherwise.</param>
/// <param name="Blockers">Reasons the merge cannot start. Empty when it can.</param>
public sealed record AlbumMergePreview(
    long SourceId,
    string SourceName,
    long TargetId,
    string TargetName,
    string SourceArtist,
    string TargetArtist,
    bool ArtistChanges,
    int SourceTrackCount,
    int TargetTrackCount,
    int FileCount,
    IReadOnlyList<AlbumMergeFieldConflict> FieldConflicts,
    IReadOnlyList<DiscConflict> DiscConflicts,
    int SuggestedDiscOffset,
    bool SeparateFolders,
    OrphanArtistWarning? OrphanArtist,
    IReadOnlyList<ArtistRenameBlocker> Blockers);