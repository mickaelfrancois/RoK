using Rok.Application.Player.Output;

namespace Rok.Services;

/// <summary>Resource keys of the texts describing the audio output.</summary>
public static class AudioOutputTextKeys
{
    public static string Status(EAudioOutputStatus status) => status switch
    {
        EAudioOutputStatus.Shared => "OutputStatusShared",
        EAudioOutputStatus.ExclusiveBitPerfect => "OutputStatusExclusiveBitPerfect",
        EAudioOutputStatus.ExclusiveProcessed => "OutputStatusExclusiveProcessed",
        EAudioOutputStatus.SharedFallback => "OutputStatusSharedFallback",
        _ => "OutputStatusIdle"
    };

    public static string FallbackToolTip(EExclusiveFallbackReason reason) => reason switch
    {
        EExclusiveFallbackReason.FormatRefused => "PlayerOutputFallbackFormatRefused",
        EExclusiveFallbackReason.DeviceBusy => "PlayerOutputFallbackDeviceBusy",
        EExclusiveFallbackReason.ExclusiveDisabled => "PlayerOutputFallbackExclusiveDisabled",
        _ => "PlayerOutputFallbackOther"
    };
}