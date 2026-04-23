using Rok.Application.Features.Tracks.Query;
using Rok.Application.Player;
using Rok.Application.Randomizer;

namespace Rok.ViewModels.Albums.Services;

public class AlbumsPlaybackService(IMediator mediator, IPlayerService playerService, ILogger<AlbumsPlaybackService> logger)
{
    public async Task PlayAlbumsAsync(IEnumerable<long> albumIds)
    {
        if (!albumIds.Any())
        {
            logger.LogDebug("No track to listen.");
            return;
        }

        List<TrackDto> tracks = (await mediator.SendMessageAsync(new GetTracksByAlbumListQuery { AlbumsId = albumIds.ToList() })).ToList();

        if (albumIds.Count() == 1)
            TracksRandomizer.Randomize(tracks);
        else
            TracksRandomizer.ArtistBalancedTrackRandomize(tracks, 0);

        playerService.LoadPlaylist(tracks.ToList());
    }
}