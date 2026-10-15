using System.Security.Cryptography;
using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Apple tag (<c>©nam</c>) writing for MP4 audio files (AAC and ALAC).
/// </summary>
internal sealed class Mp4TagFormat : ITagFormat
{
    private const string MimeType = "taglib/m4a";

    /// <inheritdoc />
    public ETagWriteReason CheckFile(string path)
    {
        return Mp4Layout.IsFragmented(path)
            ? ETagWriteReason.UnsupportedFormat
            : ETagWriteReason.None;
    }

    /// <inheritdoc />
    public void WriteTitle(string path, string title)
    {
        using TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average);

        file.GetTag(TagLib.TagTypes.Apple, true).Title = title;
        file.Save();
    }

    /// <inheritdoc />
    public TagFingerprint ReadFingerprint(string path)
    {
        TimeSpan duration;
        int bitrate;
        int sampleRate;
        string? title;

        using (TagLib.File file = TagLib.File.Create(path, MimeType, TagLib.ReadStyle.Average))
        {
            duration = file.Properties.Duration;
            bitrate = file.Properties.AudioBitrate;
            sampleRate = file.Properties.AudioSampleRate;
            title = file.GetTag(TagLib.TagTypes.Apple, false)?.Title;
        }

        List<string> preserved = [];

        foreach (Mp4IlstItem item in Mp4Layout.ReadIlstItems(path))
        {
            if (item.Type != Mp4Layout.TitleAtomType)
                preserved.Add($"ilst:{item.Type}:{Convert.ToHexString(SHA256.HashData(item.Data))}");
        }

        preserved.Add($"stco:{Mp4Layout.HashRelativeChunkOffsets(path)}");
        preserved.Sort(StringComparer.Ordinal);

        return new TagFingerprint(title, duration, bitrate, sampleRate, Mp4Layout.HashMediaBoxes(path), preserved);
    }
}