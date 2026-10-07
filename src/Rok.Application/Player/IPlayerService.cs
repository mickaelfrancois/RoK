using Rok.Application.Dto;
using Rok.Application.Messages;

namespace Rok.Application.Player;

public interface IPlayerService
{
    bool CanNext { get; }

    bool CanPrevious { get; }

    bool CanSeek { get; set; }

    TrackDto? CurrentTrack { get; }

    EPlaybackMode Mode { get; }

    RadioStationDto? CurrentStation { get; }

    string? CurrentStreamTitle { get; }

    bool IsBuffering { get; }

    /// <summary>
    /// Repeat mode. <see cref="ERepeatMode.All"/> loops the queue; <see cref="ERepeatMode.One"/> restarts the current track
    /// when it ends naturally (no gapless, crossfade or mix transition is prepared), while manual next/previous
    /// behave as with <see cref="ERepeatMode.Off"/>.
    /// </summary>
    ERepeatMode RepeatMode { get; set; }

    /// <summary>
    /// Reversible shuffle. Enabling shuffles the upcoming tracks and remembers the original order;
    /// disabling restores it around the current track without reloading it.
    /// </summary>
    bool IsShuffleEnabled { get; set; }

    bool IsMuted { get; set; }

    EPlaybackState PlaybackState { get; }

    List<TrackDto> Playlist { get; }

    double Position { get; set; }

    double Volume { get; set; }

    void AddTracksToPlaylist(List<TrackDto> tracks);

    /// <summary>
    /// Inserts <paramref name="tracks"/> at <paramref name="index"/>, or right after the current track when it is null.
    /// Starts playback when the queue was empty.
    /// </summary>
    void InsertTracksToPlaylist(List<TrackDto> tracks, int? index = null);

    void InitEvents();

    void LoadPlaylist(List<TrackDto> tracks, TrackDto? startTrack = null);

    void Next();

    void Skip();

    void Pause();

    void Play();

    /// <summary>
    /// Restarts the current track when it has played for more than three seconds, otherwise moves to the previous track.
    /// </summary>
    void Previous();

    void Start(TrackDto? startTrack = null);

    void Stop(bool firePlaybackStateChange);

    /// <summary>
    /// One-off reshuffle of the upcoming tracks. Does not enable <see cref="IsShuffleEnabled"/>.
    /// </summary>
    void ShuffleTracks();

    List<TrackDto> GetQueue();

    /// <summary>Counts the upcoming tracks (queued after the current one) matching the given track id, without mutating the queue.</summary>
    int CountUpcomingByTrack(long trackId);

    /// <summary>Counts the upcoming tracks (queued after the current one) belonging to the given album, without mutating the queue.</summary>
    int CountUpcomingByAlbum(long albumId);

    /// <summary>Counts the upcoming tracks (queued after the current one) belonging to the given artist, without mutating the queue.</summary>
    int CountUpcomingByArtist(long artistId);

    /// <summary>Counts the upcoming tracks (queued after the current one) belonging to the given genre, without mutating the queue.</summary>
    int CountUpcomingByGenre(long genreId);

    /// <summary>Removes the upcoming tracks matching the given track id. The current and already-played tracks are never removed. Returns the number removed.</summary>
    int RemoveUpcomingByTrack(long trackId);

    /// <summary>Removes the upcoming tracks belonging to the given album. The current and already-played tracks are never removed. Returns the number removed.</summary>
    int RemoveUpcomingByAlbum(long albumId);

    /// <summary>Removes the upcoming tracks belonging to the given artist. The current and already-played tracks are never removed. Returns the number removed.</summary>
    int RemoveUpcomingByArtist(long artistId);

    /// <summary>Removes the upcoming tracks belonging to the given genre. The current and already-played tracks are never removed. Returns the number removed.</summary>
    int RemoveUpcomingByGenre(long genreId);

    /// <summary>
    /// Moves the upcoming track at <paramref name="fromIndex"/> so it ends at <paramref name="toIndex"/> (RemoveAt + Insert semantics).
    /// The current and already-played tracks never move: the call is refused when <paramref name="fromIndex"/> is not upcoming,
    /// and <paramref name="toIndex"/> is clamped to the upcoming range. Returns false when nothing moved.
    /// </summary>
    bool MoveUpcoming(int fromIndex, int toIndex);

    /// <summary>Number of tracks queued after the current one. 0 in radio mode.</summary>
    int UpcomingCount { get; }

    /// <summary>Removes every track after the current one; the current and already-played tracks are kept and playback is not interrupted. Returns the number removed.</summary>
    int ClearUpcoming();

    void HandleMediaControlCommand(MediaControlCommandMessage message);

    void PlayRadioStation(RadioStationDto station);
}