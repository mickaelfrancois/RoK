namespace Rok.Application.Features.Playlists;

public interface IPlaylistService
{
    Task<PlaylistTracksDto> GenerateAsync(PlaylistHeaderDto playlist);
}
