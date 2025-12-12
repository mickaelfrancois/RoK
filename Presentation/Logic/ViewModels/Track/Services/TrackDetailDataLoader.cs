using Rok.Application.Features.Tracks.Query;

namespace Rok.Logic.ViewModels.Track.Services;

public class TrackDetailDataLoader(IMediator mediator, ILogger<TrackDetailDataLoader> logger)
{
    public async Task<TrackDto?> LoadTrackAsync(long trackId)
    {
        Result<TrackDto> trackResult = await mediator.SendMessageAsync(new GetTrackByIdQuery(trackId));

        if (trackResult.IsError)
        {
            logger.LogError("Failed to load track {TrackId}", trackId);
            return null;
        }

        return trackResult.Value!;
    }
}