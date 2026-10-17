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
    public const uint OriginalTrack = 3;
    public const uint OriginalTrackCount = 12;
    public const uint OriginalDisc = 1;
    public const uint OriginalDiscCount = 2;
    public const string OriginalComposer = "Original composer";
    public const uint OriginalBpm = 120;

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

    /// <summary>
    /// Creates the rich FLAC plus track number, disc, composer and BPM fields. The track number is written either combined
    /// (<c>TRACKNUMBER=3/12</c>) or split (<c>TRACKNUMBER=3</c> and <c>TRACKTOTAL=12</c>); <paramref name="withTempo"/>
    /// adds a <c>TEMPO</c> field next to <c>BPM</c>.
    /// </summary>
    public static string CreateWithNumbers(string folder, bool combinedTrack, bool withTempo = false, string fileName = "song.flac")
    {
        string path = CreateRich(folder, fileName);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average))
        {
            TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

            if (combinedTrack)
            {
                xiph.SetField("TRACKNUMBER", $"{OriginalTrack}/{OriginalTrackCount}");
            }
            else
            {
                xiph.SetField("TRACKNUMBER", OriginalTrack.ToString());
                xiph.SetField("TRACKTOTAL", OriginalTrackCount.ToString());
            }

            xiph.SetField("DISCNUMBER", OriginalDisc.ToString());
            xiph.SetField("DISCTOTAL", OriginalDiscCount.ToString());
            xiph.SetField("COMPOSER", OriginalComposer);
            xiph.SetField("BPM", OriginalBpm.ToString());

            if (withTempo)
                xiph.SetField("TEMPO", OriginalBpm.ToString());

            file.Save();
        }

        LowercaseFieldNameOnDisk(path);

        return path;
    }

    /// <summary>
    /// Creates the rich FLAC with exactly the given <c>ALBUMARTIST</c> and <c>ARTIST</c> values.
    /// </summary>
    public static string CreateWithArtists(string folder, string[] albumArtists, string[] performers, string fileName = "song.flac")
    {
        string path = CreateRich(folder, fileName);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average))
        {
            TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

            xiph.RemoveField("ARTIST");
            xiph.RemoveField("ALBUMARTIST");
            xiph.AlbumArtists = albumArtists;
            xiph.Performers = performers;

            file.Save();
        }

        LowercaseFieldNameOnDisk(path);

        return path;
    }

    /// <summary>
    /// Creates the rich FLAC with the given <c>ALBUM</c>, <c>GENRE</c> values and raw <c>DATE</c> text (null for none).
    /// </summary>
    public static string CreateWithAlbumFields(string folder, string album, string[] genres, string? date, string fileName = "song.flac")
    {
        string path = CreateRich(folder, fileName);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average))
        {
            TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);

            xiph.Album = album;
            xiph.Genres = genres;

            if (date is not null)
                xiph.SetField("DATE", date);

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