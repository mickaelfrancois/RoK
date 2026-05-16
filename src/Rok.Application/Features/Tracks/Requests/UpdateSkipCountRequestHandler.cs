using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class UpdateSkipCountRequest : IRequest<Result<bool>>
{
    public long TrackId { get; set; }
}


internal class UpdateSkipCountRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<UpdateSkipCountRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateSkipCountRequest request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateSkipCountAsync(request.TrackId);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("track.skip_count_update_failed", "Failed to update skip count."));
    }
}