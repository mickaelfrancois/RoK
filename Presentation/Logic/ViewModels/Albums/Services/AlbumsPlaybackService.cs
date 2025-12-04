using Rok.Application.Features.Tracks.Query;
using Rok.Application.Randomizer;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Albums.Services;

public class AlbumsPlaybackService(IMediator mediator, IPlayerService playerService, ILogger<AlbumsPlaybackService> logger)
{
    public async Task PlayAlbumsAsync(IEnumerable<long> albumIds)
    {
        if (!albumIds.Any())
        {
            logger.LogDebug("No track to listen.");
            return;
        }

        IEnumerable<TrackDto> tracks = await mediator
            .SendMessageAsync(new GetTracksByAlbumListQuery { AlbumsId = albumIds.ToList() });

        tracks = albumIds.Count() == 1
            ? TracksRandomizer.Randomize(tracks)
            : ArtistBalancedTrackRandomizer.Randomize(tracks);

        playerService.LoadPlaylist(tracks.ToList());
    }
}