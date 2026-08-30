using NAudio.Wave;

namespace Rok.Infrastructure.Player;

public class Equalizer : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;
    private readonly EqualizerBand[] _bands;
    private readonly int _channels;
    private readonly int _sampleRate;

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
    }

    public int Read(Span<float> buffer)
    {
        int samplesRead = _sourceProvider.Read(buffer);

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
    }

    public EqualizerBand[] GetBands() => _bands;
}