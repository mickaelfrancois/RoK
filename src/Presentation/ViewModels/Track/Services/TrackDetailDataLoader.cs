using Rok.Application.Features.Tracks.Requests;

namespace Rok.ViewModels.Track.Services;

public class TrackDetailDataLoader(IMediator mediator, ILogger<TrackDetailDataLoader> logger)
{
    public async Task<TrackDto?> LoadTrackAsync(long trackId)
    {
        Result<TrackDto> trackResult = await mediator.Send(new GetTrackByIdRequest(trackId));

        if (trackResult.IsFailure)
        {
            logger.LogError("Failed to load track {TrackId}", trackId);
            return null;
        }

        return trackResult.Value;
    }
}