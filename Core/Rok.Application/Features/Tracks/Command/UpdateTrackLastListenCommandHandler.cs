using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Command;

public class UpdateTrackLastListenCommand(long id) : ICommand<Result<bool>>
{
    public long TrackId { get; init; } = id;
}


internal class UpdateTrackLastListenCommandHandler(ITrackRepository _trackRepository) : ICommandHandler<UpdateTrackLastListenCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateTrackLastListenCommand request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateLastListenAsync(request.TrackId);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update last listen.");
    }
}
