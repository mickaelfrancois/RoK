using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class UpdateTrackGetLyricsLastAttemptRequest(long id) : IRequest<Result<bool>>
{
    public long TrackId { get; init; } = id;
}


internal class UpdateTrackGetLyricsLastAttemptRequestHandler(ITrackRepository _trackRepository) : IRequestHandler<UpdateTrackGetLyricsLastAttemptRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateTrackGetLyricsLastAttemptRequest request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateGetLyricsLastAttemptAsync(request.TrackId);

        if (result)
            return Result<bool>.Ok(result);
        else
            return Result<bool>.Fail(new OperationError("track.lyrics_attempt_update_failed", "Failed to update lyrics last attempt."));
    }
}
