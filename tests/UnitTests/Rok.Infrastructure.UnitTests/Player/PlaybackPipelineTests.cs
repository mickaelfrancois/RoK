using Rok.Infrastructure.Player;

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