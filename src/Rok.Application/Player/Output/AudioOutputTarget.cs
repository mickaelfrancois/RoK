namespace Rok.Application.Player.Output;

/// <summary>Output requested by the user.</summary>
/// <param name="PreferredDeviceId">Identifier of the chosen device; empty to follow the Windows default device.</param>
/// <param name="Mode">Requested sharing mode.</param>
public sealed record AudioOutputTarget(string PreferredDeviceId, EAudioOutputMode Mode)
{
    public static AudioOutputTarget Default { get; } = new(string.Empty, EAudioOutputMode.Shared);

    public bool FollowsWindowsDefault => string.IsNullOrEmpty(PreferredDeviceId);
}