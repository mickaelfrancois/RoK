namespace Rok.Application.Player.Mix;

/// <summary>Cues found at the start of a track.</summary>
/// <param name="MusicStartSeconds">Position where the music starts (0 when there is no leading silence).</param>
/// <param name="Beats">Beat grid of the intro, or null when no tempo is known.</param>
public sealed record IntroCues(double MusicStartSeconds, BeatGrid? Beats = null);