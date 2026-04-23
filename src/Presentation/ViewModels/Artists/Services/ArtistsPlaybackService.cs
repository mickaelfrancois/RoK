using Rok.Application.Features.Tracks.Query;
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

        List<TrackDto> tracks = (await mediator.SendMessageAsync(new GetTracksByArtistListQuery { ArtistIds = artistIds.ToList() })).ToList();

        if (artistIds.Count() == 1)
            TracksRandomizer.Randomize(tracks);
        else
            TracksRandomizer.ArtistBalancedTrackRandomize(tracks, 0);

        playerService.LoadPlaylist(tracks);
    }
}