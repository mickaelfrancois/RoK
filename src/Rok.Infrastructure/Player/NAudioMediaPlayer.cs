using System.Diagnostics;
using System.Timers;
using Microsoft.Extensions.Logging;
using NAudio;
using NAudio.Wave;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Player.Streaming;

namespace Rok.Infrastructure.Player;

public class NAudioMediaPlayer : IPlayerEngine, IDisposable
{
    public event EventHandler? OnMediaChanged;
    public event EventHandler? OnMediaEnded;
    public event EventHandler? OnMediaStateChanged;
    public event EventHandler? OnMediaAboutToEnd;
    public event EventHandler<string>? OnMetadataChanged;
    public event EventHandler<GaplessTransitionEventArgs>? OnGaplessTransition;

    public bool IsLive => _isLive;

    public bool IsBuffering => _streaming?.IsBuffering ?? false;

    private IWavePlayer? _outputDevice;
    private PlaybackPipeline? _pipeline;
    private PendingNext? _pendingNext;
    private int _generation;
    private float _volume = 1f;
    private readonly float[] _bandGains = new float[PlaybackPipeline.BandFrequencies.Length];
    private readonly Lock _stateLock = new();
    private readonly System.Timers.Timer _positionTimer;
    private readonly ILogger<NAudioMediaPlayer> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private StreamingPlayback? _streaming;
    private bool _isLive;

    private readonly int _crossfadeDelay = 5;
    private readonly int _aboutToEndDelay = 15;

    public int CrossfadeDelay => _crossfadeDelay;

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

    public NAudioMediaPlayer(ILogger<NAudioMediaPlayer> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        _positionTimer = new System.Timers.Timer(250)
        {
            AutoReset = true,
            Enabled = false
        };

        _positionTimer.Elapsed += PositionTimer_Elapsed;
    }

