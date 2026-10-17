using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Requests;

/// <summary>
/// Merges an artist into another one, in the database and in the tags of its files. The kept artist keeps its id.
/// </summary>
/// <param name="sourceId">Id of the artist that disappears.</param>
/// <param name="targetId">Id of the artist that stays.</param>
public class MergeArtistsRequest(long sourceId, long targetId) : IRequest<Result<ArtistMergeOutcome>>
{
    public long SourceId { get; } = sourceId;

    public long TargetId { get; } = targetId;

    /// <summary>
    /// Receives the progress of the file writing.
    /// </summary>
    public IProgress<ArtistRenameProgress>? Progress { get; init; }
}