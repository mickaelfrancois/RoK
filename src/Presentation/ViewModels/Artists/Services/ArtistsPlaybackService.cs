using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.Application.Randomizer;

namespace Rok.ViewModels.Artists.Services;

public class ArtistsPlaybackService(IMediator mediator, IPlayerService playerService, ILogger<ArtistsPlaybackService> logger)
{
    public async Task PlayArtistsAsync(IEnumerable<long> artistIds)
    {
        if (!artistIds.Any())
        {
            logger.LogDebug("No track to listen.");
            return;
        }

        List<TrackDto> tracks = (await mediator.Send(new GetTracksByArtistListRequest { ArtistIds = artistIds.ToList() })).ToList();

        if (tracks.Count == 0)
        {
            logger.LogWarning("No tracks found for the selected artists.");
            return;
        }

        if (artistIds.Count() == 1)
            TracksRandomizer.Randomize(tracks);
        else
            TracksRandomizer.ArtistBalancedTrackRandomize(tracks, 0);

        playerService.LoadPlaylist(tracks);
    }
}