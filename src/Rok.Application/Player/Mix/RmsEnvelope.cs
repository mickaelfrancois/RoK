namespace Rok.Application.Player.Mix;

/// <summary>Loudness profile of a stretch of a track, one RMS level per fixed-size window.</summary>
/// <param name="StartSeconds">Absolute position in the track of the first window.</param>
/// <param name="WindowSeconds">Duration of each window.</param>
/// <param name="LevelsDb">RMS level of each window, in dBFS.</param>
/// <param name="TrackLengthSeconds">Total length of the track.</param>
public sealed record RmsEnvelope(double StartSeconds, double WindowSeconds, float[] LevelsDb, double TrackLengthSeconds);