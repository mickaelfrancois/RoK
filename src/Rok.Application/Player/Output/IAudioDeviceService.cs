namespace Rok.Application.Player.Output;

/// <summary>Lists the audio output devices and reports their changes.</summary>
public interface IAudioDeviceService
{
    /// <summary>Raised off the UI thread when the active devices or the Windows default device change.</summary>
    event EventHandler<AudioDeviceSnapshot>? DevicesChanged;

    AudioDeviceSnapshot GetSnapshot();
}