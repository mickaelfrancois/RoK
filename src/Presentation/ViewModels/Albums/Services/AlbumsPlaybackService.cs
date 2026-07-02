using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.Application.Randomizer;

namespace Rok.ViewModels.Albums.Services;

public class AlbumsPlaybackService(IMediator mediator, IPlayerService playerService, ILogger<AlbumsPlaybackService> logger)
{
    public async Task PlayAlbumsAsync(IEnumerable<long> albumIds)
    {
        var ids = albumIds.ToList();

        if (ids.Count == 0)
        {
            logger.LogDebug("No track to listen.");
            return;
        }

        var tracks = ids.Count == 1
            ? (await mediator.Send(new GetTracksByAlbumIdRequest(ids[0]))).ToList()
            : (await mediator.Send(new GetTracksByAlbumListRequest { AlbumsId = ids })).ToList();

        if (tracks.Count == 0)
        {
            logger.LogWarning("No tracks found for the selected albums.");
            return;
        }

        if (ids.Count > 1)
            TracksRandomizer.ArtistBalancedTrackRandomize(tracks, 0);

        playerService.LoadPlaylist(tracks.ToList());
    }
}