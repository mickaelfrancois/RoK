namespace Rok.Application.Player.Mix;

/// <summary>Cues found at the start of a track.</summary>
/// <param name="MusicStartSeconds">Position where the music starts (0 when there is no leading silence).</param>
public sealed record IntroCues(double MusicStartSeconds);