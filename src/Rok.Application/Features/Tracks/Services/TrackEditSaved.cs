namespace Rok.Application.Features.Tracks.Services;

/// <summary>
/// Values stored for a track after a successful edit, as read back from its file.
/// </summary>
/// <param name="Title">Title of the track.</param>
/// <param name="TrackNumber">Track number, 0 when absent.</param>
/// <param name="Disc">Disc number, null when absent.</param>
/// <param name="Composers">Composers, null when absent.</param>
/// <param name="Bpm">Beats per minute, null when absent.</param>
/// <param name="FileDate">Last write time of the file after the edit.</param>
/// <param name="Size">Size in bytes of the file after the edit.</param>
public sealed record TrackEditSaved(string Title, int TrackNumber, int? Disc, string? Composers, int? Bpm, DateTime FileDate, long Size);