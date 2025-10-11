using Rok.Application.Interfaces;

namespace Rok.Application.Features.Tracks.Command;

public class UpdateTrackGetLyricsLastAttemptCommand(long id) : ICommand<Result<bool>>
{
    public long TrackId { get; init; } = id;
}


internal class UpdateTrackGetLyricsLastAttemptCommandHandler(ITrackRepository _trackRepository) : ICommandHandler<UpdateTrackGetLyricsLastAttemptCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(UpdateTrackGetLyricsLastAttemptCommand request, CancellationToken cancellationToken)
    {
        bool result = await _trackRepository.UpdateGetLyricsLastAttemptAsync(request.TrackId);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update lyrics last attempt.");
    }
}
