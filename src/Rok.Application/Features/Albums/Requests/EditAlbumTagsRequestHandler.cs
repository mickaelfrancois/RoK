using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Features.Albums.Requests;

public class EditAlbumTagsRequestHandler(AlbumEditService albumEditService) : IRequestHandler<EditAlbumTagsRequest, Result<AlbumTagWriteOutcome>>
{
    public Task<Result<AlbumTagWriteOutcome>> Handle(EditAlbumTagsRequest request, CancellationToken cancellationToken)
    {
        return albumEditService.EditAsync(request.AlbumId, request.Name, request.ArtistName, request.Year, request.GenreName, request.Progress, cancellationToken);
    }
}