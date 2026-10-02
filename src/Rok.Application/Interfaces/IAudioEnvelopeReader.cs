using Rok.Application.Player.Mix;

namespace Rok.Application.Interfaces;

/// <summary>Edge of a track an envelope is measured on.</summary>
public enum EAudioEdge
{
    /// <summary>The first seconds of the track.</summary>
    Head,

    /// <summary>The last seconds of the track.</summary>
    Tail
}

/// <summary>Decodes a stretch of an audio file and measures its loudness profile.</summary>
public interface IAudioEnvelopeReader
{
    /// <summary>Reads the loudness envelope, and optionally a decimated mono signal, of the head or the tail of a file in a single decode.</summary>
    /// <param name="path">Path of the audio file.</param>
    /// <param name="edge">Which end of the track to measure.</param>
    /// <param name="span">Duration of the stretch to measure.</param>
    /// <param name="includeMonoSamples">Whether to also build the decimated mono signal used for tempo detection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decoded edge, or <see langword="null"/> when the file cannot be decoded.</returns>
    Task<AudioEdgeSignal?> ReadAsync(string path, EAudioEdge edge, TimeSpan span, bool includeMonoSamples, CancellationToken ct);
}