using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Player.Mix;

namespace Rok.Infrastructure.UnitTests.Player.Mix;

public sealed class NAudioEnvelopeReaderTests : IDisposable
{
    private const int SampleRate = 44100;

    private readonly NAudioEnvelopeReader _reader = new(NullLogger<NAudioEnvelopeReader>.Instance);

    public void Dispose() => _reader.Dispose();

    private static byte[] StereoSine(double sineSeconds, double silenceSeconds)
    {
        var sineFrames = (int)(sineSeconds * SampleRate);
        var totalFrames = sineFrames + (int)(silenceSeconds * SampleRate);
        var data = new byte[totalFrames * 4];

        for (var frame = 0; frame < sineFrames; frame++)
        {
            var sample = (short)(short.MaxValue * 0.5 * Math.Sin(2 * Math.PI * 440 * frame / SampleRate));
            BitConverter.TryWriteBytes(data.AsSpan(frame * 4, 2), sample);
            BitConverter.TryWriteBytes(data.AsSpan((frame * 4) + 2, 2), sample);
        }

        return data;
    }

    [Fact(DisplayName = "reads_the_tail_of_a_wav_file")]
    public async Task ReadsTheTailOfAWavFile()
    {
        // Arrange
        using var file = TestWaveFile.Create(SampleRate, 16, 2, StereoSine(35, 5));

        // Act
        var signal = await _reader.ReadAsync(file.Path, EAudioEdge.Tail, TimeSpan.FromSeconds(30), false, CancellationToken.None);

        // Assert
        Assert.NotNull(signal);
        var envelope = signal.Envelope;
        Assert.Equal(10, envelope.StartSeconds, 0.1);
        Assert.Equal(40, envelope.TrackLengthSeconds, 0.1);
        Assert.InRange(envelope.LevelsDb.Length, 598, 600);
        Assert.True(envelope.LevelsDb[^1] < -80);
        Assert.True(envelope.LevelsDb[0] > -20);
    }

    [Fact(DisplayName = "reads_the_head_of_a_wav_file")]
    public async Task ReadsTheHeadOfAWavFile()
    {
        // Arrange
        using var file = TestWaveFile.Create(SampleRate, 16, 2, StereoSine(40, 0));

        // Act
        var signal = await _reader.ReadAsync(file.Path, EAudioEdge.Head, TimeSpan.FromSeconds(30), false, CancellationToken.None);

        // Assert
        Assert.NotNull(signal);
        var envelope = signal.Envelope;
        Assert.Equal(0, envelope.StartSeconds);
        Assert.InRange(envelope.LevelsDb.Length, 598, 600);
    }

    [Fact(DisplayName = "reads_while_another_reader_has_the_file_open")]
    public async Task ReadsWhileAnotherReaderHasTheFileOpen()
    {
        // Arrange
        using var file = TestWaveFile.Create(SampleRate, 16, 2, StereoSine(5, 0));
        using var playback = new AudioFileReader(file.Path);

        // Act
        var signal = await _reader.ReadAsync(file.Path, EAudioEdge.Head, TimeSpan.FromSeconds(30), false, CancellationToken.None);

        // Assert
        Assert.NotNull(signal);
        var envelope = signal.Envelope;
        Assert.InRange(envelope.LevelsDb.Length, 98, 100);
    }

    [Fact(DisplayName = "missing_file_returns_null_without_throwing")]
    public async Task MissingFileReturnsNullWithoutThrowing()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"rok-missing-{Guid.NewGuid():N}.wav");

        // Act
        var signal = await _reader.ReadAsync(path, EAudioEdge.Tail, TimeSpan.FromSeconds(30), false, CancellationToken.None);

        // Assert
        Assert.Null(signal);
    }

    [Fact(DisplayName = "mono_samples_are_built_at_the_decimated_rate_when_requested")]
    public async Task MonoSamplesAreBuiltWhenRequested()
    {
        // Arrange
        using var file = TestWaveFile.Create(SampleRate, 16, 2, StereoSine(35, 5));

        // Act
        var signal = await _reader.ReadAsync(file.Path, EAudioEdge.Tail, TimeSpan.FromSeconds(30), true, CancellationToken.None);

        // Assert
        Assert.NotNull(signal);
        Assert.NotNull(signal.MonoSamples);
        Assert.Equal(11025, signal.MonoSampleRate);
        Assert.InRange(signal.MonoSamples.Length, (30 * 11025) - 4, 30 * 11025);
        Assert.Equal(10, signal.StartSeconds, 0.1);
    }

    [Fact(DisplayName = "mono_samples_are_absent_when_not_requested")]
    public async Task MonoSamplesAreAbsentWhenNotRequested()
    {
        // Arrange
        using var file = TestWaveFile.Create(SampleRate, 16, 2, StereoSine(40, 0));

        // Act
        var signal = await _reader.ReadAsync(file.Path, EAudioEdge.Head, TimeSpan.FromSeconds(30), false, CancellationToken.None);

        // Assert
        Assert.NotNull(signal);
        Assert.Null(signal.MonoSamples);
        Assert.Equal(0, signal.MonoSampleRate);
    }

    [Fact(DisplayName = "envelope_is_identical_with_or_without_mono_samples")]
    public async Task EnvelopeIsTheSameWithOrWithoutMonoSamples()
    {
        // Arrange
        using var file = TestWaveFile.Create(SampleRate, 16, 2, StereoSine(35, 5));

        // Act
        var without = await _reader.ReadAsync(file.Path, EAudioEdge.Tail, TimeSpan.FromSeconds(30), false, CancellationToken.None);
        var with = await _reader.ReadAsync(file.Path, EAudioEdge.Tail, TimeSpan.FromSeconds(30), true, CancellationToken.None);

        // Assert
        Assert.NotNull(without);
        Assert.NotNull(with);
        Assert.Equal(without.Envelope.LevelsDb, with.Envelope.LevelsDb);
        Assert.Equal(without.Envelope.StartSeconds, with.Envelope.StartSeconds);
    }

    [Fact(DisplayName = "dispose_is_safe_when_no_file_was_ever_read")]
    public void DisposeIsSafeWhenNoFileWasEverRead()
    {
        // Arrange
        var reader = new NAudioEnvelopeReader(NullLogger<NAudioEnvelopeReader>.Instance);

        // Act
        var error = Record.Exception(reader.Dispose);

        // Assert
        Assert.Null(error);
    }
}