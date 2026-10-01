using NAudio.Wave;
using Rok.Infrastructure.Player;

namespace Rok.Infrastructure.UnitTests.Player;

public class FadeSampleProviderTests
{
    private const int SampleRate = 1000;
    private const int Channels = 2;

    [Theory(DisplayName = "fade_curve_keeps_the_summed_power_constant")]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void Gain_IsEqualPower(double progress)
    {
        // Act
        float fadeIn = FadeSampleProvider.Gain(EFadeDirection.In, progress);
        float fadeOut = FadeSampleProvider.Gain(EFadeDirection.Out, progress);

        // Assert
        Assert.Equal(1.0, (fadeIn * fadeIn) + (fadeOut * fadeOut), 5);
    }

    [Fact(DisplayName = "fade_curve_has_no_dip_at_mid_point")]
    public void Gain_HasNoDipAtMidPoint()
    {
        // Act
        float fadeIn = FadeSampleProvider.Gain(EFadeDirection.In, 0.5);
        float fadeOut = FadeSampleProvider.Gain(EFadeDirection.Out, 0.5);

        // Assert
        Assert.Equal(Math.Sqrt(0.5), fadeIn, 5);
        Assert.Equal(Math.Sqrt(0.5), fadeOut, 5);
    }

    [Fact(DisplayName = "signal_passes_through_untouched_without_fade")]
    public void Read_PassesThrough_WithoutFade()
    {
        // Arrange
        FadeSampleProvider sut = new(new ConstantSampleProvider(0.8f));
        float[] buffer = new float[200];

        // Act
        sut.Read(buffer);

        // Assert
        Assert.All(buffer, sample => Assert.Equal(0.8f, sample));
        Assert.False(sut.IsComplete);
    }

    [Fact(DisplayName = "fade_out_ramps_down_sample_by_sample_and_ends_in_silence")]
    public void FadeOut_EndsInSilence()
    {
        // Arrange
        FadeSampleProvider sut = new(new ConstantSampleProvider(1f));
        sut.Start(EFadeDirection.Out, TimeSpan.FromSeconds(1));
        float[] ramp = new float[SampleRate * Channels];
        float[] after = new float[100];

        // Act
        sut.Read(ramp);
        sut.Read(after);

        // Assert
        Assert.Equal(1f, ramp[0], 5);
        Assert.Equal(ramp[0], ramp[1]);
        for (int i = Channels; i < ramp.Length; i += Channels)
            Assert.True(ramp[i] <= ramp[i - Channels]);
        Assert.True(sut.IsComplete);
        Assert.All(after, sample => Assert.Equal(0f, sample, 5));
    }

    [Fact(DisplayName = "fade_in_starts_from_silence_and_ends_at_unity")]
    public void FadeIn_EndsAtUnity()
    {
        // Arrange
        FadeSampleProvider sut = new(new ConstantSampleProvider(1f));
        sut.Start(EFadeDirection.In, TimeSpan.FromSeconds(1));
        float[] firstHalf = new float[SampleRate];
        float[] secondHalf = new float[SampleRate];
        float[] after = new float[100];

        // Act
        sut.Read(firstHalf);
        bool completeAtHalf = sut.IsComplete;
        sut.Read(secondHalf);
        sut.Read(after);

        // Assert
        Assert.Equal(0f, firstHalf[0], 5);
        Assert.Equal(Math.Sqrt(0.5), secondHalf[0], 2);
        Assert.False(completeAtHalf);
        Assert.True(sut.IsComplete);
        Assert.All(after, sample => Assert.Equal(1f, sample, 5));
    }

    [Fact(DisplayName = "reset_cancels_a_fade_out_and_restores_the_signal")]
    public void Reset_RestoresSignal()
    {
        // Arrange
        FadeSampleProvider sut = new(new ConstantSampleProvider(0.5f));
        sut.Start(EFadeDirection.Out, TimeSpan.FromSeconds(1));
        sut.Read(new float[SampleRate * Channels]);
        float[] buffer = new float[100];

        // Act
        sut.Reset();
        sut.Read(buffer);

        // Assert
        Assert.All(buffer, sample => Assert.Equal(0.5f, sample));
    }

    private sealed class ConstantSampleProvider(float value) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

        public int Read(Span<float> buffer)
        {
            buffer.Fill(value);
            return buffer.Length;
        }
    }
}