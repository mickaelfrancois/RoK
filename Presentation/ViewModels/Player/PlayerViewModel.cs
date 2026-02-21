using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Tracks.Command;
using Rok.Application.Player;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Player.Services;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Player;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IPlayerService _player;
    private readonly IMediator _mediator;
    private readonly NavigationService _navigationService;
    private readonly ILogger<PlayerViewModel> _logger;
    private bool _disposed;

    private readonly PlayerDataLoader _dataLoader;
    private readonly PlayerLyricsService _lyricsService;
    private readonly PlayerListenTracker _listenTracker;
    private readonly PlayerTimerManager _timerManager;
    private readonly PlayerStateManager _stateManager;

    private bool _isFullScreen;

    public TrackViewModel? CurrentTrack => _stateManager.CurrentTrack;
    public ArtistViewModel? CurrentArtist => _stateManager.CurrentArtist;
    public AlbumViewModel? CurrentAlbum => _stateManager.CurrentAlbum;
    public bool CanSkipNext
    {
        get => _stateManager.CanSkipNext;
        set => _stateManager.CanSkipNext = value;
    }
    public bool CanSkipPrevious
    {
        get => _stateManager.CanSkipPrevious;
        set => _stateManager.CanSkipPrevious = value;
    }
    public EPlaybackState PlaybackState
    {
        get => _stateManager.PlaybackState;
        set => _stateManager.PlaybackState = value;
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

    public TimeSpan DurationTotal
    {
        get
        {
            if (CurrentTrack == null)
                return new TimeSpan(0);

            return TimeSpan.FromSeconds(CurrentTrack.Track.Duration);
        }
    }

    public string DurationTotalStr => DurationTotal.ToString(@"mm\:ss");

    public TimeSpan ListenDuration => TimeSpan.FromSeconds(_player.Position);

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

    // Lyrics
    public bool LyricsExist => _stateManager.LyricsExist;
    public ObservableCollection<LyricLine> LyricsLines => _stateManager.LyricsLines;
    public LyricLine CurrentLyric => _stateManager.CurrentLyric;
    public string PreviousLyric => _stateManager.PreviousLyric;
    public string NextLyric => _stateManager.NextLyric;
    public bool IsSynchronizedLyrics => _stateManager.IsSynchronizedLyrics;
    public string? PlainLyrics => _stateManager.PlainLyrics;


    public PlayerViewModel(
        IPlayerService player,
        NavigationService navigationService,
        IMediator mediator,
        PlayerDataLoader dataLoader,
        PlayerLyricsService lyricsService,
        PlayerListenTracker listenTracker,
        PlayerTimerManager timerManager,
        PlayerStateManager stateManager,
        ILogger<PlayerViewModel> logger)
    {
        _player = Guard.Against.Null(player);
        _navigationService = Guard.Against.Null(navigationService);
        _mediator = Guard.Against.Null(mediator);
        _dataLoader = Guard.Against.Null(dataLoader);
        _lyricsService = Guard.Against.Null(lyricsService);
        _listenTracker = Guard.Against.Null(listenTracker);
        _timerManager = Guard.Against.Null(timerManager);
        _stateManager = Guard.Against.Null(stateManager);
        _logger = Guard.Against.Null(logger);

        _stateManager.PropertyChanged += OnStateManagerPropertyChanged;

        SubscribeToMessages();
        SubscribeToTimers();

        _timerManager.Start();
    }


    private void OnStateManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        OnPropertyChanged(e.PropertyName);
    }


    private void SubscribeToMessages()
    {
        Messenger.Subscribe<MediaChangedMessage>(async (message) => await OnMediaChangedAsync(message));
        Messenger.Subscribe<MediaStateChanged>(OnMediaStateChanged);
        Messenger.Subscribe<MediaEndedEvent>(OnMediaEnded);
        Messenger.Subscribe<MediaAboutToEndEvent>(OnMediaAboutToEnd);
        Messenger.Subscribe<TrackScoreUpdateMessage>(OnTrackScoreUpdated);
        Messenger.Subscribe<PlaylistChanged>(OnPlaylistChanged);
    }

    private void SubscribeToTimers()
    {
        _timerManager.UpdateTick += OnUpdateTimerTick;
        _timerManager.LyricTick += OnLyricTimerTick;
        _timerManager.BackdropTick += OnBackdropTimerTick;
    }

    public void SetPosition(double position)
    {
        if (_player.CanSeek)
        {
            _player.Position = position;
            OnPropertyChanged();
        }
    }

    #region Timer Events

    private void OnBackdropTimerTick(object? sender, EventArgs e)
    {
        if (CurrentTrack != null)
            OnPropertyChanged(nameof(CurrentArtist));
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
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

    private void OnLyricTimerTick(object? sender, EventArgs e)
    {
        if (CurrentTrack != null && IsPlaying)
        {
            _stateManager.UpdateLyricsTime(ListenDuration);
        }
    }

    #endregion

    #region Message Handlers

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

        _stateManager.ExecuteOnUIThread(() =>
        {
            message.Track.Listening = false;
        });
    }

    private async Task OnMediaChangedAsync(MediaChangedMessage message)
    {
        _logger.LogDebug("Player VM handle media changed: title {Title}.", message.NewTrack.Title);

        TrackViewModel trackViewModel = _dataLoader.CreateTrackViewModel(message.NewTrack);

        AlbumViewModel? albumViewModel = null;
        if (trackViewModel.Track.AlbumId.HasValue)
            albumViewModel = await _dataLoader.GetAlbumByIdAsync(trackViewModel.Track.AlbumId.Value);

        ArtistViewModel? artistViewModel = null;
        if (trackViewModel.Track.ArtistId.HasValue)
            artistViewModel = await _dataLoader.GetArtistByIdAsync(trackViewModel.Track.ArtistId.Value);

        _stateManager.ExecuteOnUIThread(async () =>
        {
            albumViewModel?.LoadPicture();

            TrackViewModel? previousTrack = CurrentTrack;
            ArtistViewModel? previousArtist = CurrentArtist;

            CanSkipNext = _player.CanNext;
            CanSkipPrevious = _player.CanPrevious;

            if (previousTrack != null)
            {
                previousTrack.Listening = false;
                previousTrack.Listened = true;
            }

            if (message.NewTrack != null)
            {
                _stateManager.CurrentTrack = trackViewModel;
                _stateManager.CurrentAlbum = albumViewModel;
                _stateManager.CurrentArtist = artistViewModel;

                await UpdateListenCountAsync();
                trackViewModel.Listening = true;

                _stateManager.ResetLyrics();
                await LoadLyricsAsync();

                if (CurrentArtist != null && CurrentArtist != previousArtist)
                    LoadBackdrop();
            }
            else
            {
                _logger.LogDebug("Media changed to nothing.");

                _stateManager.CurrentTrack = null;
                _stateManager.CurrentArtist = null;
                _stateManager.CurrentAlbum = null;

                _stateManager.ResetLyrics();
            }

            OnPropertyChanged(string.Empty);
        });
    }

    private void OnMediaStateChanged(MediaStateChanged message)
    {
        _logger.LogDebug("Player VM handle media state changed: {State}.", message.State);

        _stateManager.ExecuteOnUIThread(() =>
        {
            PlaybackState = message.State;
            CanSkipNext = _player.CanNext;
            CanSkipPrevious = _player.CanPrevious;
        });
    }

    private void OnPlaylistChanged(PlaylistChanged message)
    {
        _listenTracker.ClearCache();
    }

    #endregion

    private void LoadBackdrop()
    {
        _timerManager.StopBackdropTimer();
        CurrentArtist?.LoadBackdrop();
        _timerManager.StartBackdropTimer();
    }

    private async Task UpdateListenCountAsync()
    {
        if (CurrentTrack != null)
        {
            await _listenTracker.UpdateTrackListenAsync(CurrentTrack.Track.Id);

            if (CurrentTrack.Track.GenreId.HasValue)
                await _listenTracker.UpdateGenreListenAsync(CurrentTrack.Track.GenreId.Value);
        }

        if (CurrentAlbum != null)
            await _listenTracker.UpdateAlbumListenAsync(CurrentAlbum.Album.Id);

        if (CurrentArtist != null)
            await _listenTracker.UpdateArtistListenAsync(CurrentArtist.Artist.Id);
    }

    private async Task LoadLyricsAsync()
    {
        if (CurrentTrack == null)
            return;

        _stateManager.LyricsExist = _lyricsService.CheckLyricsExists(CurrentTrack.Track.MusicFile);

        if (_stateManager.LyricsExist)
        {
            LyricsModel? lyrics = await _lyricsService.LoadLyricsAsync(CurrentTrack.Track.MusicFile);
            _stateManager.SetLyrics(lyrics);

            if (lyrics != null && lyrics.LyricsType == ELyricsType.Synchronized && !string.IsNullOrEmpty(lyrics.SynchronizedLyrics))
            {
                SyncLyricsModel syncLyrics = _lyricsService.ParseSynchronizedLyrics(lyrics.SynchronizedLyrics);
                _stateManager.SetSyncLyrics(syncLyrics);
            }
        }

        OnPropertyChanged(nameof(LyricsLines));
        OnPropertyChanged(nameof(LyricsExist));
        OnPropertyChanged(nameof(IsSynchronizedLyrics));
        OnPropertyChanged(nameof(PlainLyrics));
    }



    [RelayCommand]
    public void TogglePlayPause()
    {
        if (_player.PlaybackState == EPlaybackState.Paused)
            _player.Play();
        else if (_player.PlaybackState == EPlaybackState.Playing)
            _player.Pause();
    }

    [RelayCommand]
    public async Task SkipNextAsync()
    {
        if (!CanSkipNext || CurrentTrack == null)
            return;

        CanSkipNext = false;

        await _mediator.SendMessageAsync(new UpdateSkipCountCommand { TrackId = CurrentTrack.Track.Id });
        _player.Skip();
    }

    [RelayCommand]
    public void SkipPrevious()
    {
        if (!CanSkipPrevious)
            return;

        _player.Previous();
        CanSkipPrevious = false;
    }

    [RelayCommand]
    private void OpenListening()
    {
        _navigationService.NavigateToListening();

        if (_isFullScreen)
            DisableFullScreen();
    }

    [RelayCommand]
    private void Fullscreen()
    {
        if (_isFullScreen)
            DisableFullScreen();
        else
            EnableFullScreen();
    }

    [RelayCommand]
    private void EnableFullScreen()
    {
        _isFullScreen = true;
        Messenger.Send(new FullScreenMessage(_isFullScreen));
    }

    [RelayCommand]
    private void DisableFullScreen()
    {
        _isFullScreen = false;
        Messenger.Send(new FullScreenMessage(_isFullScreen));
    }

    [RelayCommand]
    private static void CompactMode()
    {
        Messenger.Send(new CompactModeMessage());
    }

    [RelayCommand]
    private void Mute()
    {
        IsMuted = !IsMuted;
    }



    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_stateManager != null)
                _stateManager.PropertyChanged -= OnStateManagerPropertyChanged;

            _timerManager?.Dispose();
        }

        _disposed = true;
    }
}