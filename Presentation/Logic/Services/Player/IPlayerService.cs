using Rok.Application.Player;

namespace Rok.Logic.Services.Player;

public interface IPlayerService
{
    bool CanNext { get; }

    bool CanPrevious { get; }

    bool CanSeek { get; set; }

    TrackDto? CurrentTrack { get; }

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

    void Start(TrackDto? startTrack = null, TimeSpan? startPosition = null);

    void Stop(bool firePlaybackStateChange);

    void ShuffleTracks();
}
