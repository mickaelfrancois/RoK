using NAudio.Wave;
using Rok.Application.Player.Mix;

namespace Rok.Infrastructure.Player;

/// <summary>
/// Low-shelf filter of the Mix bass swap, applied on the rendering thread. The cut follows <see cref="BassSwapCurve"/>
/// over the duration of the mix, with the switch at the requested position (the middle by default); the filter is an allocation-free RBJ biquad whose coefficients are refreshed every
/// <see cref="BlockFrames"/> frames. Passes the signal through untouched until a bass swap starts and while the cut is zero.
/// </summary>
internal sealed class BassSwapSampleProvider : ISampleProvider
{
    private const int BlockFrames = 64;

    private readonly ISampleProvider _source;
    private readonly Lock _lock = new();
    private readonly double[] _z1;
    private readonly double[] _z2;
    private bool _active;
    private bool _stateDirty;
    private EBassSwapRole _role;
    private long _framesTotal;
    private long _framesDone;
    private double _swapAtSeconds;
    private double _appliedCut = -1;
    private double _b0;
    private double _b1;
    private double _b2;
    private double _a1;
    private double _a2;

    public BassSwapSampleProvider(ISampleProvider source)
    {
        _source = source;
        _z1 = new double[source.WaveFormat.Channels];
        _z2 = new double[source.WaveFormat.Channels];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Whether a bass swap is currently running (read under lock, for tests and diagnostics).</summary>
    public bool IsActive
    {
        get
        {
            lock (_lock)
                return _active;
        }
    }

    /// <summary>Starts the bass swap of a mix of <paramref name="mixDuration"/>, counted in rendered frames from the next read.</summary>
    public void Start(EBassSwapRole role, TimeSpan mixDuration) => Start(role, mixDuration, mixDuration / 2);

    /// <summary>
    /// Starts the bass swap of a mix of <paramref name="mixDuration"/>, the switch ramp being centred
    /// <paramref name="swapAt"/> after the start of the mix.
    /// </summary>
    public void Start(EBassSwapRole role, TimeSpan mixDuration, TimeSpan swapAt)
    {
        lock (_lock)
        {
            _role = role;
            _swapAtSeconds = swapAt.TotalSeconds;
            _framesTotal = Math.Max(1, (long)(mixDuration.TotalSeconds * WaveFormat.SampleRate));
            _framesDone = 0;
            _active = true;
            _stateDirty = true;
        }
    }

    /// <summary>Cancels any bass swap: the signal passes through untouched again.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _active = false;
            _stateDirty = true;
        }
    }

    public int Read(Span<float> buffer)
    {
        int read = _source.Read(buffer);
        EBassSwapRole role;
        long framesTotal;
        long framesDone;
        double swapAtSeconds;
        bool clearState;
        int channels = WaveFormat.Channels;
        int frames = read / channels;

        lock (_lock)
        {
            clearState = _stateDirty;
            _stateDirty = false;

            if (!_active)
            {
                if (clearState)
                    ClearState();

                return read;
            }

            role = _role;
            framesTotal = _framesTotal;
            framesDone = _framesDone;
            swapAtSeconds = _swapAtSeconds;
            _framesDone = Math.Min(framesTotal, framesDone + frames);
        }

        if (clearState)
            ClearState();

        double sampleRate = WaveFormat.SampleRate;
        double mixSeconds = framesTotal / sampleRate;

        for (int start = 0; start < frames; start += BlockFrames)
        {
            int count = Math.Min(BlockFrames, frames - start);
            double elapsed = (framesDone + start + (count / 2.0)) / sampleRate;
            double cut = BassSwapCurve.Cut(role, elapsed, mixSeconds, swapAtSeconds);

            if (cut <= 0)
            {
                ClearState();
                _appliedCut = 0;

                continue;
            }

            if (cut != _appliedCut)
            {
                UpdateCoefficients(cut);
                _appliedCut = cut;
            }

            ProcessBlock(buffer, start * channels, count, channels);
        }

        return read;
    }

    private void ClearState()
    {
        Array.Clear(_z1);
        Array.Clear(_z2);
    }

    private void ProcessBlock(Span<float> buffer, int offset, int frames, int channels)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                int index = offset + (frame * channels) + channel;
                double x = buffer[index];
                double y = (_b0 * x) + _z1[channel];

                _z1[channel] = (_b1 * x) - (_a1 * y) + _z2[channel];
                _z2[channel] = (_b2 * x) - (_a2 * y);
                buffer[index] = (float)y;
            }
        }
    }

    /// <summary>RBJ low shelf (slope 1) at <see cref="BassSwapCurve.CutoffHz"/>, gain <paramref name="cut"/> × <see cref="BassSwapCurve.AttenuationDb"/>.</summary>
    private void UpdateCoefficients(double cut)
    {
        ComputeLowShelf(WaveFormat.SampleRate, BassSwapCurve.CutoffHz, cut * BassSwapCurve.AttenuationDb, out _b0, out _b1, out _b2, out _a1, out _a2);
    }

    /// <summary>Computes the normalised coefficients (a0 = 1) of an RBJ low shelf with a slope of 1.</summary>
    internal static void ComputeLowShelf(double sampleRate, double cutoffHz, double gainDb, out double b0, out double b1, out double b2, out double a1, out double a2)
    {
        double a = Math.Pow(10, gainDb / 40);
        double w0 = 2 * Math.PI * cutoffHz / sampleRate;
        double cos = Math.Cos(w0);
        double alpha = Math.Sin(w0) / 2 * Math.Sqrt(2);
        double twoSqrtAAlpha = 2 * Math.Sqrt(a) * alpha;

        double a0 = (a + 1) + ((a - 1) * cos) + twoSqrtAAlpha;

        b0 = a * ((a + 1) - ((a - 1) * cos) + twoSqrtAAlpha) / a0;
        b1 = 2 * a * ((a - 1) - ((a + 1) * cos)) / a0;
        b2 = a * ((a + 1) - ((a - 1) * cos) - twoSqrtAAlpha) / a0;
        a1 = -2 * ((a - 1) + ((a + 1) * cos)) / a0;
        a2 = ((a + 1) + ((a - 1) * cos) - twoSqrtAAlpha) / a0;
    }
}