using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagServiceTests : IDisposable
{
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromMilliseconds(200);

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagServiceTests_");
    private readonly TagService _service = new(NullLogger<TagService>.Instance);

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Theory(DisplayName = "fill_music_properties_should_read_duration_and_title_of_uncompressed_formats")]
    [InlineData(AudioFixtureFormat.Wav)]
    [InlineData(AudioFixtureFormat.Aiff)]
    [InlineData(AudioFixtureFormat.Aif)]
    public void FillMusicProperties_ShouldReadDurationAndTitle_OfUncompressedFormats(AudioFixtureFormat format)
    {
        AssertMusicPropertiesAreRead(format);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "fill_music_properties_should_read_duration_and_title_of_m4a_aac")]
    public void FillMusicProperties_ShouldReadDurationAndTitle_OfM4a()
    {
        AssertMusicPropertiesAreRead(AudioFixtureFormat.M4a);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Wma, DisplayName = "fill_music_properties_should_read_duration_and_title_of_wma")]
    public void FillMusicProperties_ShouldReadDurationAndTitle_OfWma()
    {
        AssertMusicPropertiesAreRead(AudioFixtureFormat.Wma);
    }

    private void AssertMusicPropertiesAreRead(AudioFixtureFormat format)
    {
        // Arrange
        string path = AudioFixtureFactory.Create(_tempDir.FullName, format);
        TrackFile track = new();

        // Act
        _service.FillMusicProperties(path, track);

        // Assert
        Assert.InRange(track.Duration, AudioFixtureFactory.FixtureDuration - DurationTolerance, AudioFixtureFactory.FixtureDuration + DurationTolerance);
        Assert.True(track.SampleRate > 0);
        Assert.True(track.Channels > 0);
        Assert.Equal(AudioFixtureFactory.FixtureTitle, track.Title);
    }
}