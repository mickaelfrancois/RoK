using Rok.Application.Player;

namespace Rok.Logic.Services.Player;

public class PlayerService : IPlayerService
{
    private EPlaybackState _playerState = EPlaybackState.Stopped;

    public EPlaybackState PlaybackState
    {
        get => _playerState;
        private set
        {
            _playerState = value;

            Messenger.Send(new MediaStateChanged(_playerState));
        }
    }

    private double _volume = 100;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            _player.SetVolume(_volume);
        }
    }

    public bool IsLoopingEnabled { get; set; }

    private double _seek;

    public double Position
    {
        get => _player.Position;
        set => Task.Run(() =>
        {
            _seek = value;

            if (PlaybackState == EPlaybackState.Paused || PlaybackState == EPlaybackState.Ended)
                Play();

            _player.SetPosition(_seek);
        });
    }

    public List<TrackDto> Playlist { get; private set; } = [];

    private int _currentIndex = 0;

    public bool CanSeek { get; set; } = true;

    private TrackDto? _currentTrack;

    public TrackDto CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            TrackDto? previousTrack = _currentTrack;

            _currentTrack = value;

            if (previousTrack == null || previousTrack.Id != _currentTrack.Id)
                Messenger.Send(new MediaChangedMessage(_currentTrack, previousTrack));
        }
    }

    private double _volumeBeforeMute = 50;

    private const double KDefaultMuteVolume = 50;

    private bool _isMuted;

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (value)
            {
                _volumeBeforeMute = Volume;
                Volume = 0;
            }
            else
            {
                Volume = _volumeBeforeMute > 0 ? _volumeBeforeMute : KDefaultMuteVolume;
            }

            _isMuted = value;
        }
    }

    public bool CanNext
    {
        get
        {
            if (IsLoopingEnabled)
                return true;

            return _currentIndex + 1 < Playlist.Count;
        }
    }

    public bool CanPrevious
    {
        get
        {
            if (IsLoopingEnabled)
                return true;

            return _currentIndex - 1 >= 0;
        }
    }

    private readonly IPlayerEngine _player;

    private readonly ILogger<PlayerService> _logger;


    public PlayerService(IPlayerEngine player, ILogger<PlayerService> logger)
    {
        _player = Guard.Against.Null(player, nameof(player));
        _logger = Guard.Against.Null(logger, nameof(logger));

        InitEvents();

#if DEBUG
        _volume = 5;
#endif
    }


    public void InitEvents()
    {
        _player.OnMediaAboutToEnd += OnMediaAboutToEnd;
        _player.OnMediaChanged += OnMediaChanged;
        _player.OnMediaEnded += OnMediaEnded;
        _player.OnMediaStateChanged += OnMediaStateChanged;
    }


    #region Events

    private void OnMediaStateChanged(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media state changed fired.");
    }


    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media ended fired.");

        Messenger.Send(new MediaEvent(EPlaybackState.Stopped, CurrentTrack));

        Next();
    }


    private void OnMediaChanged(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media changed fired.");
    }


    private void OnMediaAboutToEnd(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media about to end fired.");

        Messenger.Send(new MediaAboutToEndEvent(CurrentTrack));
    }


    public void LoadPlaylist(List<TrackDto> tracks, TrackDto? startTrack = null)
    {
        Guard.Against.Null(tracks);

        if (CurrentTrack != null)
            Messenger.Send(new MediaEvent(EPlaybackState.Ended, CurrentTrack));

        Stop(false);

        Playlist = tracks;
        _currentIndex = 0;
        _currentTrack = null;

        Start(startTrack, null);

        Messenger.Send(new PlaylistChanged(Playlist));
    }


    public void AddTracksToPlaylist(List<TrackDto> tracks)
    {
        Guard.Against.Null(tracks);

        tracks.ForEach(c => Playlist.Add(c));

        Messenger.Send(new PlaylistChanged(Playlist));
    }


    public void InsertTracksToPlaylist(List<TrackDto> tracks, int? index = null)
    {
        Guard.Against.Null(tracks);

        if (tracks.Count == 0)
            return;

        List<TrackDto> itemsToInsert = new(tracks.Count);
        foreach (TrackDto? t in tracks)
        {
            if (t is not null)
                itemsToInsert.Add(t);
        }

        if (itemsToInsert.Count == 0)
            return;

        if (index == null) index = _currentIndex + 1;
        if (index < 0) index = 0;
        if (index > Playlist.Count) index = Playlist.Count;

        Playlist.InsertRange(index.Value, itemsToInsert);

        Messenger.Send(new PlaylistChanged(Playlist));
    }


    public void Start(TrackDto? startTrack = null, TimeSpan? startPosition = null)
    {
        if (startTrack == null)
            _currentIndex = 0;
        else
            _currentIndex = Playlist.FindIndex(c => c.Id == startTrack.Id);

        LoadFile(Playlist[_currentIndex]);

        if (startPosition.HasValue)
            Position = startPosition.Value.TotalSeconds;

        Play();
    }

    public void Pause()
    {
        PlaybackState = EPlaybackState.Paused;

        _player.Pause();
    }

    public void Play()
    {
        Volume = _volume;

        _player.Play();

        PlaybackState = EPlaybackState.Playing;
    }

    public void Stop(bool firePlaybackStateChange)
    {
        _player.Stop();

        if (firePlaybackStateChange)
            PlaybackState = EPlaybackState.Stopped;
    }

    public void Skip()
    {
        Next();
    }

    public void Next()
    {
        if (_currentIndex + 1 >= Playlist.Count)
        {
            if (IsLoopingEnabled)
            {
                _currentIndex = 0;
            }
            else
            {
                // Playlist ended
                PlaybackState = EPlaybackState.Stopped;
                return;
            }
        }

        LoadFile(Playlist[++_currentIndex]);
        Play();
    }

    public void Previous()
    {
        if (_currentIndex - 1 < 0)
        {
            if (IsLoopingEnabled)
                _currentIndex = Playlist.Count - 1;
            else
                return;
        }

        LoadFile(Playlist[--_currentIndex]);
        Play();
    }

    #endregion


    #region Engine

    private bool LoadFile(TrackDto track)
    {
        _player.Stop();

        bool res = _player.SetTrack(track);

        if (res)
        {
            CurrentTrack = track;
        }

        return res;
    }

    #endregion
}