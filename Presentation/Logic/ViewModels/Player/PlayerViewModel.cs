using Microsoft.UI.Dispatching;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Albums.Command;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Tracks.Command;
using Rok.Application.Player;
using Rok.Infrastructure.Lyrics;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Player;

public partial class PlayerViewModel : ObservableObject
{
    private readonly IPlayerService _player;

    private readonly IMediator _mediator;

    private readonly NavigationService _navigationService;

    private readonly ILogger<PlayerViewModel> _logger;

    private readonly ILyricsService _lyricsService;

    private readonly DispatcherTimer _updateTimer;

    private readonly DispatcherTimer _lyricTimer;

    private readonly DispatcherTimer _backdropTimer;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    private bool _isFullScreen;

    private TrackViewModel? _currentTrack;
    public TrackViewModel? CurrentTrack
    {
        get
        {
            return _currentTrack;
        }
        set
        {
            _currentTrack = value;

            if (_player != null)
            {
                CanSkipNext = _player.CanNext;
                CanSkipPrevious = _player.CanPrevious;
            }

            OnPropertyChanged(string.Empty);
        }
    }

    private ArtistViewModel? _currentArtist;
    public ArtistViewModel? CurrentArtist
    {
        get => _currentArtist;
        set
        {
            _currentArtist = value;
            OnPropertyChanged();
        }
    }

    private AlbumViewModel? _currentAlbum;
    public AlbumViewModel? CurrentAlbum
    {
        get => _currentAlbum;
        set
        {
            _currentAlbum = value;
            OnPropertyChanged();
        }
    }

