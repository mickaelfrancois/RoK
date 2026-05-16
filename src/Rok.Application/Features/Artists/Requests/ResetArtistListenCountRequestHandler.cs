using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class ResetArtistListenCountRequest : IRequest<Result<bool>>
{
}

public class ResetArtistListenCountRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<ResetArtistListenCountRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResetArtistListenCountRequest message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("artist.listen_count_reset_failed", "Failed to reset artist listen count."));
    }
}
