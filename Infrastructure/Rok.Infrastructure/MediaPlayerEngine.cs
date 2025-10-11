using Rok.Application.Dto;
using Rok.Application.Interfaces;
using System.Timers;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Rok.Infrastructure;

public class WinUIMediaPlayer : IPlayerEngine, IDisposable
{
    public event EventHandler? OnMediaChanged;
    public event EventHandler? OnMediaEnded;
    public event EventHandler? OnMediaStateChanged;
    public event EventHandler? OnMediaAboutToEnd;

    private readonly MediaPlayer _player;

    private readonly System.Timers.Timer _positionTimer;

    private readonly int _crossfaceDelay = 5;
    private readonly int _aboutToEndDelay = 15;

    private double _length;
    private bool _aboutToEndRaised;
    private bool disposedValue;

    public double Position => _player.PlaybackSession?.Position.TotalSeconds ?? 0;

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

    public WinUIMediaPlayer()
    {
        _player = new MediaPlayer
        {
            CommandManager = { IsEnabled = false }
        };

        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
        _player.PlaybackSession.PlaybackStateChanged += Session_PlaybackStateChanged;

        _positionTimer = new System.Timers.Timer(250)
        {
            AutoReset = true,
            Enabled = false
        };

        _positionTimer.Elapsed += PositionTimer_Elapsed;
    }

    public void Pause()
    {
        _player.Pause();
    }

    public void Play()
    {
        _player.Play();

        if (!_positionTimer.Enabled)
            _positionTimer.Start();
    }

    public void Stop()
    {
        _positionTimer.Stop();

        Length = 0;
        SetPosition(0);
        _player.Pause();

        _player.Source = null;

        _aboutToEndRaised = false;

        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPosition(double position)
    {
        if (position < 0)
            position = 0;

        _player.PlaybackSession.Position = TimeSpan.FromSeconds(position);

        if (Position < Math.Max(0, Length - _aboutToEndDelay))
            _aboutToEndRaised = false;
    }

    public void SetVolume(double volume)
    {
        double clampVolume = Math.Clamp(volume / 100.0, 0.0, 1.0);
        _player.Volume = (float)clampVolume;
    }

    public bool SetTrack(TrackDto track)
    {
        try
        {
            Stop();

            Uri uri = new(track.MusicFile, UriKind.Absolute);
            MediaSource source = MediaSource.CreateFromUri(uri);
            _player.Source = source;

            _aboutToEndRaised = false;

            OnMediaChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Player_MediaOpened(MediaPlayer sender, object args)
    {
        TimeSpan duration = sender.PlaybackSession?.NaturalDuration ?? TimeSpan.Zero;
        Length = duration.TotalSeconds > 0 ? duration.TotalSeconds : 1;

        _aboutToEndRaised = false;

        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Player_MediaEnded(MediaPlayer sender, object args)
    {
        _positionTimer.Stop();
        OnMediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        _positionTimer.Stop();
        OnMediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void Session_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);

        if (sender.PlaybackState == MediaPlaybackState.Playing)
        {
            if (!_positionTimer.Enabled)
                _positionTimer.Start();
        }
        else
        {
            _positionTimer.Stop();
        }
    }

    private void PositionTimer_Elapsed(object? s, ElapsedEventArgs e)
    {
        MediaPlaybackSession? session = _player.PlaybackSession;
        if (session is null)
            return;

        double pos = session.Position.TotalSeconds;
        double len = Length;

        if (len <= 0)
            return;

        if (pos >= len - _aboutToEndDelay &&
            pos < len - _crossfaceDelay &&
            !_aboutToEndRaised)
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

                _player.PlaybackSession.PlaybackStateChanged -= Session_PlaybackStateChanged;
                _player.MediaOpened -= Player_MediaOpened;
                _player.MediaEnded -= Player_MediaEnded;
                _player.MediaFailed -= Player_MediaFailed;

                _player.Dispose();
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