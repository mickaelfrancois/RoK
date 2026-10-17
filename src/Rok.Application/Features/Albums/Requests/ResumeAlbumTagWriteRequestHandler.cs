using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Features.Albums.Requests;

public class ResumeAlbumTagWriteRequestHandler(AlbumEditService albumEditService) : IRequestHandler<ResumeAlbumTagWriteRequest, Result<AlbumTagWriteOutcome>>
{
    public Task<Result<AlbumTagWriteOutcome>> Handle(ResumeAlbumTagWriteRequest request, CancellationToken cancellationToken)
    {
        return albumEditService.ResumeAsync(request.AlbumId, request.Progress, cancellationToken);
    }
}