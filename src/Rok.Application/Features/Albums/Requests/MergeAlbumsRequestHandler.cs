using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;

namespace Rok.Application.Features.Albums.Requests;

public class MergeAlbumsRequestHandler(AlbumMergeService albumMergeService) : IRequestHandler<MergeAlbumsRequest, Result<AlbumTagWriteOutcome>>
{
    public Task<Result<AlbumTagWriteOutcome>> Handle(MergeAlbumsRequest request, CancellationToken cancellationToken)
    {
        return albumMergeService.MergeAsync(request.SourceId, request.TargetId, request.ShiftDiscs, request.Progress, cancellationToken);
    }
}