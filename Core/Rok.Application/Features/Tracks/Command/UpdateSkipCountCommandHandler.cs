using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Command;

public class UpdateSkipCountCommand() : IRequest<Result<bool>>
{
    public long TrackId { get; set; }
}


internal class UpdateSkipCountCommandHandler(ITrackRepository _trackRepository) : IRequestHandler<UpdateSkipCountCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateSkipCountCommand request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateSkipCountAsync(request.TrackId);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update skip count.");
    }
}
