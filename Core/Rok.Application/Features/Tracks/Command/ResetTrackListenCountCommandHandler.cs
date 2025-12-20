using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Command;

public class ResetTrackListenCountCommand : ICommand<Result<bool>>
{
}

public class ResetTrackListenCountCommandHandler(ITrackRepository _trackRepository) : ICommandHandler<ResetTrackListenCountCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(ResetTrackListenCountCommand message, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.ResetListenCountAsync();

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to reset track listen count.");
    }
}
