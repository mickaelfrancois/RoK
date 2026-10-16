using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// ID3 tag writing for MP3 files.
/// </summary>
internal sealed class Mp3TagFormat : ITagFormat
{
    private const string Mime = "taglib/mp3";

    /// <inheritdoc />
    public string MimeType => Mime;

    /// <inheritdoc />
    public ETagWriteReason CheckFile(string path)
    {
        return TagFormatHelpers.HasId3v22Tag(path, MimeType)
            ? ETagWriteReason.UnsupportedTagVersion
            : ETagWriteReason.None;
    }

    /// <inheritdoc />
    public void Write(string path, TagChanges changes)
    {
        using TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average);

        TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
        TagFormatHelpers.ApplyChanges(id3v2, changes);

        TagFormatHelpers.SyncSecondaryTags(file, changes);

        file.Save();
    }

    /// <inheritdoc />
    public TagFingerprint ReadFingerprint(string path, TagChanges changes)
    {
        long start;
        long end;
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        TagFieldSnapshot values;
        string[] frameIds;

        using (TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average))
        {
            start = file.InvariantStartPosition;
            end = file.InvariantEndPosition;
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;
            values = TagFieldReader.ReadRaw(file.Tag);
            frameIds = TagFormatHelpers.Id3v2FrameIdsExcept(file, TagFormatHelpers.ChangedId3v2FrameIds(changes));
        }

        return new TagFingerprint(values, duration, bitrate, sampleRate, TagFormatHelpers.HashRange(path, start, end), frameIds);
    }
}