    private bool _canSkipNext;
    public bool CanSkipNext
    {
        get => _canSkipNext;
        set
        {
            if (_canSkipNext != value)
            {
                _canSkipNext = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _canSkipPrevious;
    public bool CanSkipPrevious
    {
        get => _canSkipPrevious;
        set
        {
            if (_canSkipPrevious != value)
            {
                _canSkipPrevious = value;
                OnPropertyChanged();
            }
        }
    }

    public bool RepeatAll
    {
        get => _player.IsLoopingEnabled;
        set
        {
            _player.IsLoopingEnabled = value;
            OnPropertyChanged();
        }
    }

    private EPlaybackState _playbackState = EPlaybackState.Stopped;

    public EPlaybackState PlaybackState
    {
        get => _playbackState;
        set
        {
            _playbackState = value;
            OnPropertyChanged();
        }
    }

    public TimeSpan DurationTotal
    {
        get
        {
            if (CurrentTrack == null)
                return new TimeSpan(0);

            return TimeSpan.FromSeconds(CurrentTrack.Track.Duration);
        }
    }

    public string DurationTotalStr
    {
        get => DurationTotal.ToString(@"mm\:ss");
    }

    public TimeSpan ListenDuration
    {
        get => TimeSpan.FromSeconds(_player.Position);
    }

    public void SetPosition(double position)
    {
        if (_player.CanSeek)
        {
            _updateTimer.Stop();

            _player.Position = position;

            _updateTimer.Start();
            OnPropertyChanged();
        }
    }

    public string ListenDurationStr => ListenDuration.ToString(@"mm\:ss");

    public int Progression
    {
        get
        {
            const double epsilon = 1e-6;
            if (Math.Abs(DurationTotal.TotalSeconds) < epsilon)
                return 0;

            int position = (int)(ListenDuration.TotalSeconds * 100 / DurationTotal.TotalSeconds);

            if (position > 100)
                position = 100;

            if (position < 0)
                position = 1;

            return position;
        }
    }

    public double Volume
    {
        get => _player.Volume;
        set
        {
            if (value < 0)
                value = 0;
            if (value > 100)
                value = 100;

            _player.Volume = (float)value;

            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => _player.IsMuted;
        set
        {
            _player.IsMuted = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Volume));
        }
    }

    private bool IsPlaying => _player.PlaybackState == EPlaybackState.Playing;

    private readonly HashSet<long> _artistUpdatedCache = [];

    private readonly HashSet<long> _albumUpdatedCache = [];

    private readonly HashSet<long> _trackUpdatedCache = [];

    #region Lyrics

    private LyricsModel? _lyrics = new();

    private int _lyricsCurrentIndex = 0;

    public bool LyricsExist { get; set; } = false;

    public SyncLyricsModel SyncLyrics { get; set; } = new();

    public LyricLine CurrentLyric { get; private set; } = new();

    public ObservableCollection<LyricLine> Lyrics => SyncLyrics.Lyrics;

    public bool IsSynchronizedLyrics => _lyrics?.LyricsType == ELyricsType.Synchronized;

    public string? PlainLyrics => _lyrics?.PlainLyrics;

    #endregion

    public RelayCommand FullscreenCommand { get; private set; }
    public RelayCommand MuteCommand { get; private set; }
    public RelayCommand SkipPreviousCommand { get; private set; }
    public AsyncRelayCommand SkipNextCommand { get; private set; }
    public RelayCommand OpenListeningCommand { get; private set; }
    public RelayCommand CompactModeCommand { get; private set; }
    public RelayCommand TogglePlayPauseCommand { get; private set; }


    public PlayerViewModel(IPlayerService player, NavigationService navigationService, IMediator mediator, ILyricsService lyricsService, ILogger<PlayerViewModel> logger)
    {
        _player = Guard.Against.Null(player);
        _navigationService = Guard.Against.Null(navigationService);
        _mediator = Guard.Against.Null(mediator);
        _lyricsService = Guard.Against.Null(lyricsService);
        _logger = Guard.Against.Null(logger);

        SkipPreviousCommand = new RelayCommand(SkipPrevious);
        SkipNextCommand = new AsyncRelayCommand(SkipNextAsync);
        FullscreenCommand = new RelayCommand(Fullscreen);
        MuteCommand = new RelayCommand(Mute);
        OpenListeningCommand = new RelayCommand(OpenListening);
        CompactModeCommand = new RelayCommand(ToggleCompactMode);
        TogglePlayPauseCommand = new RelayCommand(TogglePlayPause);

        Messenger.Subscribe<MediaChangedMessage>(async (message) => await OnMediaChangedAsync(message));
        Messenger.Subscribe<MediaStateChanged>((message) => OnMediaStateChanged(message));
        Messenger.Subscribe<MediaEndedEvent>((message) => OnMediaEnded(message));
        Messenger.Subscribe<MediaAboutToEndEvent>((message) => OnMediaAboutToEnd(message));
        Messenger.Subscribe<TrackScoreUpdateMessage>((message) => OnTrackScoreUpdated(message));
        Messenger.Subscribe<PlaylistChanged>((message) => OnPlaylistChanged(message));

        _updateTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        _backdropTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(45)
        };

        _backdropTimer.Tick += BackdropTimer_Tick;
        _backdropTimer.Start();

        _lyricTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(0.2)
        };

        _lyricTimer.Tick += LyricTimer_Tick;
        _lyricTimer.Start();
    }


    private void InitializeListenedCache()
    {
        _artistUpdatedCache.Clear();
        _albumUpdatedCache.Clear();
        _trackUpdatedCache.Clear();
    }


    #region Timers

    private void BackdropTimer_Tick(object? sender, object e)
    {
        if (CurrentTrack != null)
            OnPropertyChanged(nameof(CurrentArtist));
    }

    private void UpdateTimer_Tick(object? sender, object e)
    {
        if (IsPlaying)
        {
            OnPropertyChanged(nameof(Progression));
            OnPropertyChanged(nameof(DurationTotal));
            OnPropertyChanged(nameof(DurationTotalStr));
            OnPropertyChanged(nameof(ListenDuration));
            OnPropertyChanged(nameof(ListenDurationStr));
        }
    }


    private void LyricTimer_Tick(object? sender, object e)
    {
        if (CurrentTrack != null && IsPlaying)
        {
            SetLyricsTime(ListenDuration);
        }
    }

    #endregion


    #region Messages

    private void OnTrackScoreUpdated(TrackScoreUpdateMessage message)
    {
        _logger.LogDebug("Player VM handle track score updated");

        foreach (TrackDto track in _player.Playlist.Where(c => c.Id == message.TrackId))
            track.Score = message.Score;

        OnPropertyChanged(string.Empty);
    }

    private void OnMediaAboutToEnd(MediaAboutToEndEvent message)
    {
        _logger.LogDebug("Player VM handle media about to end: title {Title}", message.Track.Title);
    }

