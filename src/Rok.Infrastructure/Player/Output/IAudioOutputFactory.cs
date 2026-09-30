using NAudio.Wave;
using Rok.Application.Player.Output;

namespace Rok.Infrastructure.Player.Output;

/// <summary>Opens the audio outputs used by the music and the radio.</summary>
public interface IAudioOutputFactory
{
    /// <summary>
    /// Opens an output on the device resolved for <paramref name="target"/> and initialises it with <paramref name="source"/>.
    /// In exclusive mode the formats of <see cref="ExclusiveFormatLadder"/> are tried for <paramref name="nativeBitsPerSample"/>;
    /// when none is accepted the output opens shared and reports the reason.
    /// </summary>
    /// <exception cref="InvalidOperationException">No output device is available.</exception>
    AudioOutputHandle Open(AudioOutputTarget target, ISampleProvider source, int nativeBitsPerSample);
}