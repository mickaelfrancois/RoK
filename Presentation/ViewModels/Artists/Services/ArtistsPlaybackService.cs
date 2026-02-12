using Rok.Application.Features.Tracks.Query;
using Rok.Application.Randomizer;
using Rok.Services.Player;

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

        IEnumerable<TrackDto> tracks = await mediator
            .SendMessageAsync(new GetTracksByArtistListQuery { ArtistIds = artistIds.ToList() });

        tracks = artistIds.Count() == 1
            ? TracksRandomizer.Randomize(tracks)
            : ArtistBalancedTrackRandomizer.Randomize(tracks);

        playerService.LoadPlaylist(tracks.ToList());
    }
}