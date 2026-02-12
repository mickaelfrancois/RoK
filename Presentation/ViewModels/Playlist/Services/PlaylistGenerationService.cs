using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Command;

namespace Rok.ViewModels.Playlist.Services;

public class PlaylistGenerationService(
    IMediator mediator,
    IPlaylistService playlistService,
    ILogger<PlaylistGenerationService> logger)
{
    public async Task<List<TrackDto>> GenerateTracksAsync(PlaylistHeaderDto playlist)
    {
        logger.LogTrace("Generate tracks for playlist '{Name}'.", playlist.Name);

        PlaylistTracksDto playlistTracks = await playlistService.GenerateAsync(playlist);

        await SaveTracksAsync(playlist.Id, playlistTracks.Tracks);

        return playlistTracks.Tracks;
    }

    private async Task SaveTracksAsync(long playlistId, List<TrackDto> tracks)
    {
        if (tracks == null || tracks.Count == 0)
            return;

        int index = 1;
        CreatePlaylistTracksCommand command = new() { PlaylistId = playlistId };

        foreach (TrackDto track in tracks)
            command.Tracks.Add(new CreatePlaylistTracksDto { TrackId = track.Id, Position = index++ });

        await mediator.SendMessageAsync(command);
    }
}