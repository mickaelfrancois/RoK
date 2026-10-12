namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>
/// Builds MP3 files with a precise set of tags, so tag writing can be checked against known content.
/// </summary>
internal static class Mp3TagFixture
{
    public const string OriginalTitle = "Original title";
    public const string UnknownFrameId = "ZZZZ";
    public const string MusicBrainzDescription = "MusicBrainz Album Id";
    public const string MusicBrainzValue = "6b8b3a6e-5a4c-4f7e-9c0e-1f2d3c4b5a69";
    public const string ReplayGainDescription = "REPLAYGAIN_TRACK_GAIN";
    public const string ReplayGainValue = "-6.50 dB";
    public const string LyricsText = "Line one\nLine two";

    public static readonly byte[] PictureBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];
    public static readonly byte[] UnknownFrameBytes = [9, 8, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>Creates an MP3 whose only tag is an ID3v2 of the given major version, with cover, lyrics, MusicBrainz, ReplayGain and an unknown frame.</summary>
    public static string CreateRich(string folder, byte version, string fileName = "song.mp3")
    {
        string path = CreateUntagged(folder, fileName);

        using TagLib.File file = TagLib.File.Create(path, "taglib/mp3", TagLib.ReadStyle.Average);
        TagLib.Id3v2.Tag tag = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);

        tag.Version = version;
        tag.Title = OriginalTitle;
        tag.Performers = ["Original artist"];

        tag.AddFrame(new TagLib.Id3v2.AttachmentFrame
        {
            Type = TagLib.PictureType.FrontCover,
            MimeType = "image/png",
            Data = new TagLib.ByteVector(PictureBytes)
        });

        TagLib.Id3v2.UnsynchronisedLyricsFrame lyrics = new(string.Empty, "eng", TagLib.StringType.Latin1) { Text = LyricsText };
        tag.AddFrame(lyrics);

        TagLib.Id3v2.UserTextInformationFrame.Get(tag, MusicBrainzDescription, true).Text = [MusicBrainzValue];
        TagLib.Id3v2.UserTextInformationFrame.Get(tag, ReplayGainDescription, true).Text = [ReplayGainValue];

        tag.AddFrame(new TagLib.Id3v2.UnknownFrame(new TagLib.ByteVector(UnknownFrameId), new TagLib.ByteVector(UnknownFrameBytes)));

        file.RemoveTags(TagLib.TagTypes.Id3v1);
        file.Save();

        return path;
    }

    /// <summary>Creates an MP3 with no tag at all.</summary>
    public static string CreateWithoutTags(string folder, string fileName = "song.mp3")
    {
        return CreateUntagged(folder, fileName);
    }

    /// <summary>Creates an MP3 with an ID3v2.3 tag and an ID3v1 tag.</summary>
    public static string CreateWithId3v1(string folder, string fileName = "song.mp3")
    {
        string path = CreateUntagged(folder, fileName);

        using TagLib.File file = TagLib.File.Create(path, "taglib/mp3", TagLib.ReadStyle.Average);
        TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
        id3v2.Title = OriginalTitle;

        TagLib.Tag id3v1 = file.GetTag(TagLib.TagTypes.Id3v1, true);
        id3v1.Title = OriginalTitle;

        file.Save();

        return path;
    }

    /// <summary>Creates an MP3 with a hand-built ID3v2.2 tag holding the title.</summary>
    public static string CreateWithId3v22(string folder, string fileName = "song.mp3")
    {
        string path = CreateUntagged(folder, fileName);
        byte[] audio = File.ReadAllBytes(path);

        byte[] text = [.. "\0"u8, .. System.Text.Encoding.Latin1.GetBytes(OriginalTitle)];
        byte[] frame = [.. "TT2"u8, (byte)(text.Length >> 16), (byte)(text.Length >> 8), (byte)text.Length, .. text];

        byte[] header =
        [
            .. "ID3"u8, 2, 0, 0,
            (byte)((frame.Length >> 21) & 0x7F),
            (byte)((frame.Length >> 14) & 0x7F),
            (byte)((frame.Length >> 7) & 0x7F),
            (byte)(frame.Length & 0x7F)
        ];

        File.WriteAllBytes(path, [.. header, .. frame, .. audio]);

        return path;
    }

    private static string CreateUntagged(string folder, string fileName)
    {
        Directory.CreateDirectory(folder);

        string scratch = Path.Combine(Path.GetTempPath(), "Mp3TagFixture_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            string source = AudioFixtureFactory.Create(scratch, AudioFixtureFormat.Mp3);
            string path = Path.Combine(folder, fileName);
            File.Copy(source, path, overwrite: true);

            using TagLib.File file = TagLib.File.Create(path, "taglib/mp3", TagLib.ReadStyle.None);
            file.RemoveTags(TagLib.TagTypes.AllTags);
            file.Save();

            return path;
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }
}