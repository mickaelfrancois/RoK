namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>
/// Builds FLAC files with a precise set of metadata, so tag writing can be checked against known content.
/// </summary>
internal static class FlacTagFixture
{
    public const string OriginalTitle = "Original title";
    public const string OriginalArtist = "Original artist";
    public const string LyricsText = "Line one\nLine two";
    public const string MusicBrainzValue = "6b8b3a6e-5a4c-4f7e-9c0e-1f2d3c4b5a69";
    public const string ReplayGainValue = "-6.50 dB";
    public const string UnknownFieldValue = "unknown value";
    public const string LowercaseFieldName = "rok_lowercase";
    public const string LowercaseFieldValue = "lowercase value";
    public const string ApplicationId = "ROKT";
    public const int PaddingSize = 1024;

    public static readonly byte[] PictureBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];
    public static readonly byte[] ApplicationPayload = [9, 8, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>
    /// Creates a FLAC with a Xiph comment (title, artist, lyrics, MusicBrainz, ReplayGain, an unknown field and a
    /// lowercase field name written raw), a PICTURE block, an APPLICATION block and PADDING.
    /// </summary>
    public static string CreateRich(string folder, string fileName = "song.flac")
    {
        string path = CreateBase(folder, fileName, [FlacBlock.Application(ApplicationId, ApplicationPayload), FlacBlock.Padding(PaddingSize)]);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average))
        {
            TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

            xiph.Title = OriginalTitle;
            xiph.Performers = [OriginalArtist];
            xiph.SetField("LYRICS", LyricsText);
            xiph.SetField("MUSICBRAINZ_ALBUMID", MusicBrainzValue);
            xiph.SetField("REPLAYGAIN_TRACK_GAIN", ReplayGainValue);
            xiph.SetField("ROK_UNKNOWN_FIELD", UnknownFieldValue);
            xiph.SetField(LowercaseFieldName, LowercaseFieldValue);

            file.GetTag(TagLib.TagTypes.FlacMetadata, true).Pictures =
            [
                new TagLib.Picture(new TagLib.ByteVector(PictureBytes))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = "image/png"
                }
            ];

            file.Save();
        }

        LowercaseFieldNameOnDisk(path);

        return path;
    }

    /// <summary>Creates a FLAC holding only STREAMINFO and audio frames: no Xiph comment, no padding.</summary>
    public static string CreateWithoutTags(string folder, string fileName = "song.flac")
    {
        return CreateBase(folder, fileName, []);
    }

    /// <summary>
    /// Creates a FLAC with a Xiph title plus a parasitic ID3v2 tag of the given major version (2, 3 or 4) in front of it.
    /// The ID3v2.3/2.4 tag carries the title, an artist and a user text frame.
    /// </summary>
    public static string CreateWithParasiticId3v2(string folder, byte version, string fileName = "song.flac")
    {
        string path = CreateBase(folder, fileName, [FlacBlock.Padding(PaddingSize)]);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average))
        {
            file.GetTag(TagLib.TagTypes.Xiph, true).Title = OriginalTitle;

            if (version != 2)
            {
                TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);

                id3v2.Version = version;
                id3v2.Title = OriginalTitle;
                id3v2.Performers = [OriginalArtist];
                TagLib.Id3v2.UserTextInformationFrame.Get(id3v2, "MusicBrainz Album Id", true).Text = [MusicBrainzValue];
            }

            file.Save();
        }

        if (version == 2)
            PrependId3v22(path);

        return path;
    }

    private static string CreateBase(string folder, string fileName, IReadOnlyList<FlacBlock> blocks)
    {
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, fileName);
        FlacFixtureWriter.WriteSine(path, blocks);

        return path;
    }

    private static void LowercaseFieldNameOnDisk(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        byte[] upper = System.Text.Encoding.ASCII.GetBytes(LowercaseFieldName.ToUpperInvariant() + "=");
        byte[] lower = System.Text.Encoding.ASCII.GetBytes(LowercaseFieldName + "=");
        int index = bytes.AsSpan().IndexOf(upper);

        if (index < 0)
            return;

        lower.CopyTo(bytes, index);
        File.WriteAllBytes(path, bytes);
    }

    private static void PrependId3v22(string path)
    {
        byte[] flac = File.ReadAllBytes(path);

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

        File.WriteAllBytes(path, [.. header, .. frame, .. flac]);
    }
}