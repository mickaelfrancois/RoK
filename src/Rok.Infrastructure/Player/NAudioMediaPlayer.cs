using System.Diagnostics;
using System.Timers;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Player.Mix;
using Rok.Application.Player.Output;
using Rok.Infrastructure.Player.Output;
using Rok.Infrastructure.Player.Streaming;

namespace Rok.Infrastructure.Player;

public class NAudioMediaPlayer : IPlayerEngine, IDisposable
{
    public event EventHandler? OnMediaChanged;
    public event EventHandler? OnMediaEnded;
    public event EventHandler? OnMediaStateChanged;
    public event EventHandler? OnMediaAboutToEnd;
    public event EventHandler? OnTransitionCue;
    public event EventHandler<string>? OnMetadataChanged;
    public event EventHandler<GaplessTransitionEventArgs>? OnGaplessTransition;
    public event EventHandler<OutputLostEventArgs>? OnOutputLost;
    public event EventHandler<AudioOutputState>? OnOutputStateChanged;

    private const int UnknownDepth = -1;

    public bool IsLive => _isLive;

    public bool IsBuffering => _streaming?.IsBuffering ?? false;

    private AudioOutputHandle? _output;
    private PlaybackPipeline? _pipeline;
    private PendingNext? _pendingNext;
    private PlaybackPipeline? _crossfadeIncoming;
    private int _generation;
    private float _volume = 1f;
    private readonly float[] _bandGains = new float[PlaybackPipeline.BandFrequencies.Length];
    private readonly Lock _stateLock = new();
    private readonly Lock _outputLock = new();
    private readonly TransitionCueGate _cueGate = new();
    private readonly System.Timers.Timer _positionTimer;
    private readonly ILogger<NAudioMediaPlayer> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAudioOutputFactory _outputFactory;
    private readonly IAudioFormatProbe _formatProbe;

    private AudioOutputTarget _target = AudioOutputTarget.Default;
    private volatile bool _isPlaying;
    private string? _currentFile;
    private int _nativeBits = UnknownDepth;
    private bool _outputChangePending;
    private bool _lastNeutral = true;

    private StreamingPlayback? _streaming;
    private bool _isLive;

    private const int FadePollIntervalMs = 50;
    private static readonly TimeSpan FadeCompletionGrace = TimeSpan.FromSeconds(1);
    private readonly int _aboutToEndDelay = 20;

    private double _length;
    private bool _aboutToEndRaised;
    private bool disposedValue;

    public double Position => _pipeline?.Reader.CurrentTime.TotalSeconds ?? 0;

    public double Length
    {
        get
        {
            if (_length <= 0)
                _length = 1;

            return _length;
        }
        set => _length = value;
    }

    /// <inheritdoc />
    public AudioOutputState OutputState
    {
        get
        {
            AudioOutputHandle? output = _isLive ? _streaming?.Output : _output;
            bool isNeutral = IsProcessingNeutral;

            if (output is null)
                return AudioOutputState.Closed with { IsProcessingNeutral = isNeutral };

            return new AudioOutputState(true, output.DeviceId, output.IsOnPreferred, output.ActualMode, output.FallbackReason, isNeutral);
        }
    }

    private bool IsProcessingNeutral => _volume == 1f && (_pipeline?.Source.Gain ?? 1f) == 1f && Array.TrueForAll(_bandGains, gain => gain == 0f);

    public NAudioMediaPlayer(ILogger<NAudioMediaPlayer> logger, IHttpClientFactory httpClientFactory, IAudioOutputFactory outputFactory, IAudioFormatProbe formatProbe)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _outputFactory = outputFactory;
        _formatProbe = formatProbe;

        _positionTimer = new System.Timers.Timer(250)
        {
            AutoReset = true,
            Enabled = false
        };

