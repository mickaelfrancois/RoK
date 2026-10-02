namespace Rok.Application.Player.Mix.Tempo;

/// <summary>
/// Builds a low-rate mono signal incrementally from decoded interleaved blocks: channels are averaged,
/// then each group of <see cref="Factor"/> frames is averaged (box filter) into one output sample.
/// </summary>
public sealed class MonoDecimator
{
    private readonly List<float> _samples = [];
    private double _sum;
    private int _framesInGroup;

    /// <summary>Initializes a new instance of the <see cref="MonoDecimator"/> class.</summary>
    /// <param name="sampleRate">Sample rate of the decoded audio, in Hz.</param>
    public MonoDecimator(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);

        Factor = ComputeFactor(sampleRate);
        OutputSampleRate = sampleRate / Factor;
    }

    /// <summary>Integer decimation factor, never below 1.</summary>
    public int Factor { get; }

    /// <summary>Sample rate of the decimated signal, in Hz.</summary>
    public int OutputSampleRate { get; }

    /// <summary>Number of output samples produced so far.</summary>
    public int SampleCount => _samples.Count;

    /// <summary>Computes the decimation factor that brings a rate closest to <see cref="MixThresholds.TargetMonoRate"/>.</summary>
    /// <param name="sampleRate">Source sample rate, in Hz.</param>
    public static int ComputeFactor(int sampleRate) =>
        Math.Max(1, (int)Math.Round((double)sampleRate / MixThresholds.TargetMonoRate));

    /// <summary>Adds a block of interleaved samples.</summary>
    /// <param name="interleaved">Interleaved samples, a whole number of frames.</param>
    /// <param name="channels">Number of channels of the block.</param>
    public void Add(ReadOnlySpan<float> interleaved, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channels, 0);

        var frames = interleaved.Length / channels;

        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * channels;
            double mono = 0;

            for (var channel = 0; channel < channels; channel++)
                mono += interleaved[offset + channel];

            _sum += mono / channels;
            _framesInGroup++;

            if (_framesInGroup != Factor)
                continue;

            _samples.Add((float)(_sum / Factor));
            _sum = 0;
            _framesInGroup = 0;
        }
    }

    /// <summary>Returns the decimated samples accumulated so far; an incomplete trailing group is dropped.</summary>
    public float[] Build() => _samples.ToArray();
}