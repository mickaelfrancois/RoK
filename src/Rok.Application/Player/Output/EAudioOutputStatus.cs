namespace Rok.Application.Player.Output;

/// <summary>Actual state of the audio output, as shown to the user.</summary>
public enum EAudioOutputStatus
{
    Idle = 0,
    Shared = 1,
    ExclusiveBitPerfect = 2,
    ExclusiveProcessed = 3,
    SharedFallback = 4
}