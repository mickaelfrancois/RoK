using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Command;

public class UpdateScoreCommand(long trackId, int score) : ICommand<Result<bool>>
{
    public long TrackId { get; init; } = trackId;

    public int Score { get; init; } = score;
}


internal class UpdateScoreCommandHandler(ITrackRepository _trackRepository) : ICommandHandler<UpdateScoreCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateScoreCommand request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateScoreAsync(request.TrackId, request.Score);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update track score.");
    }
}