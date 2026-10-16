namespace Rok.Application.Tag;

/// <summary>
/// Editable tag fields as the library import reads them from a file.
/// </summary>
/// <param name="Title">Trimmed title, empty when absent.</param>
/// <param name="TrackNumber">Track number, 0 when absent.</param>
/// <param name="Disc">Disc number, null when absent.</param>
/// <param name="Composers">Trimmed composers joined by TagLib, empty when absent.</param>
/// <param name="Bpm">Beats per minute, null when absent.</param>
public sealed record TagFieldValues(string Title, int TrackNumber, int? Disc, string Composers, int? Bpm)
{
    /// <summary>
    /// Artist as the library import reads it (album artist, else performer, normalized), empty when absent.
    /// A tag write fills it only when the write changed the artist.
    /// </summary>
    public string Artist { get; init; } = "";
}