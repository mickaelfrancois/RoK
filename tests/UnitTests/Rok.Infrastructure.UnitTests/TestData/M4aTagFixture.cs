namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>
/// Builds M4A files with a precise set of metadata and a chosen <c>moov</c> position, so tag writing can be checked against known content.
/// </summary>
internal static class M4aTagFixture
{
    public const string OriginalTitle = "Original title";
    public const string OriginalArtist = "Original artist";
    public const string OriginalComposer = "Original composer";
    public const uint OriginalTrack = 3;
    public const uint OriginalTrackCount = 12;
    public const uint OriginalDisc = 1;
    public const uint OriginalDiscCount = 2;
    public const uint OriginalBpm = 120;
    public const string LyricsText = "Line one\nLine two";
    public const string UnknownAtomType = "rokx";
    public const string DashMean = "com.apple.iTunes";
    public const string MusicBrainzName = "MusicBrainz Album Id";
    public const string MusicBrainzValue = "6b8b3a6e-5a4c-4f7e-9c0e-1f2d3c4b5a69";
    public const string ReplayGainName = "REPLAYGAIN_TRACK_GAIN";
    public const string ReplayGainValue = "-6.50 dB";
    public const string UnknownAtomValue = "unknown value";

    public static readonly byte[] PictureBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>
    /// Creates an AAC M4A with the title, artist, lyrics and cover atoms, two <c>com.apple.iTunes</c> dash boxes
    /// (MusicBrainz Album Id, REPLAYGAIN_TRACK_GAIN) and an unknown <c>rokx</c> atom, with <c>moov</c> in the requested position.
    /// </summary>
    public static string CreateRich(string folder, EMp4Layout layout, string fileName = "song.m4a")
    {
        string path = CreateUntagged(folder, fileName);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/m4a", TagLib.ReadStyle.Average))
        {
            TagLib.Mpeg4.AppleTag tag = (TagLib.Mpeg4.AppleTag)file.GetTag(TagLib.TagTypes.Apple, true);

            tag.Title = OriginalTitle;
            tag.Performers = [OriginalArtist];
            tag.Lyrics = LyricsText;
            tag.Track = OriginalTrack;
            tag.TrackCount = OriginalTrackCount;
            tag.Disc = OriginalDisc;
            tag.DiscCount = OriginalDiscCount;
            tag.Composers = [OriginalComposer];
            tag.BeatsPerMinute = OriginalBpm;

            tag.Pictures =
            [
                new TagLib.Picture(new TagLib.ByteVector(PictureBytes))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = "image/png"
                }
            ];

            tag.SetDashBox(DashMean, MusicBrainzName, MusicBrainzValue);
            tag.SetDashBox(DashMean, ReplayGainName, ReplayGainValue);
            tag.SetText(new TagLib.ByteVector(UnknownAtomType), UnknownAtomValue);

            file.Save();
        }

        Mp4LayoutRewriter.Rewrite(path, layout);

        return path;
    }

    /// <summary>
    /// Creates the rich M4A with exactly the given <c>aART</c> and <c>©ART</c> values.
    /// </summary>
    public static string CreateWithArtists(string folder, EMp4Layout layout, string[] albumArtists, string[] performers, string fileName = "song.m4a")
    {
        string path = CreateRich(folder, layout, fileName);

        using (TagLib.File file = TagLib.File.Create(path, "taglib/m4a", TagLib.ReadStyle.Average))
        {
            TagLib.Tag tag = file.GetTag(TagLib.TagTypes.Apple, true);

            tag.AlbumArtists = albumArtists;
            tag.Performers = performers;

            file.Save();
        }

        return path;
    }

    /// <summary>Creates an AAC M4A with no tag at all, with <c>moov</c> in the requested position.</summary>
    public static string CreateWithoutTags(string folder, EMp4Layout layout, string fileName = "song.m4a")
    {
        string path = CreateUntagged(folder, fileName);

        Mp4LayoutRewriter.Rewrite(path, layout);

        return path;
    }

    private static string CreateUntagged(string folder, string fileName)
    {
        Directory.CreateDirectory(folder);

        string scratch = Path.Combine(Path.GetTempPath(), "M4aTagFixture_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            string source = AudioFixtureFactory.Create(scratch, AudioFixtureFormat.M4a);
            string path = Path.Combine(folder, fileName);
            File.Copy(source, path, overwrite: true);

            using TagLib.File file = TagLib.File.Create(path, "taglib/m4a", TagLib.ReadStyle.None);
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