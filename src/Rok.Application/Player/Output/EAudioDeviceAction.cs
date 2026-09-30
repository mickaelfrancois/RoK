namespace Rok.Application.Player.Output;

/// <summary>What the player does after a device event.</summary>
public enum EAudioDeviceAction
{
    None = 0,
    Reopen = 1,
    Pause = 2
}