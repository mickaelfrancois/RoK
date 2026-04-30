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

    void UpdateTrackInfo(TrackDto track, string? coverPath);

    void UpdateTimeline(TimeSpan position, TimeSpan duration);
}
