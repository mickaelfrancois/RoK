using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// ID3 tag writing for MP3 files.
/// </summary>
internal sealed class Mp3TagFormat : ITagFormat
{
    private const string MimeType = "taglib/mp3";

    /// <inheritdoc />
    public ETagWriteReason CheckFile(string path)
    {
        return TagFormatHelpers.HasId3v22Tag(path, MimeType)
            ? ETagWriteReason.UnsupportedTagVersion
            : ETagWriteReason.None;
    }

    /// <inheritdoc />
    public void WriteTitle(string path, string title)
    {
        using TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average);

        TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
        id3v2.Title = title;

        TagFormatHelpers.SyncSecondaryTags(file, title);

        file.Save();
    }

    /// <inheritdoc />
    public TagFingerprint ReadFingerprint(string path)
    {
        long start;
        long end;
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        string? title;
        string[] frameIds;

        using (TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average))
        {
            start = file.InvariantStartPosition;
            end = file.InvariantEndPosition;
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;
            title = (file.GetTag(TagLib.TagTypes.Id3v2, false) as TagLib.Id3v2.Tag)?.Title;
            frameIds = TagFormatHelpers.Id3v2FrameIdsExceptTitle(file);
        }

        return new TagFingerprint(title, duration, bitrate, sampleRate, TagFormatHelpers.HashRange(path, start, end), frameIds);
    }
}