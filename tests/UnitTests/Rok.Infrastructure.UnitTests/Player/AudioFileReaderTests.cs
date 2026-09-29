using NAudio.Wave;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Player;

public sealed class AudioFileReaderTests : IDisposable
{
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromMilliseconds(200);

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("AudioFileReaderTests_");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Theory(DisplayName = "audio_file_reader_should_decode_and_seek_uncompressed_formats")]
    [InlineData(AudioFixtureFormat.Wav)]
    [InlineData(AudioFixtureFormat.Aiff)]
    [InlineData(AudioFixtureFormat.Aif)]
    public void AudioFileReader_ShouldDecodeAndSeek_UncompressedFormats(AudioFixtureFormat format)
    {
        AssertDecodesAndSeeks(format);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "audio_file_reader_should_decode_and_seek_m4a_aac")]
    public void AudioFileReader_ShouldDecodeAndSeek_M4a()
    {
        AssertDecodesAndSeeks(AudioFixtureFormat.M4a);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Wma, DisplayName = "audio_file_reader_should_decode_and_seek_wma")]
    public void AudioFileReader_ShouldDecodeAndSeek_Wma()
    {
        AssertDecodesAndSeeks(AudioFixtureFormat.Wma);
    }

    private void AssertDecodesAndSeeks(AudioFixtureFormat format)
    {
        // Arrange
        string path = AudioFixtureFactory.Create(_tempDir.FullName, format);
        float[] buffer = new float[4096];

        // Act
        using AudioFileReader reader = new(path);
        int read = reader.Read(buffer.AsSpan());
        reader.CurrentTime = TimeSpan.FromSeconds(1);
        int readAfterSeek = reader.Read(buffer.AsSpan());

        // Assert
        Assert.InRange(reader.TotalTime, AudioFixtureFactory.FixtureDuration - DurationTolerance, AudioFixtureFactory.FixtureDuration + DurationTolerance);
        Assert.True(read > 0);
        Assert.True(readAfterSeek > 0);
    }
}