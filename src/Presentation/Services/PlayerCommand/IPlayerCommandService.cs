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

    /// <summary>Moves the playhead of the current track, in seconds. Does nothing when the source cannot seek.</summary>
    void Seek(double positionSeconds);

    /// <summary>Shuffles the tracks queued after the current one.</summary>
    void Shuffle();

    /// <summary>Turns looping on or off.</summary>
    void ToggleLoop();

    /// <summary>Restarts playback at the queued track carrying the given identifier. Returns <c>false</c> when it is not queued.</summary>
    bool PlayQueuedTrack(long trackId);

    /// <summary>Rates a track from 0 to 5 and mirrors the new rating onto the queued copies. Returns <c>false</c> when the update fails.</summary>
    Task<bool> SetScoreAsync(long trackId, int score);

    /// <summary>Loads and plays a playlist by identifier. Returns <c>false</c> when it holds no track.</summary>
    Task<bool> ListenPlaylistByIdAsync(long playlistId);

    Task<bool> ListenPlaylistAsync(string playlistName);

    Task<bool> ListenAlbumAsync(string albumName);

    Task<bool> ListenArtistAsync(string artistName);

    Task<bool> ListenGenreAsync(string genreName);
}