using Rok.Application.Features.Tracks.Requests;

namespace Rok.ViewModels.Track.Services;

public class TrackScoreService(IMediator mediator)
{
    public async Task UpdateScoreAsync(long trackId, int score)
    {
        await mediator.Send(new UpdateScoreRequest(trackId, score));
        Messenger.Send(new TrackScoreUpdateMessage(trackId, score));
    }
}