    private void OnMediaEnded(MediaEndedEvent message)
    {
        _logger.LogDebug("Player VM handle media end");

        _dispatcherQueue.TryEnqueue(() =>
        {
            message.Track.Listening = false;
        });
    }

    private async Task OnMediaChangedAsync(MediaChangedMessage message)
    {
        _logger.LogDebug("Player VM handle media changed: title {Title}.", message.NewTrack.Title);

        TrackViewModel trackViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
        trackViewModel.SetData(message.NewTrack);

        AlbumViewModel? albumViewModel = null;
        if (trackViewModel.Track.AlbumId.HasValue)
            albumViewModel = await GetAlbumFromIdAsync(trackViewModel.Track.AlbumId.Value);

        ArtistViewModel? artistViewModel = null;
        if (trackViewModel.Track.ArtistId.HasValue)
            artistViewModel = await GetArtistFromIdAsync(trackViewModel.Track.ArtistId.Value);

        _dispatcherQueue.TryEnqueue(async () =>
        {
            albumViewModel?.LoadPicture();

            TrackViewModel? previousTrack = CurrentTrack;

            CanSkipNext = _player.CanNext;
            CanSkipPrevious = _player.CanPrevious;

            if (previousTrack != null)
            {
                previousTrack.Listening = false;
                previousTrack.Listened = true;
            }

            if (message.NewTrack != null)
            {
                ArtistViewModel? previousArtist = _currentArtist;

                CurrentTrack = trackViewModel;
                CurrentAlbum = albumViewModel;
                CurrentArtist = artistViewModel;

                await UpdateListenCountAsync();
                CurrentTrack.Listening = true;

                ResetLyrics();
                await LoadLyricsAsync();

                if (CurrentArtist != null && CurrentArtist != previousArtist)
                    GetBackdrop();
            }
            else
            {
                _logger.LogDebug("Media changed to nothing.");

                CurrentTrack = null;
                CurrentArtist = null;
                CurrentAlbum = null;

                ResetLyrics();
            }
        });
    }

    private void OnMediaStateChanged(MediaStateChanged message)
    {
        _logger.LogDebug("Player VM handle media state changed: {State}.", message.State);

        _dispatcherQueue.TryEnqueue(() =>
        {
            PlaybackState = message.State;

            CanSkipNext = _player.CanNext;
            CanSkipPrevious = _player.CanPrevious;
        });
    }

    private void OnPlaylistChanged(PlaylistChanged message)
    {
        InitializeListenedCache();

        // TO COMPLETE
    }

    #endregion


