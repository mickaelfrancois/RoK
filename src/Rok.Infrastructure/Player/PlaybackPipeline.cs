using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rok.Infrastructure.Player;

/// <summary>
/// Rendering chain shared by consecutive tracks: gapless chain, equalizer then volume.
/// <see cref="Output"/> plugs into any <see cref="IWavePlayer"/>.
/// </summary>
internal sealed class PlaybackPipeline : IDisposable
{
    internal static readonly float[] BandFrequencies = [32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f];

    private PlaybackPipeline(AudioFileReader reader, GaplessChainSampleProvider chain, Equalizer equalizer, VolumeSampleProvider volume)
    {
        Reader = reader;
        Chain = chain;
        Equalizer = equalizer;
        Volume = volume;
    }

    /// <summary>Reader of the track currently rendered; replaced on each gapless switch.</summary>
    public AudioFileReader Reader { get; set; }

    public GaplessChainSampleProvider Chain { get; }

    public Equalizer Equalizer { get; }

    public VolumeSampleProvider Volume { get; }

    public ISampleProvider Output => Volume;

    /// <summary>Builds a pipeline around <paramref name="reader"/> with the given band gains and volume.</summary>
    public static PlaybackPipeline Create(AudioFileReader reader, IReadOnlyList<float> bandGains, float volume)
    {
        GaplessChainSampleProvider chain = new(reader);
        Equalizer equalizer = new(chain, CreateBands(reader.WaveFormat.Channels, bandGains));
        VolumeSampleProvider volumeProvider = new(equalizer) { Volume = volume };

        return new PlaybackPipeline(reader, chain, equalizer, volumeProvider);
    }

    /// <summary>Creates one band per <see cref="BandFrequencies"/> entry, set to the matching gain of <paramref name="bandGains"/>.</summary>
    internal static EqualizerBand[] CreateBands(int channels, IReadOnlyList<float> bandGains)
    {
        EqualizerBand[] bands = new EqualizerBand[BandFrequencies.Length];

        for (int i = 0; i < bands.Length; i++)
        {
            bands[i] = new EqualizerBand(BandFrequencies[i], 1f, channels);

            if (i < bandGains.Count)
                bands[i].Gain = bandGains[i];
        }

        return bands;
    }

    public void Dispose()
    {
        (Chain.ClearNext() as IDisposable)?.Dispose();

        if (!ReferenceEquals(Chain.Current, Reader))
            (Chain.Current as IDisposable)?.Dispose();

        Reader.Dispose();
    }
}