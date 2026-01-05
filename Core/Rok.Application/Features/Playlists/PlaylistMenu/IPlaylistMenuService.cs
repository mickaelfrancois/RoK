namespace Rok.Application.Features.Playlists.PlaylistMenu;

public interface IPlaylistMenuService
{
    event EventHandler PlaylistsChanged;

    Task<IEnumerable<PlaylistMenuItem>> GetPlaylistMenuItemsAsync();

    Task AddTrackToPlaylistAsync(long playlistId, long trackId);

    Task AddAlbumToPlaylistAsync(long playlistId, long albumId);

    Task AddArtistToPlaylistAsync(long playlistId, long artistId);

    Task CreateNewPlaylistWithTrackAsync(string playlistName, long trackId);

    Task CreateNewPlaylistWithAlbumAsync(string playlistName, long albumId);

    Task CreateNewPlaylistWithArtistAsync(string playlistName, long artistId);
}