        _positionTimer.Elapsed += PositionTimer_Elapsed;
    }

    public void Pause()
    {
        _isPlaying = false;

        if (_isLive)
        {
            _streaming?.Pause();
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        AudioOutputHandle? output = _output;

        if (output is not null && output.Player.PlaybackState == PlaybackState.Playing)
            output.Player.Pause();

        _positionTimer.Stop();
        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    /// <remarks>Opens the output first when it was released; throws when no output device is available.</remarks>
    public void Play()
    {
        _isPlaying = true;

        if (_isLive)
        {
            RunOrResetPlaying(() => _streaming?.Resume());
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_pipeline is null)
            return;

        _cueGate.Rearm();

        RunOrResetPlaying(() =>
        {
            AudioOutputHandle output = EnsureOutput();

            if (output.Player.PlaybackState != PlaybackState.Playing)
                output.Player.Play();
        });

        if (!_positionTimer.Enabled)
            _positionTimer.Start();

        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RunOrResetPlaying(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            _isPlaying = false;
            throw;
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _cueGate.Clear();

        if (_isLive)
        {
            _streaming?.Stop();
            _streaming?.Dispose();
            _streaming = null;
            _isLive = false;
            _length = 0;
            _aboutToEndRaised = false;
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
            RaiseOutputStateChanged();
            return;
        }

        _positionTimer.Stop();

        AudioOutputHandle? output;
        PlaybackPipeline? pipeline;

        lock (_outputLock)
        {
            lock (_stateLock)
            {
                output = _output;
                pipeline = _pipeline;
                _output = null;
                _pipeline = null;
                _pendingNext = null;
                _generation++;
            }

            ReleaseOutput(output);
        }

        ReleasePipeline(pipeline);

        Length = 0;
        _aboutToEndRaised = false;

        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);

        if (output is not null)
            RaiseOutputStateChanged();
    }

    public void SetPosition(double position)
    {
        if (_isLive) return;

        PlaybackPipeline? pipeline = _pipeline;

        if (pipeline is null)
            return;

        if (position < 0)
            position = 0;

        pipeline.Reader.CurrentTime = TimeSpan.FromSeconds(position);
        _cueGate.Rearm();

        if (Position < Math.Max(0, Length - _aboutToEndDelay))
            _aboutToEndRaised = false;
    }

    public void SetVolume(double volume)
    {
        if (_isLive)
        {
            _streaming?.SetVolume(volume);
            return;
        }

        _volume = ToLinearVolume(volume);

        PlaybackPipeline? incoming;

        lock (_stateLock)
        {
            incoming = _crossfadeIncoming;
        }

        if (_pipeline is not null)
            _pipeline.Volume.Volume = _volume;

        if (incoming is not null)
            incoming.Volume.Volume = _volume;

        NotifyIfNeutralityChanged();
    }

    public bool SetTrack(TrackDto track, float replayGain)
    {
        AudioFileReader? reader = null;

        try
        {
            Stop();

            reader = new AudioFileReader(track.MusicFile);
            PlaybackPipeline pipeline = PlaybackPipeline.Create(reader, replayGain, track.Id, _bandGains, _volume);

            Length = reader.TotalTime.TotalSeconds > 0
                ? reader.TotalTime.TotalSeconds
                : 1;

            pipeline.Chain.SourceSwitched += Chain_SourceSwitched;

            lock (_stateLock)
            {
                _pipeline = pipeline;
                _currentFile = track.MusicFile;
                _nativeBits = UnknownDepth;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open {File}", track.MusicFile);
            reader?.Dispose();

            lock (_stateLock)
            {
                _pipeline = null;
            }

            return false;
        }

        TryOpenOutput();

        _aboutToEndRaised = false;

        OnMediaChanged?.Invoke(this, EventArgs.Empty);
        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
        NotifyIfNeutralityChanged();

        return true;
    }

    /// <summary>Opens the output right after loading a track; a failure is retried, and reported, by <see cref="Play"/>.</summary>
    private void TryOpenOutput()
    {
        try
        {
            EnsureOutput();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to open the audio output, it will be retried on play");
        }
    }

    public bool SetStream(RadioStationDto station)
    {
        Stop();

        _streaming = new StreamingPlayback(_httpClientFactory.CreateClient("RadioStream"), _logger, OpenRadioOutput);
        _streaming.MetadataChanged += (_, title) => OnMetadataChanged?.Invoke(this, title);
        _streaming.PlaybackEnded += (_, _) => OnMediaEnded?.Invoke(this, EventArgs.Empty);
        _streaming.BufferingChanged += (_, _) => OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
        _streaming.OutputLost += Streaming_OutputLost;

        _isLive = true;
        _isPlaying = true;
        _length = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                await _streaming.StartAsync(station, CancellationToken.None);
                OnMediaChanged?.Invoke(this, EventArgs.Empty);
                OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
                RaiseOutputStateChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start stream {Url}", station.StreamUrl);
                _streaming?.Dispose();
                _streaming = null;
                _isLive = false;
                OnMediaEnded?.Invoke(this, EventArgs.Empty);
            }
        });

        return true;
    }

    /// <summary>The radio always plays shared, on the device chosen for the music.</summary>
    private AudioOutputHandle OpenRadioOutput(ISampleProvider source) =>
        _outputFactory.Open(_target with { Mode = EAudioOutputMode.Shared }, source, 0);

    private void Streaming_OutputLost(object? sender, AudioOutputHandle lost)
    {
        if (!ReferenceEquals(sender, _streaming))
            return;

        OnOutputLost?.Invoke(this, new OutputLostEventArgs(_isPlaying, lost.IsOnPreferred));
        RaiseOutputStateChanged();
    }

    public void SetEqualizerBand(int bandIndex, float gain)
    {
        if (bandIndex >= 0 && bandIndex < _bandGains.Length)
            _bandGains[bandIndex] = gain;

        _pipeline?.Equalizer.UpdateBand(bandIndex, gain);

        NotifyIfNeutralityChanged();
    }

    /// <inheritdoc />
    /// <remarks>
    /// When exclusive is requested, the next track is refused while the output runs a shared fallback (so the next
    /// track retries exclusive) and when its format cannot follow on the negotiated one.
    /// </remarks>
    public bool QueueNextTrack(TrackDto nextTrack, float replayGain)
    {
        if (_isLive || _pipeline is null)
            return false;

        bool exclusiveRequested = _target.Mode == EAudioOutputMode.Exclusive;
        AudioOutputHandle? output = _output;

        if (exclusiveRequested && (output is null || output.FallbackReason is not null))
        {
            _logger.LogInformation("Gapless: the exclusive output is not open, {File} will reopen it", nextTrack.MusicFile);
            return false;
        }

        AudioFileReader reader;

        try
        {
            reader = new AudioFileReader(nextTrack.MusicFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gapless: failed to open next track {File}", nextTrack.MusicFile);
            return false;
        }

        int nextBits = UnknownDepth;

        if (exclusiveRequested && output?.ExclusiveFormat is WaveFormat negotiated)
        {
            nextBits = _formatProbe.GetBitsPerSample(nextTrack.MusicFile);

            if (!ExclusiveFormatLadder.CanChain(negotiated, reader.WaveFormat.SampleRate, reader.WaveFormat.Channels, nextBits))
            {
                _logger.LogInformation("Gapless: {File} does not fit the exclusive format {Format}, the output will reopen", nextTrack.MusicFile, negotiated);
                reader.Dispose();
                return false;
            }
        }

        ReplayGainSampleProvider source = new(reader, replayGain);
        bool queued = false;
        ISampleProvider? replaced = null;

        lock (_stateLock)
        {
            if (_pipeline is not null && _pipeline.Chain.TryQueueNext(source, out replaced))
            {
                _pendingNext = new PendingNext(source, nextTrack, nextBits);
                queued = true;
            }
        }

        (replaced as IDisposable)?.Dispose();

        if (!queued)
        {
            _logger.LogInformation("Gapless: {File} has a different format, falling back to a regular transition", nextTrack.MusicFile);
            source.Dispose();
        }

        return queued;
    }

    /// <inheritdoc />
    public void UpdateReplayGain(long trackId, float replayGain)
    {
        lock (_stateLock)
        {
            if (_pipeline is not null && _pipeline.TrackId == trackId)
                _pipeline.Source.Gain = replayGain;

            if (_pendingNext is not null && _pendingNext.Track.Id == trackId)
                _pendingNext.Source.Gain = replayGain;

            if (_crossfadeIncoming is not null && _crossfadeIncoming.TrackId == trackId)
                _crossfadeIncoming.Source.Gain = replayGain;
        }

        NotifyIfNeutralityChanged();
    }

    /// <inheritdoc />
    public void ClearNextTrack()
    {
        ISampleProvider? removed;

        lock (_stateLock)
        {
            removed = _pipeline?.Chain.ClearNext();
            _pendingNext = null;
            _aboutToEndRaised = false;
        }

        (removed as IDisposable)?.Dispose();
    }

    /// <inheritdoc />
    public void SetOutputTarget(AudioOutputTarget target)
    {
        _target = target;
        ReopenOutput();
    }

    /// <inheritdoc />
    public void ReopenOutput()
    {
        if (_isLive)
        {
            try
            {
                _streaming?.ReopenOutput(_isPlaying);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to reopen the radio output");
            }

            RaiseOutputStateChanged();
            return;
        }

        lock (_stateLock)
        {
            if (_crossfadeIncoming is not null)
            {
                _outputChangePending = true;
                return;
            }
        }

        ApplyOutputChange();
    }

    /// <summary>
    /// Releases the output and, while playing, opens a new one on the current target at the same position. The pipeline
    /// is kept, so a pending gapless track stays valid and <see cref="_generation"/> does not change.
    /// </summary>
    private void ApplyOutputChange()
    {
        lock (_outputLock)
        {
            AudioOutputHandle? previous;
            PlaybackPipeline? pipeline;

            lock (_stateLock)
            {
                previous = _output;
                pipeline = _pipeline;
                _output = null;
                _outputChangePending = false;
            }

            ReleaseOutput(previous);

            if (pipeline is not null && _isPlaying)
            {
                try
                {
                    AudioOutputHandle output = OpenOutput(pipeline);

                    lock (_stateLock)
                    {
                        _output = output;
                    }

                    output.Player.Play();

                    if (!_positionTimer.Enabled)
                        _positionTimer.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to reopen the audio output");
                }
            }
        }

        RaiseOutputStateChanged();
    }

    private AudioOutputHandle EnsureOutput()
    {
        AudioOutputHandle output;

        lock (_outputLock)
        {
            PlaybackPipeline pipeline;

            lock (_stateLock)
            {
                if (_output is not null)
                    return _output;

                pipeline = _pipeline ?? throw new InvalidOperationException("No track is loaded.");
            }

            output = OpenOutput(pipeline);

            lock (_stateLock)
            {
                _output = output;
            }
        }

        RaiseOutputStateChanged();

        return output;
    }

    /// <summary>Opens an output for <paramref name="pipeline"/>; call it under <see cref="_outputLock"/>.</summary>
    private AudioOutputHandle OpenOutput(PlaybackPipeline pipeline)
    {
        AudioOutputTarget target = _target;

        if (target.Mode == EAudioOutputMode.Exclusive && _nativeBits == UnknownDepth && _currentFile is not null)
            _nativeBits = _formatProbe.GetBitsPerSample(_currentFile);

        AudioOutputHandle output = _outputFactory.Open(target, pipeline.Output, Math.Max(0, _nativeBits));
        output.Player.PlaybackStopped += Output_PlaybackStopped;

        return output;
    }

    private void ReleaseOutput(AudioOutputHandle? output)
    {
        if (output is null)
            return;

        output.Player.PlaybackStopped -= Output_PlaybackStopped;
        output.Dispose();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Both tracks play on their own shared output; each pipeline ramps its own samples (<see cref="FadeSampleProvider"/>),
    /// so the fade is sample-accurate and the master volume can still change while it runs.
    /// </remarks>
    public Task CrossfadeToAsync(TrackDto nextTrack, float replayGain, double durationSeconds, CancellationToken ct) =>
        CrossfadeToAsync(nextTrack, replayGain, durationSeconds, new MixTransition(0, BassSwap: false), ct);

    /// <inheritdoc />
    public void SetTransitionCue(long trackId, double positionSeconds) => _cueGate.Arm(trackId, positionSeconds);

    /// <inheritdoc />
    public void ClearTransitionCue() => _cueGate.Clear();

    /// <inheritdoc />
    public async Task CrossfadeToAsync(TrackDto nextTrack, float replayGain, double durationSeconds, MixTransition transition, CancellationToken ct)
    {
        if (_isLive) return;

        if (durationSeconds <= 0)
            return;

        PlaybackPipeline nextPipeline;

        try
        {
            nextPipeline = PlaybackPipeline.Create(new AudioFileReader(nextTrack.MusicFile), replayGain, nextTrack.Id, _bandGains, _volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crossfade: failed to open next track {File}", nextTrack.MusicFile);
            return;
        }

        double nextLength = nextPipeline.Reader.TotalTime.TotalSeconds;

        double incomingStartSeconds = transition.IncomingStartSeconds;
        double incomingStart = incomingStartSeconds > 0 && incomingStartSeconds < nextLength ? incomingStartSeconds : 0;

        if (incomingStart > 0)
        {
            try
            {
                nextPipeline.Reader.CurrentTime = TimeSpan.FromSeconds(incomingStart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Crossfade: failed to seek next track {File}", nextTrack.MusicFile);
                nextPipeline.Dispose();
                return;
            }
        }

        if (nextLength > 0)
            durationSeconds = Math.Min(durationSeconds, (nextLength - incomingStart) / 2);

        TimeSpan duration = TimeSpan.FromSeconds(durationSeconds);
        bool bassSwap = transition.BassSwap && BassSwapCurve.Applies(durationSeconds);
        TimeSpan swapAt = TimeSpan.FromSeconds(BassSwapCurve.ResolveSwapAt(transition.BassSwapAtSeconds, durationSeconds));

        if (bassSwap)
            nextPipeline.BassSwap.Start(EBassSwapRole.Incoming, duration, swapAt);

        nextPipeline.Fade.Start(EFadeDirection.In, duration);

        PlaybackPipeline? outgoing;

        lock (_stateLock)
        {
            _crossfadeIncoming = nextPipeline;
            outgoing = _pipeline;
        }

        AudioOutputHandle? nextOutput = null;

        try
        {
            nextOutput = _outputFactory.Open(_target with { Mode = EAudioOutputMode.Shared }, nextPipeline.Output, 0);
            nextOutput.Player.Play();
            TimeSpan outgoingRamp = outgoing is null ? duration : RemainingRamp(outgoing, duration);
            outgoing?.Fade.Start(EFadeDirection.Out, outgoingRamp);

            if (bassSwap)
                outgoing?.BassSwap.Start(EBassSwapRole.Outgoing, duration, swapAt);

            await WaitForFadeAsync(nextPipeline.Fade, duration, ct);

            PromoteCrossfade(nextPipeline, nextOutput, nextTrack.MusicFile);

            OnMediaChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            outgoing?.Fade.Reset();
            outgoing?.BassSwap.Reset();
            AbandonCrossfade(nextPipeline, nextOutput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crossfade: unexpected error");
            outgoing?.Fade.Reset();
            outgoing?.BassSwap.Reset();
            AbandonCrossfade(nextPipeline, nextOutput);
        }

        ApplyPendingOutputChange();
    }

    /// <summary>
    /// Outgoing ramp shortened to what is left of the outgoing track, so that it reaches silence before the file ends
    /// even when the incoming output took time to open.
    /// </summary>
    private static TimeSpan RemainingRamp(PlaybackPipeline outgoing, TimeSpan duration)
    {
        TimeSpan remaining = outgoing.Reader.TotalTime - outgoing.Reader.CurrentTime;

        return remaining > TimeSpan.Zero && remaining < duration ? remaining : duration;
    }

    /// <summary>Waits until the incoming ramp has been rendered, or its duration plus a grace period when output stalls.</summary>
    private static async Task WaitForFadeAsync(FadeSampleProvider fade, TimeSpan duration, CancellationToken ct)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan deadline = duration + FadeCompletionGrace;

        while (!fade.IsComplete && elapsed.Elapsed < deadline)
            await Task.Delay(FadePollIntervalMs, ct);

        ct.ThrowIfCancellationRequested();
    }

    private void AbandonCrossfade(PlaybackPipeline nextPipeline, AudioOutputHandle? nextOutput)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_crossfadeIncoming, nextPipeline))
                _crossfadeIncoming = null;
        }

        nextOutput?.Dispose();
        nextPipeline.Dispose();
    }

    private void PromoteCrossfade(PlaybackPipeline nextPipeline, AudioOutputHandle nextOutput, string nextFile)
    {
        _positionTimer.Stop();
        _cueGate.Clear();

        AudioOutputHandle? previousOutput;
        PlaybackPipeline? previousPipeline;

        nextPipeline.Fade.Reset();
        nextPipeline.BassSwap.Reset();
        nextPipeline.Chain.SourceSwitched += Chain_SourceSwitched;
        nextOutput.Player.PlaybackStopped += Output_PlaybackStopped;

        lock (_outputLock)
        {
            lock (_stateLock)
            {
                previousOutput = _output;
                previousPipeline = _pipeline;
                _output = nextOutput;
                _pipeline = nextPipeline;
                _currentFile = nextFile;
                _nativeBits = UnknownDepth;
                _pendingNext = null;
                _crossfadeIncoming = null;
                _generation++;
                _length = nextPipeline.Reader.TotalTime.TotalSeconds > 0 ? nextPipeline.Reader.TotalTime.TotalSeconds : 1;
                _aboutToEndRaised = false;
            }

            ReleaseOutput(previousOutput);
        }

        ReleasePipeline(previousPipeline);

        _positionTimer.Start();

        RaiseOutputStateChanged();
    }

    private void ApplyPendingOutputChange()
    {
        bool pending;

        lock (_stateLock)
        {
            pending = _outputChangePending && _crossfadeIncoming is null;
        }

        if (pending)
            ApplyOutputChange();
    }

    public EqualizerBand[]? GetEqualizerBands()
    {
        return _pipeline?.Equalizer.GetBands();
    }

    private static float ToLinearVolume(double volumePercent) => (float)Math.Clamp(volumePercent / 100.0, 0.0, 1.0);

    private void ReleasePipeline(PlaybackPipeline? pipeline)
    {
        if (pipeline is null)
            return;

        pipeline.Chain.SourceSwitched -= Chain_SourceSwitched;
        pipeline.Dispose();
    }

    private void Chain_SourceSwitched(object? sender, SourceSwitchedEventArgs e)
    {
        _cueGate.Clear();

        AudioFileReader previous;
        PendingNext? pending;
        int generation;

        lock (_stateLock)
        {
            PlaybackPipeline? pipeline = _pipeline;

            if (pipeline is null || !ReferenceEquals(pipeline.Chain, sender) || e.Current is not ReplayGainSampleProvider { Source: AudioFileReader next } source)
                return;

            pending = ReferenceEquals(_pendingNext?.Source, source) ? _pendingNext : null;
            previous = pipeline.Reader;
            pipeline.Reader = next;
            pipeline.Source = source;
            pipeline.TrackId = pending?.Track.Id ?? 0;
            _currentFile = pending?.Track.MusicFile;
            _nativeBits = pending?.NativeBits ?? UnknownDepth;
            _length = next.TotalTime.TotalSeconds > 0 ? next.TotalTime.TotalSeconds : 1;
            _aboutToEndRaised = false;
            _pendingNext = null;
            generation = _generation;
        }

        ThreadPool.UnsafeQueueUserWorkItem(state => state.Player.CompleteGaplessSwitch(state.Previous, state.Pending, state.Generation), (Player: this, Previous: previous, Pending: pending, Generation: generation), preferLocal: false);
    }

    private void CompleteGaplessSwitch(AudioFileReader previous, PendingNext? pending, int generation)
    {
        try
        {
            double previousPosition = previous.CurrentTime.TotalSeconds;
            previous.Dispose();

            bool isCurrent;

            lock (_stateLock)
            {
                isCurrent = generation == _generation;
            }

            if (!isCurrent)
                return;

            NotifyIfNeutralityChanged();

            if (pending is null)
            {
                _logger.LogWarning("Gapless: the output switched to a track that was not tracked as pending");
                return;
            }

            OnGaplessTransition?.Invoke(this, new GaplessTransitionEventArgs(pending.Track, previousPosition));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gapless: failed to complete the track switch");
        }
    }

    /// <summary>
    /// Raised on the output's own playback thread: hand off at once, since stopping that output from here would wait for
    /// the thread to end (a deadlock).
    /// </summary>
    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not IWavePlayer player)
            return;

        ThreadPool.UnsafeQueueUserWorkItem(state => state.Engine.HandleOutputStopped(state.Player, state.Exception), (Engine: this, Player: player, e.Exception), preferLocal: false);
    }

    /// <summary>A stop without error ends the track; a stop with an error means the device was lost.</summary>
    internal void HandleOutputStopped(IWavePlayer player, Exception? exception)
    {
        AudioOutputHandle? output;

        lock (_stateLock)
        {
            output = _output;
        }

        if (output is null || !ReferenceEquals(output.Player, player))
            return;

        _positionTimer.Stop();

        if (exception is null)
        {
            OnMediaEnded?.Invoke(this, EventArgs.Empty);
            return;
        }

        _logger.LogWarning(exception, "Audio output lost at position {Position}s", Position);

        bool detached = false;

        lock (_outputLock)
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_output, output))
                {
                    _output = null;
                    detached = true;
                }
            }

            if (detached)
                ReleaseOutput(output);
        }

        if (!detached)
            return;

        OnOutputLost?.Invoke(this, new OutputLostEventArgs(_isPlaying, output.IsOnPreferred));
        RaiseOutputStateChanged();
    }

    private void NotifyIfNeutralityChanged()
    {
        bool isNeutral = IsProcessingNeutral;

        if (isNeutral == _lastNeutral)
            return;

        _lastNeutral = isNeutral;
        RaiseOutputStateChanged();
    }

    private void RaiseOutputStateChanged()
    {
        try
        {
            OnOutputStateChanged?.Invoke(this, OutputState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish the audio output state");
        }
    }

    private void PositionTimer_Elapsed(object? s, ElapsedEventArgs e)
    {
        PlaybackPipeline? pipeline = _pipeline;
        AudioOutputHandle? output = _output;

        if (pipeline is null || output is null || output.Player.PlaybackState != PlaybackState.Playing)
            return;

        double pos;

        try
        {
            pos = pipeline.Reader.CurrentTime.TotalSeconds;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (_cueGate.ShouldRaise(pipeline.TrackId, pos))
        {
            try
            {
                OnTransitionCue?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A transition cue handler failed");
            }
        }

        double len = Length;

        if (len <= 0)
            return;

        bool isAboutToEnd = pos >= len - _aboutToEndDelay && !_aboutToEndRaised;

        if (isAboutToEnd)
        {
            _aboutToEndRaised = true;
            OnMediaAboutToEnd?.Invoke(this, EventArgs.Empty);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _streaming?.Stop();
                _streaming?.Dispose();
                _streaming = null;

                _positionTimer.Stop();
                _positionTimer.Elapsed -= PositionTimer_Elapsed;
                _positionTimer.Dispose();

                ReleaseOutput(_output);
                _output = null;

                ReleasePipeline(_pipeline);
                _pipeline = null;
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private sealed record PendingNext(ReplayGainSampleProvider Source, TrackDto Track, int NativeBits);
}