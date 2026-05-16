using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class UpdateAlbumGetMetaDataLastAttemptRequest(long id) : IRequest<Result<bool>>
{
    public long AlbumId { get; init; } = id;
}


internal class UpdateAlbumGetMetaDataLastAttemptRequestHandler(IAlbumRepository _repository) : IRequestHandler<UpdateAlbumGetMetaDataLastAttemptRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateAlbumGetMetaDataLastAttemptRequest request, CancellationToken cancellationToken)
    {
        bool result = await _repository.UpdateGetMetaDataLastAttemptAsync(request.AlbumId);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("album.meta_attempt_update_failed", "Failed to update meta last attempt."));
    }
}
