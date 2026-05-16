using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class UpdateTrackLastListenRequest(long id) : IRequest<Result<bool>>
{
    public long TrackId { get; init; } = id;
}


internal class UpdateTrackLastListenRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<UpdateTrackLastListenRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateTrackLastListenRequest request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateLastListenAsync(request.TrackId);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("track.last_listen_update_failed", "Failed to update last listen."));
    }
}
