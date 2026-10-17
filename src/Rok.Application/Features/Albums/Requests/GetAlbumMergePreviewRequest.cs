using Rok.Application.Features.Albums.Merge;

namespace Rok.Application.Features.Albums.Requests;

/// <summary>
/// Tells what merging an album into another one would do, without changing anything.
/// </summary>
/// <param name="sourceId">Id of the album that disappears.</param>
/// <param name="targetId">Id of the album that stays.</param>
public class GetAlbumMergePreviewRequest(long sourceId, long targetId) : IRequest<Result<AlbumMergePreview>>
{
    public long SourceId { get; } = sourceId;

    public long TargetId { get; } = targetId;
}