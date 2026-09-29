namespace Rok.Application.Player;

/// <summary>Which ReplayGain level the player applies. Values are persisted as integers in settings.json.</summary>
public enum EReplayGainMode
{
    Off = 0,
    Track = 1,
    Album = 2,
    Auto = 3
}