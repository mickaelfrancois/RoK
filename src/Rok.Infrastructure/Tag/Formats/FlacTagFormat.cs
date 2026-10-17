using System.Security.Cryptography;
using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Xiph comment writing for FLAC files.
/// </summary>
internal sealed class FlacTagFormat : ITagFormat
{
    private const string Mime = "taglib/flac";
    private const string TitleFieldName = "TITLE";
    private const string BpmFieldName = "BPM";
    private const string TempoFieldName = "TEMPO";
    private const TagLib.TagTypes ParasiticTags = TagLib.TagTypes.Id3v1 | TagLib.TagTypes.Id3v2 | TagLib.TagTypes.Ape;

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

        EArtistSource artistSource = TagFieldReader.ArtistSource(file.Tag);

        TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

        TagFormatHelpers.ApplyChanges(xiph, changes with { Bpm = null }, artistSource);
        ApplyBpm(xiph, changes.Bpm);

        if (file.TagTypesOnDisk.HasFlag(TagLib.TagTypes.Id3v2))
            TagFormatHelpers.ApplyChanges(file.GetTag(TagLib.TagTypes.Id3v2, false), changes, artistSource);
        else
            file.RemoveTags(TagLib.TagTypes.Id3v2);

        TagFormatHelpers.SyncSecondaryTags(file, changes, artistSource);
        TagFormatHelpers.ApplyCompilation(file, changes.IsCompilation);

        file.Save();
    }

    /// <inheritdoc />
    public TagFingerprint ReadFingerprint(string path, TagChanges changes)
    {
        FlacLayoutInfo layout = FlacLayout.Read(path);
        IReadOnlySet<string> changedFields = ChangedXiphFields(changes);

        long end;
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        TagFieldSnapshot values;
        string[] id3v2Frames;
        TagLib.TagTypes parasiticTags;

        using (TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average))
        {
            end = file.InvariantEndPosition;
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;
            values = TagFieldReader.ReadRaw(file.Tag) with { IsCompilation = TagFieldReader.ReadCompilationFlag(file) };
            id3v2Frames = TagFormatHelpers.Id3v2FrameIdsExcept(file, TagFormatHelpers.ChangedId3v2FrameIds(changes));
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

            if (changedFields.Contains(name))
                continue;

            preserved.Add(separator < 0 ? $"xiph:{name}" : $"xiph:{name}={field[(separator + 1)..]}");
        }

        preserved.AddRange(id3v2Frames.Select(id => $"id3v2:{id}"));
        preserved.Add($"tags:{parasiticTags}");
        preserved.Sort(StringComparer.Ordinal);

        return new TagFingerprint(
            values,
            duration,
            bitrate,
            sampleRate,
            TagFormatHelpers.HashRange(path, layout.FirstFrameOffset, end),
            preserved);
    }

    private static HashSet<string> ChangedXiphFields(TagChanges changes)
    {
        HashSet<string> names = [TitleFieldName];

        if (changes.TrackNumber is not null)
        {
            names.Add("TRACKNUMBER");
            names.Add("TRACKTOTAL");
        }

        if (changes.Disc is not null)
        {
            names.Add("DISCNUMBER");
            names.Add("DISCTOTAL");
        }

        if (changes.Composers is not null)
            names.Add("COMPOSER");

        if (changes.Bpm is not null)
        {
            names.Add(BpmFieldName);
            names.Add(TempoFieldName);
        }

        if (changes.Artist is not null)
        {
            names.Add("ARTIST");
            names.Add("ALBUMARTIST");
            names.Add("ALBUM ARTIST");
        }

        if (changes.Album is not null)
            names.Add("ALBUM");

        if (changes.Genre is not null)
            names.Add("GENRE");

        if (changes.Year is not null)
        {
            names.Add("DATE");
            names.Add("YEAR");
        }

        if (changes.IsCompilation is not null)
            names.Add("COMPILATION");

        return names;
    }

    private static void ApplyBpm(TagLib.Ogg.XiphComment xiph, int? bpm)
    {
        if (bpm is null)
            return;

        if (bpm.Value == 0)
        {
            xiph.RemoveField(BpmFieldName);
            xiph.RemoveField(TempoFieldName);

            return;
        }

        string value = bpm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        xiph.SetField(BpmFieldName, value);

        if (xiph.GetField(TempoFieldName).Length > 0)
            xiph.SetField(TempoFieldName, value);
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