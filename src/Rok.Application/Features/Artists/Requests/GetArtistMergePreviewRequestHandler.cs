using Rok.Application.Features.Artists.Merge;

namespace Rok.Application.Features.Artists.Requests;

public class GetArtistMergePreviewRequestHandler(ArtistMergeService artistMergeService) : IRequestHandler<GetArtistMergePreviewRequest, Result<ArtistMergePreview>>
{
    public Task<Result<ArtistMergePreview>> Handle(GetArtistMergePreviewRequest request, CancellationToken cancellationToken)
    {
        return artistMergeService.PreviewAsync(request.SourceId, request.TargetId, cancellationToken);
    }
}