namespace Rok.Application.Player;

/// <summary>
/// Playback state reported to the Windows System Media Transport Controls (SMTC).
/// For the player's internal state machine, use <see cref="EPlaybackState"/> instead.
/// </summary>
public enum PlaybackStatus
{
    Playing,
    Paused,
    Stopped,
    Closed
}