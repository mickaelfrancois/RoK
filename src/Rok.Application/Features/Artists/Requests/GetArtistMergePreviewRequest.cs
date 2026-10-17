using Rok.Application.Features.Artists.Merge;

namespace Rok.Application.Features.Artists.Requests;

/// <summary>
/// Tells what merging two artists would do, without changing anything.
/// </summary>
/// <param name="sourceId">Id of the artist that disappears.</param>
/// <param name="targetId">Id of the artist that stays.</param>
public class GetArtistMergePreviewRequest(long sourceId, long targetId) : IRequest<Result<ArtistMergePreview>>
{
    public long SourceId { get; } = sourceId;

    public long TargetId { get; } = targetId;
}