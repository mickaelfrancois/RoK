namespace Rok.Application.Player.Output;

/// <summary>Turns the actual output state into the status shown in the options.</summary>
public static class AudioOutputStatusResolver
{
    public static EAudioOutputStatus Resolve(AudioOutputState state, bool isLive)
    {
        if (!state.IsOpen)
            return EAudioOutputStatus.Idle;

        if (isLive)
            return EAudioOutputStatus.Shared;

        if (state.ActualMode == EAudioOutputMode.Exclusive)
            return state.IsProcessingNeutral ? EAudioOutputStatus.ExclusiveBitPerfect : EAudioOutputStatus.ExclusiveProcessed;

        return state.FallbackReason is null ? EAudioOutputStatus.Shared : EAudioOutputStatus.SharedFallback;
    }
}