namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Tempo found in an onset curve.</summary>
/// <param name="Bpm">Tempo in beats per minute, folded into the usual range.</param>
/// <param name="Confidence">Strength of the periodicity, between 0 and 1.</param>
public sealed record TempoEstimate(double Bpm, double Confidence);