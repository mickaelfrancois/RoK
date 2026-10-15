namespace Rok.Services;

/// <summary>The texts typed in the track edit dialog.</summary>
/// <param name="Title">Typed title.</param>
/// <param name="TrackNumber">Typed track number.</param>
/// <param name="Disc">Typed disc number.</param>
/// <param name="Composers">Typed composers.</param>
/// <param name="Bpm">Typed beats per minute.</param>
public sealed record TrackEditInput(string? Title, string? TrackNumber, string? Disc, string? Composers, string? Bpm);