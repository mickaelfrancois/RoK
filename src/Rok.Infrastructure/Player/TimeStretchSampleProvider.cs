using NAudio.Wave;
using Rok.Application.Player.Mix;
using SoundTouch;

namespace Rok.Infrastructure.Player;

/// <summary>
/// Tempo stretch of the incoming track of a Mix crossfade, applied on the rendering thread with SoundTouch (WSOLA, no pitch change).
/// The tempo follows <see cref="TempoStretchCurve"/> counted in rendered frames; once the return is complete, the stream is spliced
/// back onto the raw samples kept in a ring so that the rendering is bit-exact again. Passes the source through untouched until
/// <see cref="Start"/> is called. SoundTouch is configured with the quick seek and without the anti-alias filter (the only
/// settings in which it does not allocate in steady state); every buffer is allocated in <see cref="Start"/>.
/// </summary>
internal sealed class TimeStretchSampleProvider : ISampleProvider
{
    /// <summary>Highest sample rate accepted by SoundTouch.</summary>
    internal const int MaxSampleRate = 192000;

    /// <summary>Highest channel count accepted by SoundTouch.</summary>
    internal const int MaxChannels = 16;

    private const int ChunkFrames = 2048;
    private const int TempoBlockFrames = 256;
    private const int WarmUpSeconds = 2;
    private const double SpliceSeconds = 0.010;
    private const double SpliceSearchSeconds = 0.025;
    private const int CoarseSearchStep = 4;

    private readonly ISampleProvider _source;
    private readonly Lock _lock = new();
    private readonly float[] _scratch;
    private StretchState? _state;
    private long _pendingSkipFrames;
    private bool _spliceRequested;

