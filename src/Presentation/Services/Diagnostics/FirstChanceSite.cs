namespace Rok.Services.Diagnostics;

/// <summary>
/// Throw site of the last null-reference error seen by <see cref="FirstChanceRecorder"/>.
/// </summary>
/// <param name="AgeMs">Milliseconds elapsed since the throw.</param>
/// <param name="Frames">Innermost frames, joined with <c>" &lt; "</c>.</param>
public sealed record FirstChanceSite(long AgeMs, string Frames);