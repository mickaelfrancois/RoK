using Rok.Application.Features.Tracks.Command;

namespace Rok.ViewModels.Track.Services;

public class TrackScoreService(IMediator mediator)
{
    public async Task UpdateScoreAsync(long trackId, int score)
    {
        await mediator.SendMessageAsync(new UpdateScoreCommand(trackId, score));
        Messenger.Send(new TrackScoreUpdateMessage(trackId, score));
    }
}