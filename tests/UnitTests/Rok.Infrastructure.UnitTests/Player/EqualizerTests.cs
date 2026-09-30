using NAudio.Wave;
using Rok.Infrastructure.Player;

namespace Rok.Infrastructure.UnitTests.Player;

public class EqualizerTests
{
    [Fact(DisplayName = "read_leaves_the_span_beyond_the_source_sample_count_untouched")]
    public void Read_LeavesSpanBeyondSourceSampleCount_Untouched()
    {
        // Arrange
        const float sentinel = -999f;
        StubSampleProvider source = new(channels: 2, samplesToReturn: 4);
        Equalizer equalizer = new(source, new EqualizerBand(1000f, 1f, 2));
        equalizer.UpdateBand(0, 12f);

        float[] buffer = [.. Enumerable.Repeat(sentinel, 8)];

        // Act
        int samplesRead = equalizer.Read(buffer);

        // Assert
        Assert.Equal(4, samplesRead);
        Assert.All(buffer[4..], sample => Assert.Equal(sentinel, sample));
    }

    [Fact(DisplayName = "read_applies_the_band_gain_from_the_start_of_the_span")]
    public void Read_AppliesBandGain_FromStartOfSpan()
    {
        // Arrange
        StubSampleProvider source = new(channels: 1, samplesToReturn: 4);
        Equalizer equalizer = new(source, new EqualizerBand(1000f, 1f, 1));
        equalizer.UpdateBand(0, 12f);

        float[] buffer = new float[4];

        // Act
        int samplesRead = equalizer.Read(buffer);

        // Assert
        Assert.Equal(4, samplesRead);
        Assert.NotEqual(StubSampleProvider.SampleValue, buffer[0]);
    }

    [Fact(DisplayName = "flat_equalizer_passes_samples_unchanged")]
    public void Read_PassesSamplesUnchanged_WhenAllBandsAreFlat()
    {
        // Arrange
        Random random = new(42);
        float[] input = [.. Enumerable.Range(0, 4096).Select(_ => (float)((random.NextDouble() * 2) - 1))];
        Equalizer equalizer = new(new ArraySampleProvider(input), PlaybackPipeline.CreateBands(1, []));
        float[] buffer = new float[input.Length];

        // Act
        int samplesRead = equalizer.Read(buffer);

        // Assert
        Assert.True(equalizer.IsFlat);
        Assert.Equal(input.Length, samplesRead);
        Assert.Equal(input, buffer);
    }

    [Fact(DisplayName = "equalizer_is_no_longer_flat_once_a_band_has_a_gain")]
    public void IsFlat_BecomesFalse_WhenABandHasAGain()
    {
        // Arrange
        Equalizer equalizer = new(new ArraySampleProvider([0f]), PlaybackPipeline.CreateBands(1, []));

        // Act
        equalizer.UpdateBand(3, 2f);
        bool afterGain = equalizer.IsFlat;
        equalizer.UpdateBand(3, 0f);

        // Assert
        Assert.False(afterGain);
        Assert.True(equalizer.IsFlat);
    }

    [Fact(DisplayName = "read_returns_zero_when_the_source_is_exhausted")]
    public void Read_ReturnsZero_WhenSourceIsExhausted()
    {
        // Arrange
        StubSampleProvider source = new(channels: 2, samplesToReturn: 0);
        Equalizer equalizer = new(source, new EqualizerBand(1000f, 1f, 2));

        float[] buffer = new float[8];

        // Act
        int samplesRead = equalizer.Read(buffer);

        // Assert
        Assert.Equal(0, samplesRead);
    }

    private sealed class ArraySampleProvider(float[] samples) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        public int Read(Span<float> buffer)
        {
            int count = Math.Min(buffer.Length, samples.Length);
            samples.AsSpan(0, count).CopyTo(buffer);

            return count;
        }
    }

    private sealed class StubSampleProvider(int channels, int samplesToReturn) : ISampleProvider
    {
        public const float SampleValue = 1f;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, channels);

        public int Read(Span<float> buffer)
        {
            int count = Math.Min(samplesToReturn, buffer.Length);
            buffer[..count].Fill(SampleValue);

            return count;
        }
    }
}