using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class ResetAlbumListenCountRequest : IRequest<Result<bool>>
{
}

public class ResetAlbumListenCountRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<ResetAlbumListenCountRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResetAlbumListenCountRequest message, CancellationToken cancellationToken)
    {
        bool result = await _albumRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("album.listen_count_reset_failed", "Failed to reset album listen count."));
    }
}