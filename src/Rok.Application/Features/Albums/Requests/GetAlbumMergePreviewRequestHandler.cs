using Rok.Application.Features.Albums.Merge;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumMergePreviewRequestHandler(AlbumMergeService albumMergeService) : IRequestHandler<GetAlbumMergePreviewRequest, Result<AlbumMergePreview>>
{
    public Task<Result<AlbumMergePreview>> Handle(GetAlbumMergePreviewRequest request, CancellationToken cancellationToken)
    {
        return albumMergeService.PreviewAsync(request.SourceId, request.TargetId, cancellationToken);
    }
}