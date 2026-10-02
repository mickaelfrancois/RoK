using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rok.Infrastructure.Player;

/// <summary>
/// Rendering chain shared by consecutive tracks: per-track ReplayGain, gapless chain, Mix time-stretch, equalizer, bass swap, volume then crossfade ramp.
/// <see cref="Output"/> plugs into any <see cref="IWavePlayer"/>.
/// </summary>
internal sealed class PlaybackPipeline : IDisposable
{
    internal static readonly float[] BandFrequencies = [32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f];

    private PlaybackPipeline(AudioFileReader reader, ReplayGainSampleProvider source, long trackId, GaplessChainSampleProvider chain, TimeStretchSampleProvider timeStretch, Equalizer equalizer, BassSwapSampleProvider bassSwap, VolumeSampleProvider volume, FadeSampleProvider fade)
    {
        Reader = reader;
        Source = source;
        TrackId = trackId;
        Chain = chain;
        TimeStretch = timeStretch;
        Equalizer = equalizer;
        BassSwap = bassSwap;
        Volume = volume;
        Fade = fade;
    }

    /// <summary>Reader of the track currently rendered; replaced on each gapless switch.</summary>
    public AudioFileReader Reader { get; set; }

    /// <summary>ReplayGain wrapper around <see cref="Reader"/>; replaced on each gapless switch.</summary>
    public ReplayGainSampleProvider Source { get; set; }

    /// <summary>Identifier of the track currently rendered; replaced on each gapless switch.</summary>
    public long TrackId { get; set; }

    public GaplessChainSampleProvider Chain { get; }

    /// <summary>Tempo stretch of the Mix incoming track, a bit-exact pass-through outside a Mix transition.</summary>
    public TimeStretchSampleProvider TimeStretch { get; }

    public Equalizer Equalizer { get; }

    /// <summary>Low-shelf cut of the Mix bass swap, a pass-through outside a bass swap.</summary>
    public BassSwapSampleProvider BassSwap { get; }

    public VolumeSampleProvider Volume { get; }

    /// <summary>Crossfade ramp, a pass-through outside a crossfade.</summary>
    public FadeSampleProvider Fade { get; }

    public ISampleProvider Output => Fade;

    /// <summary>Builds a pipeline around <paramref name="reader"/> with its ReplayGain, the band gains and the volume.</summary>
    public static PlaybackPipeline Create(AudioFileReader reader, float replayGain, long trackId, IReadOnlyList<float> bandGains, float volume)
    {
        ReplayGainSampleProvider source = new(reader, replayGain);
        GaplessChainSampleProvider chain = new(source);
        TimeStretchSampleProvider timeStretch = new(chain);
        Equalizer equalizer = new(timeStretch, CreateBands(reader.WaveFormat.Channels, bandGains));
        BassSwapSampleProvider bassSwap = new(equalizer);
        VolumeSampleProvider volumeProvider = new(bassSwap) { Volume = volume };

        return new PlaybackPipeline(reader, source, trackId, chain, timeStretch, equalizer, bassSwap, volumeProvider, new FadeSampleProvider(volumeProvider));
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

        if (!ReferenceEquals(Chain.Current, Source))
            (Chain.Current as IDisposable)?.Dispose();

        Source.Dispose();
    }
}