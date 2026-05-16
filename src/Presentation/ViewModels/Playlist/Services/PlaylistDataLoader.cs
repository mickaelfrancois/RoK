using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Playlist.Services;

public class PlaylistDataLoader(IMediator mediator, ITrackViewModelFactory trackViewModelFactory, ILogger<PlaylistDataLoader> logger)
{
    public async Task<PlaylistHeaderDto?> LoadPlaylistAsync(long playlistId)
    {
        Result<PlaylistHeaderDto> result = await mediator.Send(new GetPlaylistByIdRequest(playlistId));

        if (result.IsSuccess)
            return result.Value!;

        logger.LogError("Playlist {PlaylistId} not found", playlistId);
        return null;
    }

    public async Task<List<TrackViewModel>> LoadTracksAsync(long playlistId)
    {
        IEnumerable<TrackDto> tracks = await mediator.Send(new GetTracksByPlaylistIdRequest(playlistId));
        return TrackViewModelMap.CreateViewModels(tracks, trackViewModelFactory);
    }
}