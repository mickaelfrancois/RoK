using NAudio.Wave;

namespace Rok.Infrastructure.Player;

/// <summary>
/// Applies a per-track linear ReplayGain factor to <see cref="Source"/>. The gain can be changed from any thread
/// while the rendering thread reads: it is taken once per buffer, without locking.
/// </summary>
public sealed class ReplayGainSampleProvider(ISampleProvider source, float gain) : ISampleProvider, IDisposable
{
    private volatile float _gain = gain;

    /// <summary>Wrapped source, typically the <see cref="AudioFileReader"/> of one track.</summary>
    public ISampleProvider Source { get; } = source;

    /// <summary>Format of <see cref="Source"/>, so that the gapless chain compares the real track formats.</summary>
    public WaveFormat WaveFormat => Source.WaveFormat;

    /// <summary>Linear factor applied to every sample; 1 leaves the signal untouched.</summary>
    public float Gain
    {
        get => _gain;
        set => _gain = value;
    }

    /// <summary>Reads from <see cref="Source"/> and multiplies the samples by <see cref="Gain"/>.</summary>
    public int Read(Span<float> buffer)
    {
        int read = Source.Read(buffer);
        float gain = _gain;

        if (gain == 1f)
            return read;

        Span<float> samples = buffer[..read];

        for (int i = 0; i < samples.Length; i++)
            samples[i] *= gain;

        return read;
    }

    public void Dispose() => (Source as IDisposable)?.Dispose();
}