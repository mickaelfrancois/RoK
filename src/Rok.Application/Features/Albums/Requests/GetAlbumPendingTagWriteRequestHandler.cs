using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumPendingTagWriteRequestHandler(IPendingTagWriteRepository pendingTagWriteRepository) : IRequestHandler<GetAlbumPendingTagWriteRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(GetAlbumPendingTagWriteRequest request, CancellationToken cancellationToken)
    {
        int count = await pendingTagWriteRepository.CountByAlbumAsync(request.AlbumId, cancellationToken);

        return Result<bool>.Ok(count > 0);
    }
}