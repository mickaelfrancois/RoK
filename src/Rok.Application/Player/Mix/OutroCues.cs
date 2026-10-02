namespace Rok.Application.Player.Mix;

/// <summary>Cues found at the end of a track.</summary>
/// <param name="MusicEndSeconds">Position where the music ends (the track length when there is no trailing silence).</param>
/// <param name="FadeOutSeconds">Duration of a natural fade-out ending the track, or 0 when the ending is abrupt.</param>
/// <param name="Beats">Beat grid of the outro, or null when no tempo is known.</param>
/// <param name="MixPoint">Best bar boundary to start a mix on, whatever its score, or null when none was found.</param>
public sealed record OutroCues(double MusicEndSeconds, double FadeOutSeconds, BeatGrid? Beats = null, MixPoint? MixPoint = null);