    public TimeStretchSampleProvider(ISampleProvider source)
    {
        _source = source;
        _scratch = Supports(source.WaveFormat) ? new float[ChunkFrames * source.WaveFormat.Channels] : [];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Tells whether SoundTouch can process <paramref name="format"/>.</summary>
    public static bool Supports(WaveFormat format) =>
        format.SampleRate > 0
        && format.SampleRate <= MaxSampleRate
        && format.Channels is >= 1 and <= MaxChannels;

    /// <summary>Whether a stretch, its return or a pending splice is running (read under lock, for tests and diagnostics).</summary>
    public bool IsActive
    {
        get
        {
            lock (_lock)
                return _state is not null;
        }
    }

    /// <summary>Discards <paramref name="duration"/> of source content at the next read, stretched or not.</summary>
    public void SkipSource(TimeSpan duration)
    {
        long frames = (long)Math.Round(duration.TotalSeconds * WaveFormat.SampleRate);

        if (frames <= 0 || !Supports(WaveFormat))
            return;

        lock (_lock)
            _pendingSkipFrames += frames;
    }

    /// <summary>
    /// Starts stretching at <paramref name="ratio"/> for <paramref name="overlap"/>, then returns linearly to the original tempo
    /// over <paramref name="returnDuration"/>. Must be called from the control thread before the rendering starts.
    /// </summary>
    public void Start(double ratio, TimeSpan overlap, TimeSpan returnDuration)
    {
        if (!Supports(WaveFormat))
            throw new NotSupportedException("The wave format is not supported by the time stretch.");

        double clamped = TempoStretchCurve.ClampRatio(ratio);
        int channels = WaveFormat.Channels;
        int sampleRate = WaveFormat.SampleRate;
        SoundTouchProcessor processor = new()
        {
            Channels = channels,
            SampleRate = sampleRate,
        };

        processor.SetSetting(SettingId.UseQuickSeek, 1);
        processor.SetSetting(SettingId.UseAntiAliasFilter, 0);

        StretchState state = new(processor, channels, sampleRate, clamped, overlap.TotalSeconds, returnDuration.TotalSeconds);

        WarmUp(processor, 1 - MixThresholds.MaxTempoStretch, channels);
        WarmUp(processor, 1, channels);
        WarmUp(processor, 1 + MixThresholds.MaxTempoStretch, channels);
        processor.Clear();
        processor.Tempo = clamped;

        lock (_lock)
        {
            _state = state;
            _spliceRequested = false;
        }
    }

    /// <summary>Splices the stream back onto the raw samples at the next read (pause): the rendering is bit-exact again right after.</summary>
    public void ReturnToOriginalTempo()
    {
        lock (_lock)
        {
            if (_state is not null)
                _spliceRequested = true;
        }
    }

    /// <summary>Abandons any stretch at once: the next read passes the source through untouched.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = null;
            _spliceRequested = false;
        }
    }

    public int Read(Span<float> buffer)
    {
        StretchState? state;
        long skip;
        bool splice;

        lock (_lock)
        {
            state = _state;
            skip = _pendingSkipFrames;
            splice = _spliceRequested;
            _pendingSkipFrames = 0;
            _spliceRequested = false;
        }

        if (skip > 0 && !DiscardSource(skip))
            return 0;

        if (state is null)
            return _source.Read(buffer);

        int channels = WaveFormat.Channels;
        int frames = buffer.Length / channels;
        int written = 0;

        try
        {
            ReadStretched(state, buffer, frames, ref written, splice);
        }
        catch (Exception)
        {
            Release(state);
        }

        lock (_lock)
        {
            if (_state is not null)
                return written * channels;
        }

        if (written < frames)
            written += _source.Read(buffer.Slice(written * channels, (frames - written) * channels)) / channels;

        return written * channels;
    }

    private bool DiscardSource(long frames)
    {
        int channels = WaveFormat.Channels;

        while (frames > 0)
        {
            int wanted = (int)Math.Min(frames, ChunkFrames);
            int read = _source.Read(_scratch.AsSpan(0, wanted * channels)) / channels;

            if (read == 0)
                return false;

            frames -= read;
        }

        return true;
    }

    private void ReadStretched(StretchState state, Span<float> buffer, int frames, ref int written, bool spliceRequested)
    {
        int channels = state.Channels;

        if (spliceRequested && state.Phase == EStretchPhase.Stretching)
            BeginSplice(state);

        while (written < frames)
        {
            if (state.Phase == EStretchPhase.Replay)
            {
                written += EmitReplay(state, buffer.Slice(written * channels, (frames - written) * channels));

                if (state.ReplayDone)
                {
                    Release(state);

                    return;
                }

                continue;
            }

            double elapsed = state.EmittedFrames / (double)state.SampleRate;

            if (TempoStretchCurve.IsComplete(state.OverlapSeconds, state.ReturnSeconds, elapsed))
            {
                BeginSplice(state);

                continue;
            }

            int block = Math.Min(TempoBlockFrames, frames - written);
            double tempo = TempoStretchCurve.TempoAt(state.Ratio, state.OverlapSeconds, state.ReturnSeconds, elapsed + (block / 2.0 / state.SampleRate));

            if (tempo != state.CurrentTempo)
            {
                state.Processor.Tempo = tempo;
                state.CurrentTempo = tempo;
            }

            int got = Pull(state, buffer.Slice(written * channels, block * channels), block);

            state.EmittedFrames += got;
            written += got;

            if (got < block)
            {
                Release(state);

                return;
            }
        }
    }

    private int Pull(StretchState state, Span<float> destination, int frames)
    {
        SoundTouchProcessor processor = state.Processor;
        int channels = state.Channels;
        int delivered = 0;

        while (delivered < frames)
        {
            if (!EnsureAvailable(state))
                break;

            int count = Math.Min(frames - delivered, processor.AvailableSamples);
            Span<float> target = destination.Slice(delivered * channels);

            delivered += processor.ReceiveSamples(target, count);
        }

        return delivered;
    }

    private bool EnsureAvailable(StretchState state)
    {
        while (state.Processor.AvailableSamples < 1)
        {
            if (state.SourceEnded)
                return false;

            FeedChunk(state);
        }

        return true;
    }

    private void FeedChunk(StretchState state)
    {
        int channels = state.Channels;
        int read = _source.Read(_scratch.AsSpan(0, ChunkFrames * channels)) / channels;

        if (read == 0)
        {
            state.SourceEnded = true;
            state.Processor.Flush();

            return;
        }

        state.Store(_scratch, read);
        ReadOnlySpan<float> samples = _scratch;

        state.Processor.PutSamples(samples, read);
    }

    /// <summary>Pulls a few frames of the stretched stream and cross-fades them onto the best matching raw samples of the ring.</summary>
    private void BeginSplice(StretchState state)
    {
        int channels = state.Channels;
        int length = state.SpliceFrames;
        int got = Pull(state, state.Splice.AsSpan(0, length * channels), length);

        state.Phase = EStretchPhase.Replay;
        state.SpliceIndex = 0;
        state.SpliceCount = got;
        state.ReplayPosition = state.TotalRead;
        state.ReplayEnd = state.TotalRead;

        if (got < length)
            return;

        SoundTouchProcessor processor = state.Processor;
        long contentEnd = state.TotalRead - processor.UnprocessedSampleCount - (long)(processor.AvailableSamples * state.CurrentTempo);
        long estimate = contentEnd - length;
        int search = (int)(SpliceSearchSeconds * state.SampleRate);
        long lowest = Math.Max(0, state.TotalRead - state.RingFrames);
        long highest = state.TotalRead - length;

        if (highest < lowest)
            return;

        long best = FindBestPosition(state, Math.Clamp(estimate - search, lowest, highest), Math.Clamp(estimate + search, lowest, highest), length);

        for (int frame = 0; frame < length; frame++)
        {
            float weight = frame / (float)length;

            for (int channel = 0; channel < channels; channel++)
            {
                int index = (frame * channels) + channel;
                float raw = state.Ring[(int)((best + frame) % state.RingFrames) * channels + channel];

                state.Splice[index] = (state.Splice[index] * (1 - weight)) + (raw * weight);
            }
        }

        state.ReplayPosition = best + length;
    }

    private static long FindBestPosition(StretchState state, long from, long to, int length)
    {
        long best = from;
        double bestScore = double.NegativeInfinity;

        for (long position = from; position <= to; position += CoarseSearchStep)
        {
            double score = Score(state, position, length);

            if (score > bestScore)
            {
                bestScore = score;
                best = position;
            }
        }

        long refineFrom = Math.Max(from, best - CoarseSearchStep + 1);
        long refineTo = Math.Min(to, best + CoarseSearchStep - 1);

        for (long position = refineFrom; position <= refineTo; position++)
        {
            double score = Score(state, position, length);

            if (score > bestScore)
            {
                bestScore = score;
                best = position;
            }
        }

        return best;
    }

    /// <summary>Normalised correlation between the stretched frames and the ring starting at <paramref name="position"/>.</summary>
    private static double Score(StretchState state, long position, int length)
    {
        int channels = state.Channels;
        double dot = 0;
        double energy = 0;

        for (int frame = 0; frame < length; frame++)
        {
            int ringIndex = (int)((position + frame) % state.RingFrames) * channels;

            for (int channel = 0; channel < channels; channel++)
            {
                double raw = state.Ring[ringIndex + channel];

                dot += state.Splice[(frame * channels) + channel] * raw;
                energy += raw * raw;
            }
        }

        return dot / Math.Sqrt(energy + 1e-12);
    }

    private static int EmitReplay(StretchState state, Span<float> destination)
    {
        int channels = state.Channels;
        int capacity = destination.Length / channels;
        int emitted = 0;

        while (emitted < capacity && state.SpliceIndex < state.SpliceCount)
        {
            int count = Math.Min(capacity - emitted, state.SpliceCount - state.SpliceIndex);

            state.Splice.AsSpan(state.SpliceIndex * channels, count * channels).CopyTo(destination.Slice(emitted * channels));
            state.SpliceIndex += count;
            emitted += count;
        }

        while (emitted < capacity && state.ReplayPosition < state.ReplayEnd)
        {
            int ringIndex = (int)(state.ReplayPosition % state.RingFrames);
            int count = (int)Math.Min(capacity - emitted, Math.Min(state.ReplayEnd - state.ReplayPosition, state.RingFrames - ringIndex));

            state.Ring.AsSpan(ringIndex * channels, count * channels).CopyTo(destination.Slice(emitted * channels));
            state.ReplayPosition += count;
            emitted += count;
        }

        return emitted;
    }

    private void Release(StretchState state)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_state, state))
                _state = null;
        }
    }

    /// <summary>
    /// Runs the processor with noise at <paramref name="tempo"/>, fed and drained like <see cref="Pull"/> does, so that SoundTouch's
    /// FIFO buffers reach their steady-state capacity here rather than on the rendering thread.
    /// </summary>
    private void WarmUp(SoundTouchProcessor processor, double tempo, int channels)
    {
        processor.Tempo = tempo;

        uint seed = 2463534242;
        int chunks = Math.Max(1, WarmUpFrames(processor) / ChunkFrames);
        Span<float> output = _scratch;

        for (int chunk = 0; chunk < chunks; chunk++)
        {
            for (int index = 0; index < ChunkFrames * channels; index++)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                _scratch[index] = ((seed >> 8) / 16777216f) - 0.5f;
            }

            ReadOnlySpan<float> samples = _scratch;

            processor.PutSamples(samples, ChunkFrames);
            Drain(processor, output);
        }

        processor.Flush();
        Drain(processor, output);
    }

    private static int WarmUpFrames(SoundTouchProcessor processor) => processor.SampleRate * WarmUpSeconds;

    private static void Drain(SoundTouchProcessor processor, Span<float> output)
    {
        while (processor.AvailableSamples > 0)
            processor.ReceiveSamples(output, Math.Min(processor.AvailableSamples, TempoBlockFrames));
    }

    private enum EStretchPhase
    {
        Stretching,
        Replay,
    }

    private sealed class StretchState
    {
        public StretchState(SoundTouchProcessor processor, int channels, int sampleRate, double ratio, double overlapSeconds, double returnSeconds)
        {
            Processor = processor;
            Channels = channels;
            SampleRate = sampleRate;
            Ratio = ratio;
            OverlapSeconds = overlapSeconds;
            ReturnSeconds = returnSeconds;
            CurrentTempo = ratio;
            RingFrames = sampleRate;
            Ring = new float[RingFrames * channels];
            SpliceFrames = Math.Max(1, (int)(SpliceSeconds * sampleRate));
            Splice = new float[SpliceFrames * channels];
        }

        public SoundTouchProcessor Processor { get; }

        public int Channels { get; }

        public int SampleRate { get; }

        public double Ratio { get; }

        public double OverlapSeconds { get; }

        public double ReturnSeconds { get; }

        public double CurrentTempo { get; set; }

        public bool SourceEnded { get; set; }

        public EStretchPhase Phase { get; set; }

        public float[] Ring { get; }

        public int RingFrames { get; }

        public long TotalRead { get; private set; }

        public long EmittedFrames { get; set; }

        public float[] Splice { get; }

        public int SpliceFrames { get; }

        public int SpliceIndex { get; set; }

        public int SpliceCount { get; set; }

        public long ReplayPosition { get; set; }

        public long ReplayEnd { get; set; }

        public bool ReplayDone => SpliceIndex >= SpliceCount && ReplayPosition >= ReplayEnd;

        public void Store(float[] samples, int frames)
        {
            int done = 0;

            while (done < frames)
            {
                int ringIndex = (int)((TotalRead + done) % RingFrames);
                int count = Math.Min(frames - done, RingFrames - ringIndex);

                Array.Copy(samples, done * Channels, Ring, ringIndex * Channels, count * Channels);
                done += count;
            }

            TotalRead += frames;
        }
    }
}