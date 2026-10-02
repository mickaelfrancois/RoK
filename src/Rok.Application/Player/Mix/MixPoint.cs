namespace Rok.Application.Player.Mix;

/// <summary>Candidate position, on a bar boundary, where a mix out of a track can start.</summary>
/// <param name="Seconds">Absolute position in the track of the bar boundary.</param>
/// <param name="Score">Quality of the candidate (novelty, dip and phrase position), between 0 and 1.5.</param>
public sealed record MixPoint(double Seconds, double Score);