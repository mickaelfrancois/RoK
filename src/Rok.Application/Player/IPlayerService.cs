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

    bool IsLoopingEnabled { get; set; }

    bool IsMuted { get; set; }

    EPlaybackState PlaybackState { get; }

    List<TrackDto> Playlist { get; }

    double Position { get; set; }

    double Volume { get; set; }

    void AddTracksToPlaylist(List<TrackDto> tracks);

    void InsertTracksToPlaylist(List<TrackDto> tracks, int? index = null);

    void InitEvents();

    void LoadPlaylist(List<TrackDto> tracks, TrackDto? startTrack = null);

    void Next();

    void Skip();

    void Pause();

    void Play();

    void Previous();

    void Start(TrackDto? startTrack = null);

    void Stop(bool firePlaybackStateChange);

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

    void HandleMediaControlCommand(MediaControlCommandMessage message);

    void PlayRadioStation(RadioStationDto station);
}