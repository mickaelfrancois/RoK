using System.Security.Cryptography;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Helpers shared by the tag formats.
/// </summary>
internal static class TagFormatHelpers
{
    private const int Id3v22MajorVersion = 2;
    private const string TitleFrameId = "TIT2";

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
    /// Lists the ID3v2 frame ids of the file except the title frame, sorted in ordinal order.
    /// </summary>
    /// <param name="file">Open file.</param>
    /// <returns>The sorted frame ids, empty when there is no ID3v2 tag.</returns>
    public static string[] Id3v2FrameIdsExceptTitle(TagLib.File file)
    {
        if (file.GetTag(TagLib.TagTypes.Id3v2, false) is not TagLib.Id3v2.Tag id3v2)
            return [];

        return id3v2.GetFrames()
            .Select(f => f.FrameId.ToString())
            .Where(id => id != TitleFrameId)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Writes the title into the secondary tags that exist on disk and removes the absent ones, never creating any.
    /// </summary>
    /// <param name="file">Open file.</param>
    /// <param name="title">New title.</param>
    public static void SyncSecondaryTags(TagLib.File file, string title)
    {
        foreach (TagLib.TagTypes type in SecondaryTagTypes)
        {
            if (file.TagTypesOnDisk.HasFlag(type))
                file.GetTag(type, false).Title = title;
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