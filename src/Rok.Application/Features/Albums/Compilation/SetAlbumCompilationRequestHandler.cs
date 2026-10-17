using Rok.Application.Features.Albums.Edit;

namespace Rok.Application.Features.Albums.Compilation;

public class SetAlbumCompilationRequestHandler(AlbumCompilationService albumCompilationService) : IRequestHandler<SetAlbumCompilationRequest, Result<AlbumTagWriteOutcome>>
{
    public Task<Result<AlbumTagWriteOutcome>> Handle(SetAlbumCompilationRequest request, CancellationToken cancellationToken)
    {
        return albumCompilationService.SetAsync(request.AlbumId, request.IsCompilation, request.Progress, cancellationToken);
    }
}