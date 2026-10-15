using System.Security.Cryptography;
using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Apple tag (<c>©nam</c>) writing for MP4 audio files (AAC and ALAC).
/// </summary>
internal sealed class Mp4TagFormat : ITagFormat
{
    private const string Mime = "taglib/m4a";

    /// <inheritdoc />
    public string MimeType => Mime;

    /// <inheritdoc />
    public ETagWriteReason CheckFile(string path)
    {
        return Mp4Layout.IsFragmented(path)
            ? ETagWriteReason.UnsupportedFormat
            : ETagWriteReason.None;
    }

    /// <inheritdoc />
    public void Write(string path, TagChanges changes)
    {
        using TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average);

        TagFormatHelpers.ApplyChanges(file.GetTag(TagLib.TagTypes.Apple, true), changes);
        file.Save();
    }

    /// <inheritdoc />
    public TagFingerprint ReadFingerprint(string path, TagChanges changes)
    {
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        TagFieldSnapshot values;

        using (TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average))
        {
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;
            values = TagFieldReader.ReadRaw(file.Tag);
        }

        IReadOnlySet<string> changedAtoms = ChangedAtomTypes(changes);
        List<string> preserved = [];

        foreach (Mp4IlstItem item in Mp4Layout.ReadIlstItems(path))
        {
            if (!changedAtoms.Contains(item.Type))
                preserved.Add($"ilst:{item.Type}:{Convert.ToHexString(SHA256.HashData(item.Data))}");
        }

        preserved.Add($"stco:{Mp4Layout.HashRelativeChunkOffsets(path)}");
        preserved.Sort(StringComparer.Ordinal);

        return new TagFingerprint(values, duration, bitrate, sampleRate, Mp4Layout.HashMediaBoxes(path), preserved);
    }

    private static HashSet<string> ChangedAtomTypes(TagChanges changes)
    {
        HashSet<string> types = [Mp4Layout.TitleAtomType];

        if (changes.TrackNumber is not null)
            types.Add("trkn");

        if (changes.Disc is not null)
            types.Add("disk");

        if (changes.Composers is not null)
            types.Add("©wrt");

        if (changes.Bpm is not null)
            types.Add("tmpo");

        return types;
    }
}