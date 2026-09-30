namespace Rok.Application.Player.Output;

/// <summary>Actual state of the audio output.</summary>
/// <param name="IsOpen">An output is open on a device.</param>
/// <param name="DeviceId">Identifier of the device in use, when open.</param>
/// <param name="IsOnPreferred">The device in use is the requested one (always true when following the Windows default).</param>
/// <param name="ActualMode">Sharing mode really in use.</param>
/// <param name="FallbackReason">Why exclusive was requested but the output runs shared; <c>null</c> otherwise.</param>
/// <param name="IsProcessingNeutral">Equalizer flat, ReplayGain and volume at unity: samples reach the device unchanged.</param>
public sealed record AudioOutputState(bool IsOpen, string? DeviceId, bool IsOnPreferred, EAudioOutputMode ActualMode, EExclusiveFallbackReason? FallbackReason, bool IsProcessingNeutral)
{
    public static AudioOutputState Closed { get; } = new(false, null, false, EAudioOutputMode.Shared, null, true);
}