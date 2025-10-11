namespace Rok.Application.Features.Playlists.PlaylistMenu;

public interface IPlaylistMenuService
{
    event EventHandler PlaylistsChanged;

    Task<IEnumerable<PlaylistMenuItem>> GetPlaylistMenuItemsAsync();

    Task AddTrackToPlaylistAsync(long playlistId, long trackId);

    Task CreateNewPlaylistWithTrackAsync(string playlistName, long trackId);
}
