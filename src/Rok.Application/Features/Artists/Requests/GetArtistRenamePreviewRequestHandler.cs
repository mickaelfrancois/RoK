using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Requests;

public class GetArtistRenamePreviewRequestHandler(ArtistRenameService artistRenameService) : IRequestHandler<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>
{
    public Task<Result<ArtistRenamePreview>> Handle(GetArtistRenamePreviewRequest request, CancellationToken cancellationToken)
    {
        return artistRenameService.PreviewAsync(request.ArtistId, request.NewName, cancellationToken);
    }
}