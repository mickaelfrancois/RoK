using NAudio.Wave;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.Player;

public class Equalizer : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;
    private readonly EqualizerBand[] _bands;
    private readonly int _channels;
    private readonly int _sampleRate;
    private volatile bool _isFlat;

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public Equalizer(ISampleProvider sourceProvider, params EqualizerBand[] bands)
    {
        _sourceProvider = sourceProvider;
        _bands = bands;
        _channels = sourceProvider.WaveFormat.Channels;
        _sampleRate = sourceProvider.WaveFormat.SampleRate;

        foreach (EqualizerBand band in bands)
        {
            band.SetGain(band.Gain, _sampleRate);
        }

        _isFlat = AreAllBandsFlat();
    }

    /// <summary>All bands at 0 dB: samples pass through untouched, which keeps an exclusive output bit-perfect.</summary>
    public bool IsFlat => _isFlat;

    public int Read(Span<float> buffer)
    {
        int samplesRead = _sourceProvider.Read(buffer);

        if (_isFlat)
            return samplesRead;

        for (int i = 0; i < samplesRead; i++)
        {
            int channel = i % _channels;

            foreach (EqualizerBand band in _bands)
            {
                buffer[i] = band.Transform(buffer[i], channel);
            }
        }

        return samplesRead;
    }

    public void UpdateBand(int bandIndex, float gain)
    {
        if (bandIndex >= 0 && bandIndex < _bands.Length)
        {
            _bands[bandIndex].SetGain(gain, _sampleRate);
        }

        _isFlat = AreAllBandsFlat();
    }

    private bool AreAllBandsFlat() => Array.TrueForAll(_bands, band => band.Gain.EqualsZero());

    public EqualizerBand[] GetBands() => _bands;
}