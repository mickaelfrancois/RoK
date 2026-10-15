using System.Security.Cryptography;
using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Xiph comment writing for FLAC files.
/// </summary>
internal sealed class FlacTagFormat : ITagFormat
{
    private const string MimeType = "taglib/flac";
    private const string TitleFieldName = "TITLE";
    private const TagLib.TagTypes ParasiticTags = TagLib.TagTypes.Id3v1 | TagLib.TagTypes.Id3v2 | TagLib.TagTypes.Ape;

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

        file.GetTag(TagLib.TagTypes.Xiph, true).Title = title;

        if (file.TagTypesOnDisk.HasFlag(TagLib.TagTypes.Id3v2))
            file.GetTag(TagLib.TagTypes.Id3v2, false).Title = title;
        else
            file.RemoveTags(TagLib.TagTypes.Id3v2);

        TagFormatHelpers.SyncSecondaryTags(file, title);

        file.Save();
    }

    /// <inheritdoc />
    public TagFingerprint ReadFingerprint(string path)
    {
        FlacLayoutInfo layout = FlacLayout.Read(path);

        long end;
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        string? title;
        string[] id3v2Frames;
        TagLib.TagTypes parasiticTags;

        using (TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average))
        {
            end = file.InvariantEndPosition;
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;
            title = file.GetTag(TagLib.TagTypes.Xiph, false)?.Title;
            id3v2Frames = TagFormatHelpers.Id3v2FrameIdsExceptTitle(file);
            parasiticTags = file.TagTypesOnDisk & ParasiticTags;
        }

        end = ResolveAudioEnd(end, new FileInfo(path).Length, layout.FirstFrameOffset);

        List<string> preserved = [];

        foreach (FlacMetadataBlock block in layout.Blocks)
        {
            if (block.Type is FlacLayout.VorbisCommentType or FlacLayout.PaddingType)
                continue;

            preserved.Add($"block:{block.Type}:{Convert.ToHexString(SHA256.HashData(block.Data))}");
        }

        foreach (string field in layout.VorbisFields)
        {
            int separator = field.IndexOf('=');
            string name = (separator < 0 ? field : field[..separator]).ToUpperInvariant();

            if (name == TitleFieldName)
                continue;

            preserved.Add(separator < 0 ? $"xiph:{name}" : $"xiph:{name}={field[(separator + 1)..]}");
        }

        preserved.AddRange(id3v2Frames.Select(id => $"id3v2:{id}"));
        preserved.Add($"tags:{parasiticTags}");
        preserved.Sort(StringComparer.Ordinal);

        return new TagFingerprint(
            title,
            duration,
            bitrate,
            sampleRate,
            TagFormatHelpers.HashRange(path, layout.FirstFrameOffset, end),
            preserved);
    }

    /// <summary>
    /// Picks the end of the audio stream and rejects an end that lies before the first frame.
    /// </summary>
    /// <param name="invariantEnd">TagLib's invariant end position, negative when unknown.</param>
    /// <param name="fileLength">Length of the file, used when the end is unknown.</param>
    /// <param name="firstFrameOffset">Position of the first audio frame.</param>
    /// <returns>The end position of the audio stream.</returns>
    /// <exception cref="TagLib.CorruptFileException">The end lies before the first frame.</exception>
    internal static long ResolveAudioEnd(long invariantEnd, long fileLength, long firstFrameOffset)
    {
        long end = invariantEnd < 0 ? fileLength : invariantEnd;

        if (end < firstFrameOffset)
            throw new TagLib.CorruptFileException("The FLAC audio stream ends before its first frame.");

        return end;
    }
}