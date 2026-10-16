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
    }
}