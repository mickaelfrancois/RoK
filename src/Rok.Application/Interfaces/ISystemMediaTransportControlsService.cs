using Rok.Application.Dto;

namespace Rok.Application.Interfaces;

/// <summary>
/// Service that registers with Windows System Media Transport Controls (SMTC) to handle
/// Bluetooth headset commands (play, pause, next, previous) and display media info in the Windows UI.
/// </summary>
public interface ISystemMediaTransportControlsService : IDisposable
{
    void SetPlayerService(Player.IPlayerService playerService);

    void Initialize();

    void UpdatePlaybackState(Player.PlaybackStatus status);

    Task UpdateTrackInfoAsync(TrackDto track, string? coverPath);

    void UpdateTimeline(TimeSpan position, TimeSpan duration);

    /// <summary>
    /// Updates the SMTC display for a radio station. Disables next/previous buttons and clears the timeline.
    /// </summary>
    void UpdateRadioStation(RadioStationDto station);

    /// <summary>
    /// Updates the SMTC display with ICY stream metadata (artist/title parsed from the stream title).
    /// </summary>
    void UpdateRadioMetadata(string streamTitle);
}