namespace Rok.Services.PlayerCommand;

public interface IPlayerCommandService
{
    void Play();

    void Pause();

    void Toggle();

    void Next();

    void Previous();

    void ToggleMute();

    void SetVolume(double volume);

    Task<bool> ListenPlaylistAsync(string playlistName);

    Task<bool> ListenAlbumAsync(string albumName);

    Task<bool> ListenArtistAsync(string artistName);

    Task<bool> ListenGenreAsync(string genreName);
}