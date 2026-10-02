namespace Rok.Application.Player.Mix;

/// <summary>What a single decode of a track edge produced: the loudness envelope and, when requested, a low-rate mono signal.</summary>
/// <param name="Envelope">Loudness envelope of the edge.</param>
/// <param name="MonoSamples">Decimated mono samples of the same stretch, or <see langword="null"/> when they were not requested.</param>
/// <param name="MonoSampleRate">Sample rate of <paramref name="MonoSamples"/>, in Hz (0 when there are none).</param>
/// <param name="StartSeconds">Position of the first decoded sample in the track, in seconds.</param>
public sealed record AudioEdgeSignal(RmsEnvelope Envelope, float[]? MonoSamples, int MonoSampleRate, double StartSeconds);