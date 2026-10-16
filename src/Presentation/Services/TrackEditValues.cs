namespace Rok.Services;

/// <summary>The tag fields a track has now, used to seed the track edit dialog.</summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="TrackNumber">Track number, null or 0 when absent.</param>
/// <param name="Disc">Disc number, null or 0 when absent.</param>
/// <param name="Composers">Composers, null or empty when absent.</param>
/// <param name="Bpm">Beats per minute, null or 0 when absent.</param>
public sealed record TrackEditValues(long TrackId, string Title, int? TrackNumber, int? Disc, string? Composers, int? Bpm);