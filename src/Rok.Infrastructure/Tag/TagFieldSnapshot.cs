using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// Raw editable fields read from a file, compared before and after a tag write.
/// </summary>
/// <param name="Title">Title as read.</param>
/// <param name="Track">Track number.</param>
/// <param name="TrackCount">Track total.</param>
/// <param name="Disc">Disc number.</param>
/// <param name="DiscCount">Disc total.</param>
/// <param name="Bpm">Beats per minute.</param>
/// <param name="Composers">Composers in canonical form.</param>
/// <param name="AlbumArtists">Album artists joined by a separator that cannot occur in a name.</param>
/// <param name="Performers">Performers joined by a separator that cannot occur in a name.</param>
/// <param name="ArtistSource">Field that defines the artist.</param>
/// <param name="Album">Album as read.</param>
/// <param name="Genres">Genres joined by a separator that cannot occur in a name.</param>
/// <param name="Year">Year, 0 when absent.</param>
internal sealed record TagFieldSnapshot(
    string? Title,
    uint Track,
    uint TrackCount,
    uint Disc,
    uint DiscCount,
    uint Bpm,
    string Composers,
    string AlbumArtists,
    string Performers,
    EArtistSource ArtistSource,
    string? Album,
    string Genres,
    uint Year)
{
    /// <summary>
    /// Compilation flag of the file (TCMP, COMPILATION or cpil on any tag).
    /// </summary>
    public bool IsCompilation { get; init; }

    /// <summary>
    /// Gives the snapshot a verified copy must have once the changes are applied.
    /// </summary>
    /// <param name="changes">Changes written to the copy.</param>
    /// <returns>The expected snapshot.</returns>
    public TagFieldSnapshot WithChanges(TagChanges changes)
    {
        string albumArtists = AlbumArtists;
        string performers = Performers;

        if (changes.Artist is not null)
        {
            if (ArtistSource == EArtistSource.AlbumArtist)
                albumArtists = TagFieldReader.ReplaceFirstJoinedArtistValue(AlbumArtists, changes.Artist);
            else
                performers = TagFieldReader.ReplaceFirstJoinedArtistValue(Performers, changes.Artist);
        }

        string genres = changes.Genre is null ? Genres : TagFieldReader.ReplaceFirstJoinedArtistValue(Genres, changes.Genre);

        return this with
        {
            Album = changes.Album ?? Album,
            Genres = genres,
            Year = (uint?)changes.Year ?? Year,
            IsCompilation = changes.IsCompilation ?? IsCompilation,
            AlbumArtists = albumArtists,
            Performers = performers,
            Title = changes.Title ?? Title,
            Track = (uint?)changes.TrackNumber ?? Track,
            Disc = (uint?)changes.Disc ?? Disc,
            Bpm = (uint?)changes.Bpm ?? Bpm,
            Composers = changes.Composers is null ? Composers : TagFieldReader.CanonicalComposers(changes.Composers)
        };
    }
}