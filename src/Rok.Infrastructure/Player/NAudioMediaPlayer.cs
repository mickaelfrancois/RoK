using System.Diagnostics;
using System.Timers;
using Microsoft.Extensions.Logging;
using NAudio;
using NAudio.Wave;
using Rok.Application.Dto;
using Rok.Application.Interfaces;

namespace Rok.Infrastructure.Player;

public class NAudioMediaPlayer : IPlayerEngine, IDisposable
{
    public event EventHandler? OnMediaChanged;
    public event EventHandler? OnMediaEnded;
    public event EventHandler? OnMediaStateChanged;
    public event EventHandler? OnMediaAboutToEnd;

    private IWavePlayer? _outputDevice;
    private AudioFileReader? _audioFileReader;
    private Equalizer? _equalizer;
    private readonly System.Timers.Timer _positionTimer;
    private readonly ILogger<NAudioMediaPlayer> _logger;

    private readonly int _crossfadeDelay = 5;
    private readonly int _aboutToEndDelay = 15;

    public int CrossfadeDelay => _crossfadeDelay;

    private double _length;
    private bool _aboutToEndRaised;
    private bool disposedValue;

    public double Position => _audioFileReader?.CurrentTime.TotalSeconds ?? 0;

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

    public NAudioMediaPlayer(ILogger<NAudioMediaPlayer> logger)
    {
        _logger = logger;

        _positionTimer = new System.Timers.Timer(250)
        {
            AutoReset = true,
            Enabled = false
        };

        _positionTimer.Elapsed += PositionTimer_Elapsed;
    }

    public void Pause()
    {
        if (_outputDevice is not null && _outputDevice.PlaybackState == PlaybackState.Playing)
        {
            _outputDevice.Pause();
            _positionTimer.Stop();
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Play()
    {
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
        if (_equalizer is null || _audioFileReader is null)
            return;

        double currentPosition = _audioFileReader.CurrentTime.TotalSeconds;

        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        _outputDevice = new WaveOutEvent();
        _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
        _outputDevice.Init(_equalizer);

        _audioFileReader.CurrentTime = TimeSpan.FromSeconds(currentPosition);

        _logger.LogInformation("Audio device reinitialized, position restored to {Position}s", currentPosition);
    }

    public void Stop()
    {
        _positionTimer.Stop();

        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
            _outputDevice.Stop();
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        if (_audioFileReader is not null)
        {
            _audioFileReader.Dispose();
            _audioFileReader = null;
        }

        _equalizer = null;

        Length = 0;
        _aboutToEndRaised = false;

        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPosition(double position)
    {
        if (_audioFileReader is null)
            return;

        if (position < 0)
            position = 0;

        _audioFileReader.CurrentTime = TimeSpan.FromSeconds(position);

        if (Position < Math.Max(0, Length - _aboutToEndDelay))
            _aboutToEndRaised = false;
    }

    public void SetVolume(double volume)
    {
        if (_audioFileReader is not null)
        {
            float clampVolume = (float)Math.Clamp(volume / 100.0, 0.0, 1.0);
            _audioFileReader.Volume = clampVolume;
        }
    }

    public bool SetTrack(TrackDto track)
    {
        try
        {
            Stop();

            _audioFileReader = new AudioFileReader(track.MusicFile);
            Length = _audioFileReader.TotalTime.TotalSeconds > 0
                ? _audioFileReader.TotalTime.TotalSeconds
                : 1;

            EqualizerBand[] bands = new[]
            {
                new EqualizerBand(32f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(64f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(125f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(250f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(500f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(1000f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(2000f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(4000f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(8000f, 1f, _audioFileReader.WaveFormat.Channels),
                new EqualizerBand(16000f, 1f, _audioFileReader.WaveFormat.Channels)
            };

            _equalizer = new Equalizer(_audioFileReader, bands);

            _outputDevice = new WaveOutEvent();
            _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            _outputDevice.Init(_equalizer);

            _aboutToEndRaised = false;

            OnMediaChanged?.Invoke(this, EventArgs.Empty);
            OnMediaStateChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
        catch
        {
            if (_audioFileReader is not null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }

            if (_outputDevice is not null)
            {
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            _equalizer = null;

            return false;
        }
    }

    public void SetEqualizerBand(int bandIndex, float gain)
    {
        _equalizer?.UpdateBand(bandIndex, gain);
    }

    /// <inheritdoc />
    public async Task CrossfadeToAsync(TrackDto nextTrack, double durationSeconds, double masterVolume, CancellationToken ct)
    {
        if (durationSeconds <= 0)
            return;

        AudioFileReader nextReader;
        try
        {
            nextReader = new AudioFileReader(nextTrack.MusicFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crossfade: failed to open next track {File}", nextTrack.MusicFile);
            return;
        }

        // Build the full equalizer chain upfront so the next device plays continuously without restart
        EqualizerBand[] nextBands = new[]
        {
            new EqualizerBand(32f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(64f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(125f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(250f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(500f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(1000f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(2000f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(4000f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(8000f, 1f, nextReader.WaveFormat.Channels),
            new EqualizerBand(16000f, 1f, nextReader.WaveFormat.Channels)
        };

        Equalizer nextEqualizer = new(nextReader, nextBands);

        WaveOutEvent nextDevice = new();

        try
        {
            nextDevice.Init(nextEqualizer);
            nextReader.Volume = 0f;
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
                nextReader.Volume = (float)Math.Clamp(fadeInVolume / 100.0, 0.0, 1.0);

                if (progress >= 1.0)
                    break;

                await Task.Delay(intervalMs, ct);
            }

            // Promote nextDevice as the main player — no Stop/Init/Play, just wire events and swap state
            _positionTimer.Stop();

            if (_outputDevice is not null)
            {
                _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
            }

            _audioFileReader?.Dispose();

            _audioFileReader = nextReader;
            _equalizer = nextEqualizer;
            _length = nextReader.TotalTime.TotalSeconds > 0 ? nextReader.TotalTime.TotalSeconds : 1;
            nextReader.Volume = (float)Math.Clamp(masterVolume / 100.0, 0.0, 1.0);
            _aboutToEndRaised = false;

            _outputDevice = nextDevice;
            _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;

            _positionTimer.Start();

            OnMediaChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            nextDevice?.Stop();
            nextDevice?.Dispose();
            nextReader.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crossfade: unexpected error");
            nextDevice?.Stop();
            nextDevice?.Dispose();
            nextReader.Dispose();
        }
    }

    public EqualizerBand[]? GetEqualizerBands()
    {
        return _equalizer?.GetBands();
    }

    private static double DbInterpolate(double t, double masterVolumePercent, double minDb = -80.0)
    {
        double curDb = 0.0 * (1.0 - t) + minDb * t;
        double gain = Math.Pow(10.0, curDb / 20.0);
        double v = gain * masterVolumePercent;
        return double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : Math.Clamp(v, 0.0, 100.0);
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
        if (_audioFileReader is null || _outputDevice is null || _outputDevice.PlaybackState != PlaybackState.Playing)
            return;

        double pos = _audioFileReader.CurrentTime.TotalSeconds;
        double len = Length;

        if (len <= 0)
            return;

        bool isAboutToEnd = pos >= len - _aboutToEndDelay &&
                    pos < len - _crossfadeDelay &&
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
                _positionTimer.Stop();
                _positionTimer.Elapsed -= PositionTimer_Elapsed;
                _positionTimer.Dispose();

                if (_outputDevice is not null)
                {
                    _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                    _outputDevice.Dispose();
                }

                if (_audioFileReader is not null)
                {
                    _audioFileReader.Dispose();
                }
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}