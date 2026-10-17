using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Requests;

/// <summary>
/// Merges an album into another one, in the database and in the tags of its files. The kept album keeps its id.
/// </summary>
/// <param name="sourceId">Id of the album that disappears.</param>
/// <param name="targetId">Id of the album that stays.</param>
/// <param name="shiftDiscs">True to move the discs of the merged album after those of the kept one when their numbers collide.</param>
public class MergeAlbumsRequest(long sourceId, long targetId, bool shiftDiscs) : IRequest<Result<AlbumTagWriteOutcome>>
{
    public long SourceId { get; } = sourceId;

    public long TargetId { get; } = targetId;

    public bool ShiftDiscs { get; } = shiftDiscs;

    /// <summary>
    /// Receives the progress of the file writing.
    /// </summary>
    public IProgress<ArtistRenameProgress>? Progress { get; init; }
}