using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class UpdateScoreRequest(long trackId, int score) : IRequest<Result<bool>>
{
    public long TrackId { get; init; } = trackId;

    public int Score { get; init; } = score;
}


internal class UpdateScoreRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<UpdateScoreRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateScoreRequest request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateScoreAsync(request.TrackId, request.Score);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("track.score_update_failed", "Failed to update track score."));
    }
}