    private async Task<AlbumViewModel?> GetAlbumFromIdAsync(long albumId)
    {
        Result<AlbumDto> albumResult = await _mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId));
        if (albumResult.IsError)
        {
            _logger.LogError("Failed to get album by ID {AlbumId}: {ErrorMessage}", albumId, albumResult.Error);
            return null;
        }

        AlbumViewModel albumViewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
        albumViewModel.SetData(albumResult.Value!);

        return albumViewModel;
    }


    private async Task<ArtistViewModel?> GetArtistFromIdAsync(long artistId)
    {
        Result<ArtistDto> artistResult = await _mediator.SendMessageAsync(new GetArtistByIdQuery(artistId));
        if (artistResult.IsError)
        {
            _logger.LogError("Failed to get artist by ID {ArtistId}: {ErrorMessage}", artistId, artistResult.Error);
            return null;
        }

        ArtistViewModel artistViewModel = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
        artistViewModel.SetData(artistResult.Value!);

        return artistViewModel;
    }


    private void GetBackdrop()
    {
        _backdropTimer.Stop();

        _currentArtist?.LoadBackdrop();

        _backdropTimer.Start();
    }


    private async Task UpdateListenCountAsync()
    {
        await UpdateTrackListenAsync();
        await UpdateAlbumListenAsync();
        await UpdateArtistListenAsync();
    }


    private async Task UpdateTrackListenAsync()
    {
        if (CurrentTrack == null)
            return;

        if (_trackUpdatedCache.TryGetValue(CurrentTrack.Track.Id, out _))
            return;

        await _mediator.SendMessageAsync(new UpdateTrackLastListenCommand(CurrentTrack.Track.Id));
        _trackUpdatedCache.Add(CurrentTrack.Track.Id);
    }


    private async Task UpdateArtistListenAsync()
    {
        if (CurrentArtist == null)
            return;

        if (_artistUpdatedCache.TryGetValue(CurrentArtist.Artist.Id, out _))
            return;

        await _mediator.SendMessageAsync(new UpdateArtistLastListenCommand(CurrentArtist.Artist.Id));
        _artistUpdatedCache.Add(CurrentArtist.Artist.Id);
    }


    private async Task UpdateAlbumListenAsync()
    {
        if (CurrentAlbum == null)
            return;

        if (_albumUpdatedCache.TryGetValue(CurrentAlbum.Album.Id, out _))
            return;

        await _mediator.SendMessageAsync(new UpdateAlbumLastListenCommand(CurrentAlbum.Album.Id));
        _albumUpdatedCache.Add(CurrentAlbum.Album.Id);
    }


    private async Task LoadLyricsAsync()
    {
        if (CurrentTrack != null)
        {
            LyricsExist = _lyricsService.CheckLyricsFileExists(CurrentTrack.Track.MusicFile) != ELyricsType.None;

            if (LyricsExist)
            {
                _lyrics = await _lyricsService.LoadLyricsAsync(CurrentTrack.Track.MusicFile);
                if (_lyrics != null && _lyrics.LyricsType == ELyricsType.Synchronized && !string.IsNullOrEmpty(_lyrics.SynchronizedLyrics))
                {
                    LyricsParser parser = new();
                    SyncLyricsModel syncLyrics = parser.Parse(_lyrics.SynchronizedLyrics);
                    SyncLyrics = syncLyrics;
                }
            }

            OnPropertyChanged(nameof(Lyrics));
            OnPropertyChanged(nameof(LyricsExist));
            OnPropertyChanged(nameof(IsSynchronizedLyrics));
            OnPropertyChanged(nameof(PlainLyrics));
        }
    }


    public void SetLyricsTime(TimeSpan time)
    {
        if (SyncLyrics == null)
            return;

        int start = _lyricsCurrentIndex + 1;

        // Search current lyric
        for (int i = start; i < SyncLyrics.Time.Count; i++)
        {
            if (SyncLyrics.Time[i] > time)
            {
                _lyricsCurrentIndex = i - 1;
                break;
            }
        }

        if (_lyricsCurrentIndex < 0 || _lyricsCurrentIndex >= Lyrics.Count)
            CurrentLyric = new();
        else
            CurrentLyric = Lyrics[_lyricsCurrentIndex];

        OnPropertyChanged(nameof(CurrentLyric));
    }


    private void ResetLyrics()
    {
        _lyricsCurrentIndex = -1;
        CurrentLyric = new();

        OnPropertyChanged(nameof(CurrentLyric));
    }


    public void TogglePlayPause()
    {
        if (_player.PlaybackState == EPlaybackState.Paused)
            _player.Play();
        else if (_player.PlaybackState == EPlaybackState.Playing)
            _player.Pause();
    }

    public async Task SkipNextAsync()
    {
        if (!CanSkipNext)
            return;

        if (CurrentTrack == null)
            return;

        CanSkipNext = false;

        await _mediator.SendMessageAsync(new UpdateSkipCountCommand { TrackId = CurrentTrack.Track.Id });
        _player.Skip();
    }

    public void SkipPrevious()
    {
        if (!CanSkipPrevious)
            return;

        _player.Previous();

        CanSkipPrevious = false;
    }

    private void OpenListening()
    {
        _navigationService.NavigateToListening();

        if (_isFullScreen)
            DisableFullScreen();
    }


    private void Fullscreen()
    {
        if (_isFullScreen)
            DisableFullScreen();
        else
            EnableFullScreen();
    }

    private void EnableFullScreen()
    {
        _isFullScreen = true;
        Messenger.Send(new FullScreenMessage(_isFullScreen));
    }


    private void DisableFullScreen()
    {
        _isFullScreen = false;
        Messenger.Send(new FullScreenMessage(_isFullScreen));
    }


    private void ToggleCompactMode()
    {
        Messenger.Send(new CompactModeMessage());
    }


    private void Mute()
    {
        bool isMuted = IsMuted;
        IsMuted = !isMuted;
    }
}