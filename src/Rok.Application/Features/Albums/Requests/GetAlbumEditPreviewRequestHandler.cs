using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumEditPreviewRequestHandler(AlbumEditService albumEditService) : IRequestHandler<GetAlbumEditPreviewRequest, Result<AlbumEditPreview>>
{
    public Task<Result<AlbumEditPreview>> Handle(GetAlbumEditPreviewRequest request, CancellationToken cancellationToken)
    {
        return albumEditService.PreviewAsync(request.AlbumId, request.Name, request.ArtistName, request.Year, request.GenreName, cancellationToken);
    }
}