    public void Pause()
    {
        if (_isLive)
        {
            _streaming?.Pause();
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_outputDevice is not null && _outputDevice.PlaybackState == PlaybackState.Playing)
        {
            _outputDevice.Pause();
            _positionTimer.Stop();
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Play()
    {
        if (_isLive)
        {
            _streaming?.Resume();
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_outputDevice is not null && _outputDevice.PlaybackState != PlaybackState.Playing)
        {
            try
            {
                _outputDevice.Play();
            }
            catch (MmException ex)
            {
                _logger.LogWarning(ex, "Audio device lost (NoDriver), attempting to reinitialize");
                ReinitializeOutputDevice();
                _outputDevice?.Play();
            }

            if (!_positionTimer.Enabled)
                _positionTimer.Start();

            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ReinitializeOutputDevice()
    {
        PlaybackPipeline? pipeline = _pipeline;

        if (pipeline is null)
            return;

        double currentPosition = pipeline.Reader.CurrentTime.TotalSeconds;

        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        _outputDevice = new WaveOut();
        _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
        _outputDevice.Init(pipeline.Output);

        pipeline.Reader.CurrentTime = TimeSpan.FromSeconds(currentPosition);

        _logger.LogInformation("Audio device reinitialized, position restored to {Position}s", currentPosition);
    }

    public void Stop()
    {
        if (_isLive)
        {
            _streaming?.Stop();
            _streaming?.Dispose();
            _streaming = null;
            _isLive = false;
            _length = 0;
            _aboutToEndRaised = false;
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _positionTimer.Stop();

        IWavePlayer? device;
        PlaybackPipeline? pipeline;

        lock (_stateLock)
        {
            device = _outputDevice;
            pipeline = _pipeline;
            _outputDevice = null;
            _pipeline = null;
            _pendingNext = null;
            _generation++;
        }

        if (device is not null)
        {
            device.PlaybackStopped -= OutputDevice_PlaybackStopped;
            device.Stop();
            device.Dispose();
        }

        ReleasePipeline(pipeline);

        Length = 0;
        _aboutToEndRaised = false;

        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
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

        if (_pipeline is not null)
            _pipeline.Volume.Volume = _volume;
    }

    public bool SetTrack(TrackDto track)
    {
        AudioFileReader? reader = null;

        try
        {
            Stop();

            reader = new AudioFileReader(track.MusicFile);
            PlaybackPipeline pipeline = PlaybackPipeline.Create(reader, _bandGains, _volume);

            Length = reader.TotalTime.TotalSeconds > 0
                ? reader.TotalTime.TotalSeconds
                : 1;

            WaveOut device = new();
            device.PlaybackStopped += OutputDevice_PlaybackStopped;
            device.Init(pipeline.Output);

            pipeline.Chain.SourceSwitched += Chain_SourceSwitched;

            lock (_stateLock)
            {
                _pipeline = pipeline;
                _outputDevice = device;
            }

            _aboutToEndRaised = false;

            OnMediaChanged?.Invoke(this, EventArgs.Empty);
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
        catch
        {
            reader?.Dispose();

            if (_outputDevice is not null)
            {
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            _pipeline = null;

            return false;
        }
    }

    public bool SetStream(RadioStationDto station)
    {
        Stop();

        _streaming = new StreamingPlayback(_httpClientFactory.CreateClient("RadioStream"), _logger);
        _streaming.MetadataChanged += (_, title) => OnMetadataChanged?.Invoke(this, title);
        _streaming.PlaybackEnded += (_, _) => OnMediaEnded?.Invoke(this, EventArgs.Empty);
        _streaming.BufferingChanged += (_, _) => OnMediaStateChanged?.Invoke(this, EventArgs.Empty);

        _isLive = true;
        _length = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                await _streaming.StartAsync(station, CancellationToken.None);
                OnMediaChanged?.Invoke(this, EventArgs.Empty);
                OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
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

    public void SetEqualizerBand(int bandIndex, float gain)
    {
        if (bandIndex >= 0 && bandIndex < _bandGains.Length)
            _bandGains[bandIndex] = gain;

        _pipeline?.Equalizer.UpdateBand(bandIndex, gain);
    }

    /// <inheritdoc />
    public bool QueueNextTrack(TrackDto nextTrack)
    {
        if (_isLive || _pipeline is null)
            return false;

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

        bool queued = false;
        ISampleProvider? replaced = null;

        lock (_stateLock)
        {
            if (_pipeline is not null && _pipeline.Chain.TryQueueNext(reader, out replaced))
            {
                _pendingNext = new PendingNext(reader, nextTrack);
                queued = true;
            }
        }

        (replaced as IDisposable)?.Dispose();

        if (!queued)
        {
            _logger.LogInformation("Gapless: {File} has a different format, falling back to a regular transition", nextTrack.MusicFile);
            reader.Dispose();
        }

        return queued;
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
    public async Task CrossfadeToAsync(TrackDto nextTrack, double durationSeconds, double masterVolume, CancellationToken ct)
    {
        if (_isLive) return;

        if (durationSeconds <= 0)
            return;

        PlaybackPipeline nextPipeline;

        try
        {
            nextPipeline = PlaybackPipeline.Create(new AudioFileReader(nextTrack.MusicFile), _bandGains, 0f);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crossfade: failed to open next track {File}", nextTrack.MusicFile);
            return;
        }

        WaveOut nextDevice = new();

        try
        {
            nextDevice.Init(nextPipeline.Output);
            nextDevice.Play();

            const int intervalMs = 50;
            var sw = Stopwatch.StartNew();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                double progress = Math.Clamp(sw.Elapsed.TotalSeconds / durationSeconds, 0.0, 1.0);

                double fadeOutVolume = Math.Max(0, DbInterpolate(progress, masterVolume));
                SetVolume(fadeOutVolume);

                double fadeInVolume = Math.Max(0, DbInterpolate(1.0 - progress, masterVolume));
                nextPipeline.Volume.Volume = ToLinearVolume(fadeInVolume);

                if (progress >= 1.0)
                    break;

                await Task.Delay(intervalMs, ct);
            }

            PromoteCrossfade(nextPipeline, nextDevice, masterVolume);

            OnMediaChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            nextDevice.Stop();
            nextDevice.Dispose();
            nextPipeline.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crossfade: unexpected error");
            nextDevice.Stop();
            nextDevice.Dispose();
            nextPipeline.Dispose();
        }
    }

    private void PromoteCrossfade(PlaybackPipeline nextPipeline, WaveOut nextDevice, double masterVolume)
    {
        _positionTimer.Stop();

        IWavePlayer? previousDevice;
        PlaybackPipeline? previousPipeline;

        _volume = ToLinearVolume(masterVolume);
        nextPipeline.Volume.Volume = _volume;
        nextPipeline.Chain.SourceSwitched += Chain_SourceSwitched;
        nextDevice.PlaybackStopped += OutputDevice_PlaybackStopped;

        lock (_stateLock)
        {
            previousDevice = _outputDevice;
            previousPipeline = _pipeline;
            _outputDevice = nextDevice;
            _pipeline = nextPipeline;
            _pendingNext = null;
            _generation++;
            _length = nextPipeline.Reader.TotalTime.TotalSeconds > 0 ? nextPipeline.Reader.TotalTime.TotalSeconds : 1;
            _aboutToEndRaised = false;
        }

        if (previousDevice is not null)
        {
            previousDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
            previousDevice.Stop();
            previousDevice.Dispose();
        }

        ReleasePipeline(previousPipeline);

        _positionTimer.Start();
    }

    public EqualizerBand[]? GetEqualizerBands()
    {
        return _pipeline?.Equalizer.GetBands();
    }

    private static float ToLinearVolume(double volumePercent) => (float)Math.Clamp(volumePercent / 100.0, 0.0, 1.0);

    private static double DbInterpolate(double t, double masterVolumePercent, double minDb = -80.0)
    {
        double curDb = 0.0 * (1.0 - t) + minDb * t;
        double gain = Math.Pow(10.0, curDb / 20.0);
        double v = gain * masterVolumePercent;
        return double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : Math.Clamp(v, 0.0, 100.0);
    }

    private void ReleasePipeline(PlaybackPipeline? pipeline)
    {
        if (pipeline is null)
            return;

        pipeline.Chain.SourceSwitched -= Chain_SourceSwitched;
        pipeline.Dispose();
    }

    private void Chain_SourceSwitched(object? sender, SourceSwitchedEventArgs e)
    {
        AudioFileReader previous;
        PendingNext? pending;
        int generation;

        lock (_stateLock)
        {
            PlaybackPipeline? pipeline = _pipeline;

            if (pipeline is null || !ReferenceEquals(pipeline.Chain, sender) || e.Current is not AudioFileReader next)
                return;

            previous = pipeline.Reader;
            pipeline.Reader = next;
            _length = next.TotalTime.TotalSeconds > 0 ? next.TotalTime.TotalSeconds : 1;
            _aboutToEndRaised = false;
            pending = ReferenceEquals(_pendingNext?.Reader, next) ? _pendingNext : null;
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

    private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _positionTimer.Stop();

        if (e.Exception is not null)
            _logger.LogWarning(e.Exception, "Playback stopped unexpectedly at position {Position}s.", Position);

        OnMediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void PositionTimer_Elapsed(object? s, ElapsedEventArgs e)
    {
        PlaybackPipeline? pipeline = _pipeline;

        if (pipeline is null || _outputDevice is null || _outputDevice.PlaybackState != PlaybackState.Playing)
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

        double len = Length;

        if (len <= 0)
            return;

        bool isAboutToEnd = pos >= len - _aboutToEndDelay &&
                    (pos < len - _crossfadeDelay || len <= _aboutToEndDelay) &&
                    !_aboutToEndRaised;

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

                if (_outputDevice is not null)
                {
                    _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                    _outputDevice.Dispose();
                }

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

    private sealed record PendingNext(AudioFileReader Reader, TrackDto Track);
}