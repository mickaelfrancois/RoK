using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagFieldReaderTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagFieldReaderTests_");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "tag_field_reader_matches_tag_service_on_mp3")]
    public void Read_ShouldMatchTagService_OnMp3()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(_tempDir.FullName, 3);

        // Act & Assert
        AssertMatchesTagService(path);
    }

    [Fact(DisplayName = "tag_field_reader_matches_tag_service_on_flac")]
    public void Read_ShouldMatchTagService_OnFlac()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(_tempDir.FullName);

        // Act & Assert
        AssertMatchesTagService(path);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "tag_field_reader_matches_tag_service_on_m4a")]
    public void Read_ShouldMatchTagService_OnM4a()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(_tempDir.FullName, EMp4Layout.MoovBeforeMdat);

        // Act & Assert
        AssertMatchesTagService(path);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "tag_field_reader_reads_the_artist_like_tag_service")]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Read_ShouldReadTheArtistLikeTagService(bool withAlbumArtist, bool withPerformer)
    {
        // Arrange
        string[] albumArtists = withAlbumArtist ? [" Album Artist "] : [];
        string[] performers = withPerformer ? ["Performer\u2010One"] : [];
        string path = Mp3TagFixture.CreateWithArtists(_tempDir.FullName, 4, albumArtists, performers);

        // Act & Assert
        AssertMatchesTagService(path);

        using TagLib.File file = TagLib.File.Create(path);
        string expected = withAlbumArtist ? "Album Artist" : withPerformer ? "Performer-One" : "";
        Assert.Equal(expected, TagFieldReader.Read(file.Tag).Artist);
    }

    [Theory(DisplayName = "tag_field_reader_reads_album_genre_and_year_like_tag_service")]
    [InlineData("  Spaced ‐ Album ", " Rock‐Pop ", "2001-05-12", "Spaced - Album", "Rock-Pop", 2001)]
    [InlineData("Plain", "Jazz", "1999", "Plain", "Jazz", 1999)]
    [InlineData("Plain", "Jazz", null, "Plain", "Jazz", null)]
    public void Read_ShouldReadAlbumGenreAndYearLikeTagService(string album, string genre, string? date, string expectedAlbum, string expectedGenre, int? expectedYear)
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(_tempDir.FullName, album, [genre, "Other"], date);

        // Act & Assert
        AssertMatchesTagService(path);

        using TagLib.File file = TagLib.File.Create(path);
        TagFieldValues values = TagFieldReader.Read(file.Tag);
        Assert.Equal(expectedAlbum, values.Album);
        Assert.Equal(expectedGenre, values.Genre);
        Assert.Equal(expectedYear, values.Year);
    }

    [Theory(DisplayName = "replace_first_artist_value_replaces_the_first_and_keeps_the_others")]
    [InlineData("", "New", "New")]
    [InlineData("Old", "New", "New")]
    [InlineData("Old\u001FGuest", "New", "New\u001FGuest")]
    public void ReplaceFirstJoinedArtistValue_ShouldKeepTheOtherValues(string joined, string first, string expected)
    {
        // Act
        string result = TagFieldReader.ReplaceFirstJoinedArtistValue(joined, first);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "canonical_composers_splits_trims_and_rejoins")]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("AC/DC", "AC; DC")]
    [InlineData(" A ;B; ; C ", "A; B; C")]
    public void CanonicalComposers_ShouldNormalize(string? input, string expected)
    {
        // Act
        string result = TagFieldReader.CanonicalComposers(input);

        // Assert
        Assert.Equal(expected, result);
    }

    private static void AssertMatchesTagService(string path)
    {
        // Arrange
        TagService service = new(NullLogger<TagService>.Instance);
        TrackFile track = new();

        // Act
        service.FillMusicProperties(path, track);

        TagFieldValues values;

        using (TagLib.File file = TagLib.File.Create(path))
            values = TagFieldReader.Read(file.Tag);

        // Assert
        Assert.Equal(track.Title, values.Title);
        Assert.Equal(track.TrackNumber, values.TrackNumber);
        Assert.Equal(track.Disc, values.Disc);
        Assert.Equal(track.Bpm, values.Bpm);
        Assert.Equal(track.Composers, values.Composers);
        Assert.Equal(track.Artist, values.Artist);
        Assert.Equal(track.Album, values.Album);
        Assert.Equal(track.Genre, values.Genre);
        Assert.Equal(track.Year, values.Year);
    }
}