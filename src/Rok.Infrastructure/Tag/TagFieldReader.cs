using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag;

/// <summary>
/// Single source of truth for reading the editable tag fields, shared by the import and the tag write verification.
/// </summary>
internal static class TagFieldReader
{
    private static readonly char[] ComposerSeparators = [';', '/'];

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
            tag.BeatsPerMinute > 0 ? (int)tag.BeatsPerMinute : null);
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
            CanonicalComposers(tag.JoinedComposers));
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

        return Read(file.Tag);
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