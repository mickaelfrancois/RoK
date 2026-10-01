using NAudio.Wave;

namespace Rok.Infrastructure.Player;

/// <summary>Direction of a crossfade ramp.</summary>
internal enum EFadeDirection
{
    In,
    Out
}

/// <summary>
/// Applies a crossfade ramp sample by sample, on the rendering thread. Equal-power curve: the incoming gain is
/// sin(p·π/2) and the outgoing gain cos(p·π/2), so the summed power stays constant and the transition has no dip.
/// Passes the signal through untouched until a fade starts; an outgoing fade ends in silence, an incoming one in unity.
/// </summary>
internal sealed class FadeSampleProvider(ISampleProvider source) : ISampleProvider
{
    private readonly Lock _lock = new();
    private EFadeDirection? _direction;
    private long _framesTotal;
    private long _framesDone;

    public WaveFormat WaveFormat => source.WaveFormat;

    /// <summary>The ramp started by <see cref="Start"/> has been fully rendered.</summary>
    public bool IsComplete
    {
        get
        {
            lock (_lock)
            {
                return _direction is not null && _framesDone >= _framesTotal;
            }
        }
    }

    /// <summary>Starts a ramp of <paramref name="duration"/>, counted in rendered frames from the next read.</summary>
    public void Start(EFadeDirection direction, TimeSpan duration)
    {
        lock (_lock)
        {
            _direction = direction;
            _framesTotal = Math.Max(1, (long)(duration.TotalSeconds * WaveFormat.SampleRate));
            _framesDone = 0;
        }
    }

    /// <summary>Cancels any ramp: the signal passes through untouched again.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _direction = null;
        }
    }

    /// <summary>Gain of the ramp at <paramref name="progress"/> (0 to 1).</summary>
    public static float Gain(EFadeDirection direction, double progress)
    {
        double angle = Math.Clamp(progress, 0.0, 1.0) * Math.PI / 2.0;

        return (float)(direction == EFadeDirection.In ? Math.Sin(angle) : Math.Cos(angle));
    }

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        EFadeDirection direction;
        long framesTotal;
        long framesDone;

        lock (_lock)
        {
            if (_direction is null)
                return read;

            direction = _direction.Value;
            framesTotal = _framesTotal;
            framesDone = _framesDone;
            _framesDone = Math.Min(framesTotal, framesDone + ((read + WaveFormat.Channels - 1) / WaveFormat.Channels));
        }

        int channels = WaveFormat.Channels;

        for (int frame = 0; frame * channels < read; frame++)
        {
            float gain = Gain(direction, (double)(framesDone + frame) / framesTotal);
            int offset = frame * channels;

            for (int channel = 0; channel < channels && offset + channel < read; channel++)
                buffer[offset + channel] *= gain;
        }

        return read;
    }
}