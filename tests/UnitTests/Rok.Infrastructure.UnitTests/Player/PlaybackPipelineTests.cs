using NAudio.Wave;
using Rok.Infrastructure.Player;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player;

public class PlaybackPipelineTests
{
    [Fact(DisplayName = "create_bands_copies_the_given_gains_onto_the_ten_frequencies")]
    public void CreateBands_CopiesGivenGains_OntoTenFrequencies()
    {
        // Arrange
        float[] gains = [1f, 2f, 3f, 4f, 5f, -1f, -2f, -3f, -4f, -5f];

        // Act
        EqualizerBand[] bands = PlaybackPipeline.CreateBands(channels: 2, gains);

        // Assert
        Assert.Equal(PlaybackPipeline.BandFrequencies, bands.Select(band => band.Frequency));
        Assert.Equal(gains, bands.Select(band => band.Gain));
    }

    [Fact(DisplayName = "neutral_pipeline_is_bit_exact_for_24_bit_wav")]
    public void NeutralPipeline_IsBitExact_ForTwentyFourBitWav()
    {
        // Arrange
        Random random = new(42);
        int[] values = [-8388608, 8388607, 0, 1, -1, .. Enumerable.Range(0, 4096).Select(_ => random.Next(-8388608, 8388608))];
        byte[] data = new byte[values.Length * 3 * 2];

        for (int i = 0; i < values.Length * 2; i++)
        {
            int value = values[i % values.Length];
            data[i * 3] = (byte)value;
            data[(i * 3) + 1] = (byte)(value >> 8);
            data[(i * 3) + 2] = (byte)(value >> 16);
        }

        using TestWaveFile file = TestWaveFile.Create(96000, 24, 2, data);
        using PlaybackPipeline pipeline = PlaybackPipeline.Create(new AudioFileReader(file.Path), 1f, 1, [], 1f);
        WaveFormatExtensible negotiated = ExclusiveFormatLadder.Candidates(96000, 2, 24)[0];
        FloatToPcmWaveProvider output = new(pipeline.Output, negotiated);
        byte[] rendered = new byte[data.Length];

        // Act
        int read = 0;
        int chunk;

        while (read < rendered.Length && (chunk = output.Read(rendered.AsSpan(read))) > 0)
            read += chunk;

        // Assert
        Assert.Equal(data.Length, read);
        Assert.Equal(data, rendered);
    }

    [Fact(DisplayName = "create_bands_leaves_bands_flat_when_no_gain_is_given")]
    public void CreateBands_LeavesBandsFlat_WhenNoGainIsGiven()
    {
        // Act
        EqualizerBand[] bands = PlaybackPipeline.CreateBands(channels: 2, []);

        // Assert
        Assert.Equal(10, bands.Length);
        Assert.All(bands, band => Assert.Equal(0f, band.Gain));
    }
}