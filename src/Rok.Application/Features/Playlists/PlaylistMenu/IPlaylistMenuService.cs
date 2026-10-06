namespace Rok.Application.Features.Playlists.PlaylistMenu;

public interface IPlaylistMenuService
{
    event EventHandler PlaylistsChanged;

    Task<IEnumerable<PlaylistMenuItem>> GetPlaylistMenuItemsAsync();

    Task AddTrackToPlaylistAsync(long playlistId, long trackId);

    Task AddAlbumToPlaylistAsync(long playlistId, long albumId);

    Task AddArtistToPlaylistAsync(long playlistId, long artistId);

    Task AddArtistToCurrentListeningAsync(long artistId);

    Task AddAlbumToCurrentListeningAsync(long albumId);

    Task AddTrackToCurrentListeningAsync(long trackId);

    /// <summary>Queues the tracks of the artist right after the current track.</summary>
    Task PlayArtistNextAsync(long artistId);

    /// <summary>Queues the tracks of the album, in album order, right after the current track.</summary>
    Task PlayAlbumNextAsync(long albumId);

    /// <summary>Queues the track right after the current track.</summary>
    Task PlayTrackNextAsync(long trackId);

    Task CreateNewPlaylistWithTrackAsync(string playlistName, long trackId);

    Task CreateNewPlaylistWithAlbumAsync(string playlistName, long albumId);

    Task CreateNewPlaylistWithArtistAsync(string playlistName, long artistId);
}