using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Playlist.Services;

public class PlaylistDataLoader(IMediator mediator, ILogger<PlaylistDataLoader> logger)
{
    public async Task<PlaylistHeaderDto?> LoadPlaylistAsync(long playlistId)
    {
        Result<PlaylistHeaderDto> result = await mediator.SendMessageAsync(new GetPlaylistByIdQuery(playlistId));

        if (result.IsSuccess)
            return result.Value!;

        logger.LogError("Playlist {PlaylistId} not found", playlistId);
        return null;
    }

    public async Task<List<TrackViewModel>> LoadTracksAsync(long playlistId)
    {
        IEnumerable<TrackDto> tracks = await mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(playlistId));
        return TrackViewModelMap.CreateViewModels(tracks);
    }
}