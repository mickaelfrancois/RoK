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
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("track.listen_count_reset_failed", "Failed to reset track listen count."));
    }
}