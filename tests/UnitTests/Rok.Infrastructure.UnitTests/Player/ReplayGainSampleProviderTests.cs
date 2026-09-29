using NAudio.Wave;
using Rok.Infrastructure.Player;

namespace Rok.Infrastructure.UnitTests.Player;

public class ReplayGainSampleProviderTests
{
    [Fact(DisplayName = "read_multiplies_every_sample_by_the_gain")]
    public void Read_MultipliesEverySampleByGain()
    {
        // Arrange
        ReplayGainSampleProvider provider = new(new ConstantSampleProvider(sampleCount: 8, value: 0.8f), gain: 0.5f);
        float[] buffer = new float[8];

        // Act
        int read = provider.Read(buffer);

        // Assert
        Assert.Equal(8, read);
        Assert.All(buffer, sample => Assert.Equal(0.4f, sample, 1e-6f));
    }

    [Fact(DisplayName = "read_leaves_the_signal_untouched_with_a_unity_gain")]
    public void Read_LeavesSignalUntouched_WithUnityGain()
    {
        // Arrange
        ReplayGainSampleProvider provider = new(new ConstantSampleProvider(sampleCount: 4, value: 0.3f), gain: 1f);
        float[] buffer = new float[4];

        // Act
        provider.Read(buffer);

        // Assert
        Assert.All(buffer, sample => Assert.Equal(0.3f, sample));
    }

    [Fact(DisplayName = "read_keeps_samples_at_or_below_full_scale_with_a_peak_capped_gain")]
    public void Read_KeepsSamplesAtOrBelowFullScale_WithPeakCappedGain()
    {
        // Arrange
        const float peak = 0.9f;
        ReplayGainSampleProvider provider = new(new ConstantSampleProvider(sampleCount: 16, value: peak), gain: 1f / peak);
        float[] buffer = new float[16];

        // Act
        provider.Read(buffer);

        // Assert
        Assert.All(buffer, sample => Assert.True(sample <= 1.0f + 1e-6f));
    }

    [Fact(DisplayName = "read_leaves_the_span_beyond_the_source_count_untouched")]
    public void Read_LeavesSpanBeyondSourceCountUntouched()
    {
        // Arrange
        const float sentinel = -999f;
        ReplayGainSampleProvider provider = new(new ConstantSampleProvider(sampleCount: 3, value: 0.5f), gain: 0.5f);
        float[] buffer = [.. Enumerable.Repeat(sentinel, 6)];

        // Act
        int read = provider.Read(buffer);

        // Assert
        Assert.Equal(3, read);
        Assert.All(buffer[3..], sample => Assert.Equal(sentinel, sample));
    }

    [Fact(DisplayName = "gain_changes_apply_from_the_next_buffer")]
    public void Gain_ChangesApplyFromNextBuffer()
    {
        // Arrange
        ReplayGainSampleProvider provider = new(new ConstantSampleProvider(sampleCount: 8, value: 1f), gain: 1f);
        float[] first = new float[4];
        float[] second = new float[4];

        // Act
        provider.Read(first);
        provider.Gain = 0.25f;
        provider.Read(second);

        // Assert
        Assert.All(first, sample => Assert.Equal(1f, sample));
        Assert.All(second, sample => Assert.Equal(0.25f, sample));
    }

    [Fact(DisplayName = "wave_format_is_the_format_of_the_source")]
    public void WaveFormat_IsFormatOfSource()
    {
        // Arrange
        ConstantSampleProvider source = new(sampleCount: 1, value: 0f, sampleRate: 48000, channels: 1);

        // Act
        ReplayGainSampleProvider provider = new(source, gain: 1f);

        // Assert
        Assert.Same(source.WaveFormat, provider.WaveFormat);
    }

    [Fact(DisplayName = "dispose_disposes_the_source")]
    public void Dispose_DisposesSource()
    {
        // Arrange
        ConstantSampleProvider source = new(sampleCount: 1, value: 0f);

        // Act
        using (new ReplayGainSampleProvider(source, gain: 1f))
        {
        }

        // Assert
        Assert.True(source.IsDisposed);
    }

    [Fact(DisplayName = "gapless_chain_keeps_each_source_gain_across_the_switch")]
    public void GaplessChain_KeepsEachSourceGainAcrossSwitch()
    {
        // Arrange
        ReplayGainSampleProvider first = new(new ConstantSampleProvider(sampleCount: 4, value: 1f), gain: 0.5f);
        ReplayGainSampleProvider second = new(new ConstantSampleProvider(sampleCount: 100, value: 1f), gain: 0.25f);
        GaplessChainSampleProvider chain = new(first);
        chain.TryQueueNext(second, out _);
        float[] buffer = new float[8];

        // Act
        chain.Read(buffer);

        // Assert
        Assert.All(buffer[..4], sample => Assert.Equal(0.5f, sample));
        Assert.All(buffer[4..], sample => Assert.Equal(0.25f, sample));
    }

    [Fact(DisplayName = "gapless_chain_rejects_a_wrapped_source_with_a_different_format")]
    public void GaplessChain_RejectsWrappedSourceWithDifferentFormat()
    {
        // Arrange
        GaplessChainSampleProvider chain = new(new ReplayGainSampleProvider(new ConstantSampleProvider(4, 1f, sampleRate: 44100), 1f));
        ReplayGainSampleProvider other = new(new ConstantSampleProvider(4, 1f, sampleRate: 48000), 1f);

        // Act
        bool queued = chain.TryQueueNext(other, out _);

        // Assert
        Assert.False(queued);
    }

    private sealed class ConstantSampleProvider(int sampleCount, float value, int sampleRate = 44100, int channels = 2) : ISampleProvider, IDisposable
    {
        private int _remaining = sampleCount;

        public bool IsDisposed { get; private set; }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        public int Read(Span<float> buffer)
        {
            int count = Math.Min(_remaining, buffer.Length);
            buffer[..count].Fill(value);
            _remaining -= count;

            return count;
        }

        public void Dispose() => IsDisposed = true;
    }
}