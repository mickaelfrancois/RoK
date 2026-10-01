using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class RmsEnvelopeAccumulatorTests
{
    [Fact(DisplayName = "envelope_of_a_full_scale_sine_is_about_minus_3_dbfs")]
    public void Build_FullScaleSine_IsAboutMinus3Db()
    {
        // Arrange
        var accumulator = new RmsEnvelopeAccumulator(44100);
        var samples = new float[44100 * 2];

        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 44100);

        // Act
        accumulator.Add(samples, 1);
        var envelope = accumulator.Build(0, 2);

        // Assert
        Assert.All(envelope.LevelsDb, level => Assert.InRange(level, -3.11, -2.91));
    }

    [Fact(DisplayName = "envelope_downmixes_stereo_to_mono")]
    public void Add_LeftChannelOnly_IsAveragedWithSilentRight()
    {
        // Arrange
        var accumulator = new RmsEnvelopeAccumulator(SyntheticSignal.SampleRate);
        var mono = SyntheticSignal.Sine(1);
        var stereo = new float[mono.Length * 2];

        for (var i = 0; i < mono.Length; i++)
            stereo[i * 2] = mono[i];

        // Act
        accumulator.Add(stereo, 2);
        var envelope = accumulator.Build(0, 1);

        // Assert
        Assert.All(envelope.LevelsDb, level => Assert.InRange(level, -9.13, -8.93));
    }

    [Fact(DisplayName = "envelope_has_one_level_per_50_ms_window")]
    public void Build_ThirtySecondsAt44100_Has600Windows()
    {
        // Arrange
        var accumulator = new RmsEnvelopeAccumulator(44100);

        // Act
        accumulator.Add(new float[44100 * 30], 1);
        var envelope = accumulator.Build(0, 30);

        // Assert
        Assert.Equal(600, envelope.LevelsDb.Length);
        Assert.Equal(0.05, envelope.WindowSeconds);
    }

    [Fact(DisplayName = "windows_span_block_boundaries")]
    public void Add_InSmallBlocks_GivesTheSameEnvelopeAsOneBlock()
    {
        // Arrange
        var samples = SyntheticSignal.Sine(3);
        var whole = SyntheticSignal.Envelope(samples);
        var chunked = new RmsEnvelopeAccumulator(SyntheticSignal.SampleRate);

        // Act
        for (var offset = 0; offset < samples.Length; offset += 333)
            chunked.Add(samples.AsSpan(offset, Math.Min(333, samples.Length - offset)), 1);

        // Assert
        Assert.Equal(whole.LevelsDb, chunked.Build(0, 3).LevelsDb);
    }

    [Fact(DisplayName = "silence_is_reported_at_the_floor_level")]
    public void Build_Zeros_ReportsTheSilentLevel()
    {
        // Act
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Silence(1));

        // Assert
        Assert.All(envelope.LevelsDb, level => Assert.Equal((float)MixThresholds.SilentLevelDb, level));
    }

    [Fact(DisplayName = "build_keeps_the_start_and_track_length")]
    public void Build_KeepsStartAndTrackLength()
    {
        // Act
        var envelope = SyntheticSignal.Envelope(SyntheticSignal.Sine(1), startSeconds: 150, trackLengthSeconds: 180);

        // Assert
        Assert.Equal(150, envelope.StartSeconds);
        Assert.Equal(180, envelope.TrackLengthSeconds);
    }
}