namespace Rok.Application.Player.Mix.Tempo;

/// <summary>
/// Onset strength of a mono signal, one value per analysis frame (spectral flux of the log magnitude,
/// minus a sliding mean, positive part only).
/// </summary>
/// <param name="Values">Onset strength of each frame, never negative.</param>
/// <param name="HopSeconds">Time between two consecutive frames.</param>
/// <param name="FirstFrameCenterSeconds">Time of the centre of the first frame, relative to the first sample.</param>
public sealed record OnsetCurve(float[] Values, double HopSeconds, double FirstFrameCenterSeconds)
{
    private const float LogGain = 100f;

    /// <summary>Time of the centre of a frame, relative to the first sample.</summary>
    /// <param name="frame">Frame index.</param>
    public double TimeOf(int frame) => FirstFrameCenterSeconds + (frame * HopSeconds);

    /// <summary>Computes the onset curve of a mono signal.</summary>
    /// <param name="mono">Mono samples.</param>
    /// <param name="sampleRate">Sample rate of <paramref name="mono"/>, in Hz.</param>
    public static OnsetCurve Compute(ReadOnlySpan<float> mono, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);

        var frameSize = MixThresholds.OnsetFrameSize;
        var hop = MixThresholds.OnsetHop;
        var hopSeconds = (double)hop / sampleRate;
        var centerSeconds = (frameSize / 2.0) / sampleRate;

        if (mono.Length < frameSize)
            return new OnsetCurve([], hopSeconds, centerSeconds);

        var frames = ((mono.Length - frameSize) / hop) + 1;
        var flux = new float[frames];
        var fft = new Fft(frameSize);
        var window = new float[frameSize];
        var real = new float[frameSize];
        var imaginary = new float[frameSize];
        var bins = (frameSize / 2) + 1;
        var previous = new float[bins];
        var current = new float[bins];

        for (var i = 0; i < frameSize; i++)
            window[i] = (float)(0.5 - (0.5 * Math.Cos(2 * Math.PI * i / frameSize)));

        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * hop;

            for (var i = 0; i < frameSize; i++)
            {
                real[i] = mono[offset + i] * window[i];
                imaginary[i] = 0;
            }

            fft.Transform(real, imaginary);

            float sum = 0;

            for (var bin = 0; bin < bins; bin++)
            {
                var magnitude = MathF.Sqrt((real[bin] * real[bin]) + (imaginary[bin] * imaginary[bin]));
                current[bin] = MathF.Log(1 + (LogGain * magnitude));

                if (frame > 0)
                    sum += MathF.Max(0, current[bin] - previous[bin]);
            }

            flux[frame] = sum;
            (previous, current) = (current, previous);
        }

        RemoveSlidingMean(flux, Math.Max(1, (int)Math.Round(MixThresholds.OnsetMeanSeconds / hopSeconds)));

        return new OnsetCurve(flux, hopSeconds, centerSeconds);
    }

    private static void RemoveSlidingMean(float[] values, int windowFrames)
    {
        var prefix = new double[values.Length + 1];

        for (var i = 0; i < values.Length; i++)
            prefix[i + 1] = prefix[i] + values[i];

        var half = windowFrames / 2;

        for (var i = 0; i < values.Length; i++)
        {
            var from = Math.Max(0, i - half);
            var to = Math.Min(values.Length, i + half + 1);
            var mean = (prefix[to] - prefix[from]) / (to - from);

            values[i] = (float)Math.Max(0, values[i] - mean);
        }
    }
}