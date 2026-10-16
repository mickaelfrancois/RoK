using System.Security.Cryptography;
using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Helpers shared by the tag formats.
/// </summary>
internal static class TagFormatHelpers
{
    private const int Id3v22MajorVersion = 2;
    private const string TitleFrameId = "TIT2";
    private const string TrackFrameId = "TRCK";
    private const string DiscFrameId = "TPOS";
    private const string ComposerFrameId = "TCOM";
    private const string BpmFrameId = "TBPM";
    private const string PerformerFrameId = "TPE1";
    private const string AlbumArtistFrameId = "TPE2";

    private static readonly TagLib.TagTypes[] SecondaryTagTypes = [TagLib.TagTypes.Id3v1, TagLib.TagTypes.Ape];

    /// <summary>
    /// Tells whether the file carries an ID3v2.2 tag, which TagLib would rewrite on save.
    /// </summary>
    /// <param name="path">File to inspect.</param>
    /// <param name="mimeType">MIME type used to open the file.</param>
    /// <returns><c>true</c> when an ID3v2.2 tag is present.</returns>
    public static bool HasId3v22Tag(string path, string mimeType)
    {
        using TagLib.File file = TagLib.File.Create(path, mimeType, TagLib.ReadStyle.None);

        return file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag { Version: Id3v22MajorVersion };
    }

    /// <summary>
    /// Lists the ids of the ID3v2 frames that a write with these changes may alter.
    /// </summary>
    /// <param name="changes">Changes being written.</param>
    /// <returns>The frame ids to leave out of the preserved fields.</returns>
    public static IReadOnlySet<string> ChangedId3v2FrameIds(TagChanges changes)
    {
        HashSet<string> ids = [TitleFrameId];

        if (changes.TrackNumber is not null)
            ids.Add(TrackFrameId);

        if (changes.Disc is not null)
            ids.Add(DiscFrameId);

        if (changes.Composers is not null)
            ids.Add(ComposerFrameId);

        if (changes.Bpm is not null)
            ids.Add(BpmFrameId);

        if (changes.Artist is not null)
        {
            ids.Add(PerformerFrameId);
            ids.Add(AlbumArtistFrameId);
        }

        return ids;
    }

    /// <summary>
    /// Lists the ID3v2 frame ids of the file except the given ones, sorted in ordinal order.
    /// </summary>
    /// <param name="file">Open file.</param>
    /// <param name="excludedIds">Frame ids to leave out.</param>
    /// <returns>The sorted frame ids, empty when there is no ID3v2 tag.</returns>
    public static string[] Id3v2FrameIdsExcept(TagLib.File file, IReadOnlySet<string> excludedIds)
    {
        if (file.GetTag(TagLib.TagTypes.Id3v2, false) is not TagLib.Id3v2.Tag id3v2)
            return [];

        return id3v2.GetFrames()
            .Select(f => f.FrameId.ToString())
            .Where(id => !excludedIds.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Applies the changes to a tag.
    /// </summary>
    /// <param name="tag">Tag to write.</param>
    /// <param name="changes">Changes to apply.</param>
    /// <param name="artistSource">Field that defines the artist, resolved once on the combined tag before any write.</param>
    public static void ApplyChanges(TagLib.Tag tag, TagChanges changes, EArtistSource artistSource)
    {
        if (changes.Title is not null)
            tag.Title = changes.Title;

        if (changes.TrackNumber is not null)
            tag.Track = (uint)changes.TrackNumber.Value;

        if (changes.Disc is not null)
            tag.Disc = (uint)changes.Disc.Value;

        if (changes.Composers is not null)
            tag.Composers = changes.Composers.Length == 0 ? [] : [changes.Composers];

        if (changes.Bpm is not null)
            tag.BeatsPerMinute = (uint)changes.Bpm.Value;

        if (changes.Artist is not null)
            ApplyArtist(tag, changes.Artist, artistSource);
    }

    private static void ApplyArtist(TagLib.Tag tag, string artist, EArtistSource artistSource)
    {
        if (artistSource == EArtistSource.AlbumArtist)
            tag.AlbumArtists = TagFieldReader.ReplaceFirstArtistValue(tag.AlbumArtists, artist);
        else
            tag.Performers = TagFieldReader.ReplaceFirstArtistValue(tag.Performers, artist);
    }

    /// <summary>
    /// Applies the changes to the secondary tags that exist on disk and removes the absent ones, never creating any.
    /// </summary>
    /// <param name="file">Open file.</param>
    /// <param name="changes">Changes to apply.</param>
    /// <param name="artistSource">Field that defines the artist.</param>
    public static void SyncSecondaryTags(TagLib.File file, TagChanges changes, EArtistSource artistSource)
    {
        foreach (TagLib.TagTypes type in SecondaryTagTypes)
        {
            if (file.TagTypesOnDisk.HasFlag(type))
                ApplyChanges(file.GetTag(type, false), changes, artistSource);
            else
                file.RemoveTags(type);
        }
    }

    /// <summary>
    /// Hashes a byte range of a file with SHA-256.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <param name="start">First byte of the range.</param>
    /// <param name="end">Position after the last byte of the range.</param>
    /// <returns>The hash as an uppercase hexadecimal string.</returns>
    public static string HashRange(string path, long start, long end)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(start, SeekOrigin.Begin);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long remaining = end - start;

        while (remaining > 0)
        {
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));

            if (read == 0)
                break;

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}