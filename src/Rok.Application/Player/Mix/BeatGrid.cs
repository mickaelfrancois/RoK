using Rok.Domain.Enums;

namespace Rok.Application.Player.Mix;

/// <summary>Regular beat positions of a stretch of a track.</summary>
/// <param name="Bpm">Tempo in beats per minute.</param>
/// <param name="FirstBeatSeconds">Absolute position in the track of the first beat of the analysed window.</param>
/// <param name="Confidence">Confidence of the tempo, between 0 and 1.</param>
/// <param name="Source">Where the tempo comes from.</param>
public sealed record BeatGrid(double Bpm, double FirstBeatSeconds, double Confidence, BpmSource Source);