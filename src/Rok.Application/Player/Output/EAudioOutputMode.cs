namespace Rok.Application.Player.Output;

/// <summary>How the audio output shares the device with other applications.</summary>
public enum EAudioOutputMode
{
    /// <summary>Goes through the Windows mixer, which may resample.</summary>
    Shared = 0,

    /// <summary>Takes the device alone and sends the file at its native rate.</summary>
    Exclusive = 1
}