using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class ResetTrackListenCountRequest : IRequest<Result<bool>>
{
}

public class ResetTrackListenCountRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<ResetTrackListenCountRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResetTrackListenCountRequest message, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to reset track listen count.");
    }
}
