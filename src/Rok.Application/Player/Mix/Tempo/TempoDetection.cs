namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Result of the tempo and beat detection on one window of a track.</summary>
/// <param name="Bpm">Tempo in beats per minute, or null when none was found.</param>
/// <param name="BpmConfidence">Confidence of the tempo, between 0 and 1.</param>
/// <param name="FirstBeatSeconds">Absolute position in the track of the first beat of the window, or null.</param>
/// <param name="PhaseConfidence">Confidence of the beat position, between 0 and 1.</param>
public sealed record TempoDetection(double? Bpm, double BpmConfidence, double? FirstBeatSeconds, double PhaseConfidence)
{
    /// <summary>Detection that found neither a tempo nor a beat position.</summary>
    public static TempoDetection None { get; } = new(null, 0, null, 0);
}