namespace Rok.Application.Player.Mix;

/// <summary>
/// Builds an <see cref="RmsEnvelope"/> incrementally from decoded blocks, so the samples never have to be kept in memory.
/// Channels are averaged into a mono signal; incomplete trailing windows are dropped.
/// </summary>
public sealed class RmsEnvelopeAccumulator
{
    private readonly int _framesPerWindow;
    private readonly double _windowSeconds;
    private readonly List<float> _levels = [];
    private double _sumSquares;
    private int _framesInWindow;

    /// <summary>Initializes a new instance of the <see cref="RmsEnvelopeAccumulator"/> class.</summary>
    /// <param name="sampleRate">Sample rate of the decoded audio, in Hz.</param>
    /// <param name="windowSeconds">Duration of one RMS window.</param>
    public RmsEnvelopeAccumulator(int sampleRate, double windowSeconds = MixThresholds.RmsWindowSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(windowSeconds, 0);

        _windowSeconds = windowSeconds;
        _framesPerWindow = Math.Max(1, (int)Math.Round(sampleRate * windowSeconds));
    }

    /// <summary>Number of complete windows accumulated so far.</summary>
    public int WindowCount => _levels.Count;

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
            double sum = 0;

            for (var channel = 0; channel < channels; channel++)
                sum += interleaved[offset + channel];

            var mono = sum / channels;
            _sumSquares += mono * mono;
            _framesInWindow++;

            if (_framesInWindow == _framesPerWindow)
                CloseWindow();
        }
    }

    /// <summary>Builds the envelope of the windows accumulated so far.</summary>
    /// <param name="startSeconds">Absolute position in the track of the first accumulated sample.</param>
    /// <param name="trackLengthSeconds">Total length of the track.</param>
    public RmsEnvelope Build(double startSeconds, double trackLengthSeconds) =>
        new(startSeconds, _windowSeconds, _levels.ToArray(), trackLengthSeconds);

    private void CloseWindow()
    {
        var meanSquare = _sumSquares / _framesPerWindow;
        var level = meanSquare > 0 ? 10 * Math.Log10(meanSquare) : MixThresholds.SilentLevelDb;
        _levels.Add((float)Math.Max(level, MixThresholds.SilentLevelDb));
        _sumSquares = 0;
        _framesInWindow = 0;
    }
}