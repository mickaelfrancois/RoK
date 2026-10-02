using Rok.Application.Player.Mix.Tempo;

namespace Rok.ApplicationTests.Player.Mix.Tempo;

public class MonoDecimatorTests
{
    [Theory(DisplayName = "decimation_brings_the_rate_close_to_the_target")]
    [InlineData(44100, 4, 11025)]
    [InlineData(48000, 4, 12000)]
    [InlineData(88200, 8, 11025)]
    [InlineData(8000, 1, 8000)]
    public void Factor_And_OutputRate(int sampleRate, int expectedFactor, int expectedRate)
    {
        // Act
        var decimator = new MonoDecimator(sampleRate);

        // Assert
        Assert.Equal(expectedFactor, decimator.Factor);
        Assert.Equal(expectedRate, decimator.OutputSampleRate);
    }

    [Theory(DisplayName = "decimation_output_length_matches_the_input_duration")]
    [InlineData(44100)]
    [InlineData(48000)]
    public void Build_HasExpectedLength(int sampleRate)
    {
        // Arrange
        var decimator = new MonoDecimator(sampleRate);
        var frames = sampleRate * 2;
        var interleaved = new float[frames * 2];

        // Act
        decimator.Add(interleaved, 2);
        var output = decimator.Build();

        // Assert
        Assert.Equal(2 * decimator.OutputSampleRate, output.Length);
        Assert.Equal(output.Length, decimator.SampleCount);
    }

    [Fact(DisplayName = "decimation_averages_stereo_channels_and_keeps_a_constant")]
    public void Add_AveragesChannels_AndKeepsConstant()
    {
        // Arrange
        var decimator = new MonoDecimator(44100);
        var interleaved = new float[44100 * 2];

        for (var i = 0; i < interleaved.Length; i += 2)
        {
            interleaved[i] = 0.8f;
            interleaved[i + 1] = 0.2f;
        }

        // Act
        decimator.Add(interleaved, 2);
        var output = decimator.Build();

        // Assert
        Assert.NotEmpty(output);
        Assert.All(output, sample => Assert.Equal(0.5f, sample, 1e-5f));
    }

    [Fact(DisplayName = "decimation_is_continuous_across_blocks")]
    public void Add_IsContinuousAcrossBlocks()
    {
        // Arrange
        var whole = new MonoDecimator(44100);
        var split = new MonoDecimator(44100);
        var samples = SyntheticSignal.WhiteNoise(1, 44100);

        // Act
        whole.Add(samples, 1);
        split.Add(samples.AsSpan(0, 1001), 1);
        split.Add(samples.AsSpan(1001), 1);

        // Assert
        Assert.Equal(whole.Build(), split.Build());
    }
}