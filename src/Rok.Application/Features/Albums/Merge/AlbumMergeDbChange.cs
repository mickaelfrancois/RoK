using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// Everything the database must record when an album is merged into another one, applied in one transaction.
/// </summary>
/// <param name="SourceId">Album that disappears.</param>
/// <param name="TargetId">Album that stays.</param>
/// <param name="Merged">Final field values of the kept album (see <see cref="AlbumMergeResult"/>); its <c>ArtistId</c> is the artist the tracks of the merged album take.</param>
/// <param name="DiscOffset">Number of discs added to the disc number of the tracks of the merged album; <c>0</c> when they keep their numbers.</param>
/// <param name="PendingWrites">Tracks of the merged album whose file still has fields to write; stored in the same transaction.</param>
public sealed record AlbumMergeDbChange(
    long SourceId,
    long TargetId,
    AlbumEntity Merged,
    int DiscOffset,
    IReadOnlyList<PendingTagWrite> PendingWrites);