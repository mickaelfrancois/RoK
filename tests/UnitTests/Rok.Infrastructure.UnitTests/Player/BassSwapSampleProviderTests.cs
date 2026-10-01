using NAudio.Wave;
using Rok.Application.Player.Mix;
using Rok.Infrastructure.Player;

namespace Rok.Infrastructure.UnitTests.Player;

public class BassSwapSampleProviderTests
{
    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const double MixSeconds = 4;

    [Fact(DisplayName = "signal_is_bit_exact_without_bass_swap")]
    public void Read_IsBitExact_WithoutBassSwap()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        float[] expected = Render(new SineSampleProvider(60), SampleRate);

        // Act
        float[] actual = Render(sut, SampleRate);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "incoming_bass_is_cut_by_at_least_twenty_db_at_the_start_of_the_mix")]
    public void Incoming_CutsBass_AtStart()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        sut.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));

        // Act
        float[] output = Render(sut, SampleRate / 2);

        // Assert
        Assert.True(AttenuationDb(output) >= 20);
    }

    [Fact(DisplayName = "incoming_treble_is_kept_within_one_db_at_the_start_of_the_mix")]
    public void Incoming_KeepsTreble_AtStart()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(2000));
        sut.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));

        // Act
        float[] output = Render(sut, SampleRate / 2);

        // Assert
        Assert.True(Math.Abs(AttenuationDb(output)) < 1);
    }

    [Fact(DisplayName = "outgoing_signal_is_bit_exact_before_the_ramp")]
    public void Outgoing_IsBitExact_BeforeRamp()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        sut.Start(EBassSwapRole.Outgoing, TimeSpan.FromSeconds(MixSeconds));
        float[] expected = Render(new SineSampleProvider(60), SampleRate);

        // Act
        float[] actual = Render(sut, SampleRate);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "outgoing_bass_is_cut_by_at_least_twenty_db_at_the_end_of_the_mix")]
    public void Outgoing_CutsBass_AtEnd()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        sut.Start(EBassSwapRole.Outgoing, TimeSpan.FromSeconds(MixSeconds));
        Render(sut, SampleRate * 3);

        // Act
        float[] output = Render(sut, SampleRate / 2);

        // Assert
        Assert.True(AttenuationDb(output) >= 20);
    }

    [Fact(DisplayName = "incoming_signal_is_bit_exact_once_the_bass_is_restored")]
    public void Incoming_IsBitExact_AfterRamp()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        sut.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));
        Render(sut, SampleRate * 3);
        SineSampleProvider reference = new(60);
        Render(reference, SampleRate * 3);

        // Act
        float[] actual = Render(sut, SampleRate / 2);

        // Assert
        Assert.Equal(Render(reference, SampleRate / 2), actual);
    }

    [Theory(DisplayName = "ramp_has_no_discontinuity")]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(500)]
    public void Ramp_HasNoDiscontinuity(int blockFrames)
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        sut.Start(EBassSwapRole.Outgoing, TimeSpan.FromSeconds(MixSeconds));
        float[] output = Render(sut, SampleRate * 4, blockFrames * Channels);

        // Act
        float maxStep = 0;

        for (int i = Channels; i < output.Length; i++)
            maxStep = Math.Max(maxStep, Math.Abs(output[i] - output[i - Channels]));

        // Assert
        double sineMaxStep = 0.5 * 2 * Math.PI * 60 / SampleRate;
        Assert.True(maxStep < sineMaxStep * 1.5);
    }

    [Fact(DisplayName = "reset_restores_the_untouched_signal")]
    public void Reset_RestoresUntouchedSignal()
    {
        // Arrange
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        sut.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));
        SineSampleProvider reference = new(60);
        Render(sut, SampleRate / 2);
        Render(reference, SampleRate / 2);
        bool activeBeforeReset = sut.IsActive;
        sut.Reset();

        // Act
        float[] actual = Render(sut, SampleRate / 2);

        // Assert
        Assert.True(activeBeforeReset);
        Assert.False(sut.IsActive);
        Assert.Equal(Render(reference, SampleRate / 2), actual);
    }

    [Fact(DisplayName = "low_shelf_at_zero_db_has_a_unit_gain_numerator_equal_to_the_denominator")]
    public void ComputeLowShelf_AtZeroDb_IsTransparent()
    {
        // Act
        BassSwapSampleProvider.ComputeLowShelf(SampleRate, BassSwapCurve.CutoffHz, 0, out double b0, out double b1, out double b2, out double a1, out double a2);

        // Assert
        Assert.Equal(1, b0, 1e-12);
        Assert.Equal(a1, b1, 1e-12);
        Assert.Equal(a2, b2, 1e-12);
    }

    [Theory(DisplayName = "read_sizes_not_aligned_on_the_block_or_the_channels_do_not_throw_and_swap_at_the_same_time")]
    [InlineData(100)]
    [InlineData(127)]
    [InlineData(333)]
    public void Read_WithUnalignedSizes_MatchesBlockAlignedRead(int chunkSamples)
    {
        // Arrange
        BassSwapSampleProvider reference = new(new SineSampleProvider(60));
        BassSwapSampleProvider sut = new(new SineSampleProvider(60));
        reference.Start(EBassSwapRole.Outgoing, TimeSpan.FromSeconds(MixSeconds));
        sut.Start(EBassSwapRole.Outgoing, TimeSpan.FromSeconds(MixSeconds));
        float[] expected = Render(reference, SampleRate * 4, 64 * Channels);

        // Act
        float[] actual = Render(sut, SampleRate * 4, chunkSamples);

        // Assert
        double maxDifference = 0;

        for (int i = 0; i < expected.Length; i++)
            maxDifference = Math.Max(maxDifference, Math.Abs(expected[i] - actual[i]));

        Assert.True(maxDifference < 0.02);
    }

    [Fact(DisplayName = "restarting_the_bass_swap_clears_the_history_of_the_previous_mix")]
    public void Start_Restarted_ClearsFilterHistory()
    {
        // Arrange
        BassSwapSampleProvider restarted = new(new SineSampleProvider(60));
        restarted.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));
        Render(restarted, SampleRate / 2);
        BassSwapSampleProvider fresh = new(new SineSampleProvider(60));
        Render(fresh, SampleRate / 2);

        // Act
        restarted.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));
        fresh.Start(EBassSwapRole.Incoming, TimeSpan.FromSeconds(MixSeconds));
        float[] actual = Render(restarted, SampleRate / 2);
        float[] expected = Render(fresh, SampleRate / 2);

        // Assert
        Assert.Equal(expected, actual);
    }

    private static float[] Render(ISampleProvider provider, int frames, int chunkSamples = 1000)
    {
        float[] buffer = new float[frames * Channels];
        int done = 0;

        while (done < buffer.Length)
        {
            int chunk = Math.Min(chunkSamples, buffer.Length - done);
            int read = provider.Read(buffer.AsSpan(done, chunk));

            if (read == 0)
                break;

            done += read;
        }

        return buffer;
    }

    private static double AttenuationDb(float[] output)
    {
        int skip = output.Length / 4;
        double sum = 0;

        for (int i = skip; i < output.Length; i++)
            sum += output[i] * output[i];

        double rms = Math.Sqrt(sum / (output.Length - skip));
        double inputRms = 0.5 / Math.Sqrt(2);

        return 20 * Math.Log10(inputRms / rms);
    }

    private sealed class SineSampleProvider(double frequency) : ISampleProvider
    {
        private long _frame;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

        public int Read(Span<float> buffer)
        {
            int samples = buffer.Length - (buffer.Length % Channels);

            for (int i = 0; i < samples; i += Channels)
            {
                float value = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * _frame / SampleRate));

                for (int channel = 0; channel < Channels; channel++)
                    buffer[i + channel] = value;

                _frame++;
            }

            return samples;
        }
    }
}