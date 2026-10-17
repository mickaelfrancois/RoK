using Rok.Application.Tag;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// Single source of truth for reading the editable tag fields, shared by the import and the tag write verification.
/// </summary>
internal static class TagFieldReader
{
    private const char ArtistValueSeparator = '\u001F';

    private static readonly char[] ComposerSeparators = [';', '/'];

    /// <summary>
    /// Tells which field defines the artist: the album artist when the tag has one, otherwise the performer.
    /// This is the single rule shared by the import, the tag write and its verification.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The field that defines the artist.</returns>
    public static EArtistSource ArtistSource(TagLib.Tag tag)
    {
        return tag.FirstAlbumArtist is not null ? EArtistSource.AlbumArtist : EArtistSource.Performer;
    }

    /// <summary>
    /// Reads the artist with the normalization the library import applies.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The normalized artist, empty when absent.</returns>
    public static string ReadArtist(TagLib.Tag tag)
    {
        string? artist = ArtistSource(tag) == EArtistSource.AlbumArtist ? tag.FirstAlbumArtist : tag.FirstPerformer;

        return (artist?.Trim() ?? "").NormalizeIndexedName();
    }

    /// <summary>
    /// Reads the album with the normalization the library import applies.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The trimmed, normalized album, empty when absent.</returns>
    public static string ReadAlbum(TagLib.Tag tag)
    {
        return (tag.Album?.Trim() ?? "").NormalizeIndexedName();
    }

    /// <summary>
    /// Reads the first genre with the normalization the library import applies.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The trimmed, normalized first genre, empty when absent.</returns>
    public static string ReadGenre(TagLib.Tag tag)
    {
        return (tag.FirstGenre?.Trim() ?? "").NormalizeIndexedName();
    }

    /// <summary>
    /// Reads the year as the library import does.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The year, null when absent or not positive.</returns>
    public static int? ReadYear(TagLib.Tag tag)
    {
        return tag.Year > 0 ? (int)tag.Year : null;
    }

    /// <summary>
    /// Joins artist values into the canonical text kept in a snapshot.
    /// </summary>
    /// <param name="values">Values of an artist field.</param>
    /// <returns>The joined values, empty when none.</returns>
    public static string JoinArtistValues(IEnumerable<string?> values)
    {
        return string.Join(ArtistValueSeparator, values);
    }

    /// <summary>
    /// Replaces the first value of an artist field and keeps the other ones.
    /// </summary>
    /// <param name="values">Current values, possibly empty.</param>
    /// <param name="first">New first value.</param>
    /// <returns>The new values.</returns>
    public static string[] ReplaceFirstArtistValue(string[]? values, string first)
    {
        if (values is null || values.Length == 0)
            return [first];

        string[] result = [.. values];
        result[0] = first;

        return result;
    }

    /// <summary>
    /// Replaces the first value of a joined artist field, as <see cref="ReplaceFirstArtistValue"/> does.
    /// </summary>
    /// <param name="joined">Values joined by <see cref="JoinArtistValues"/>.</param>
    /// <param name="first">New first value.</param>
    /// <returns>The joined new values.</returns>
    public static string ReplaceFirstJoinedArtistValue(string joined, string first)
    {
        string[] values = joined.Length == 0 ? [] : joined.Split(ArtistValueSeparator);

        return JoinArtistValues(ReplaceFirstArtistValue(values, first));
    }

    /// <summary>
    /// Reads the fields with the normalization the library import applies.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The normalized values.</returns>
    public static TagFieldValues Read(TagLib.Tag tag)
    {
        return new TagFieldValues(
            tag.Title?.Trim() ?? "",
            (int)tag.Track,
            tag.Disc > 0 ? (int)tag.Disc : null,
            tag.JoinedComposers?.Trim() ?? "",
            tag.BeatsPerMinute > 0 ? (int)tag.BeatsPerMinute : null)
        {
            Artist = ReadArtist(tag),
            Album = ReadAlbum(tag),
            Genre = ReadGenre(tag),
            Year = ReadYear(tag)
        };
    }

    /// <summary>
    /// Reads the raw fields used to verify a written copy, totals included.
    /// </summary>
    /// <param name="tag">Combined tag of an open file.</param>
    /// <returns>The raw snapshot.</returns>
    public static TagFieldSnapshot ReadRaw(TagLib.Tag tag)
    {
        return new TagFieldSnapshot(
            tag.Title,
            tag.Track,
            tag.TrackCount,
            tag.Disc,
            tag.DiscCount,
            tag.BeatsPerMinute,
            CanonicalComposers(tag.JoinedComposers),
            JoinArtistValues(tag.AlbumArtists),
            JoinArtistValues(tag.Performers),
            ArtistSource(tag),
            tag.Album,
            JoinArtistValues(tag.Genres),
            tag.Year);
    }

    /// <summary>
    /// Reads the compilation flag of a file: ID3v2 TCMP, Xiph COMPILATION or Apple cpil.
    /// Several tags can be present on one file (a FLAC with a stray ID3v2), the flag is set when any of them carries it.
    /// This is the single rule shared by the import, the tag write and its verification.
    /// </summary>
    /// <param name="file">Open file.</param>
    /// <returns>True when a tag of the file marks it as part of a compilation.</returns>
    public static bool ReadCompilationFlag(TagLib.File file)
    {
        return file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag { IsCompilation: true }
            || file.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment { IsCompilation: true }
            || file.GetTag(TagLib.TagTypes.Apple, false) is TagLib.Mpeg4.AppleTag { IsCompilation: true };
    }

    /// <summary>
    /// Reads the normalized fields of a file.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <param name="mimeType">MIME type used to open the file.</param>
    /// <returns>The normalized values.</returns>
    public static TagFieldValues ReadFile(string path, string mimeType)
    {
        using TagLib.File file = TagLib.File.Create(path, mimeType, TagLib.ReadStyle.Average);

        return Read(file.Tag) with { IsCompilation = ReadCompilationFlag(file) };
    }

    /// <summary>
    /// Puts composers in a canonical form: split on ';' and '/', trimmed, empty parts removed, joined by "; ".
    /// </summary>
    /// <param name="composers">Composers as joined by TagLib.</param>
    /// <returns>The canonical form, empty when none.</returns>
    public static string CanonicalComposers(string? composers)
    {
        if (string.IsNullOrWhiteSpace(composers))
            return "";

        return string.Join(
            "; ",
            composers.Split(ComposerSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}