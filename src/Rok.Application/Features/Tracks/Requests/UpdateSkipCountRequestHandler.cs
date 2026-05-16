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
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update skip count.");
    }
}
