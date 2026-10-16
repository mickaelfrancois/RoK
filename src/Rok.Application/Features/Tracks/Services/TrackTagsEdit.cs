namespace Rok.Application.Features.Tracks.Services;

/// <summary>
/// Tag fields to change on one track. A null field is left untouched.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">New title, or null to keep the current one.</param>
/// <param name="TrackNumber">New track number (positive), or null to keep the current one.</param>
/// <param name="Disc">New disc number (positive), or null to keep the current one.</param>
/// <param name="Composers">New composers, an empty string to remove them, or null to keep the current ones.</param>
/// <param name="Bpm">New beats per minute, 0 to remove it, or null to keep the current one.</param>
public sealed record TrackTagsEdit(long TrackId, string? Title = null, int? TrackNumber = null, int? Disc = null, string? Composers = null, int? Bpm = null);