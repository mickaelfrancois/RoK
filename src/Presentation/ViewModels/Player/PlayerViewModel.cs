using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Services;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Player.Services;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Player;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IPlayerService _player;
    private readonly ResourceLoader _resourceLoader;
    private readonly IMediator _mediator;
    private readonly NavigationService _navigationService;
    private readonly IPlayerSleepModeService _playerSleepModeService;
    private readonly ILogger<PlayerViewModel> _logger;
    private readonly ITelemetryClient _telemetryClient;
    private bool _disposed;

    private readonly PlayerDataLoader _dataLoader;
    private readonly PlayerLyricsService _lyricsService;
    private readonly PlayerListenTracker _listenTracker;
    private readonly PlayerTimerManager _timerManager;
    private readonly PlayerStateManager _stateManager;
    private readonly IMessenger _messenger;
    private readonly List<IDisposable> _subscriptions = new();

    private bool _isFullScreen;
    private readonly IEqualizerWindowService _equalizerWindowService;

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

    [ObservableProperty]
    public partial EPlaybackMode Mode { get; set; }

    [ObservableProperty]
    public partial string? CurrentStationName { get; set; }

    [ObservableProperty]
    public partial string? CurrentStreamTitle { get; set; }

    [ObservableProperty]
    public partial bool IsBuffering { get; set; }

    public bool IsMusicMode => Mode == EPlaybackMode.Music;
    public bool IsRadioMode => Mode == EPlaybackMode.Radio;

    public bool IsSleepModeActive => _playerSleepModeService.IsSleepTimerActive;
    public int RemainingSleepTime => _playerSleepModeService.GetRemainingSleepTimeInSeconds();

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

    public EqualizerViewModel EqualizerViewModel { get; }

    // Lyrics
    public bool LyricsExist => _stateManager.LyricsExist;
    public ObservableCollection<LyricLine> LyricsLines => _stateManager.LyricsLines;
    public LyricLine CurrentLyric => _stateManager.CurrentLyric;
    public string PreviousLyric => _stateManager.PreviousLyric;
    public string NextLyric => _stateManager.NextLyric;
    public bool IsSynchronizedLyrics => _stateManager.IsSynchronizedLyrics;
    public string? PlainLyrics => _stateManager.PlainLyrics;
    public int CurrentLyricIndex => _stateManager.CurrentLyricIndex;

    public PlayerViewModel(
        IPlayerService player,
        NavigationService navigationService,
        IMediator mediator,
        IMessenger messenger,
        PlayerDataLoader dataLoader,
        PlayerLyricsService lyricsService,
        PlayerListenTracker listenTracker,
        PlayerTimerManager timerManager,
        PlayerStateManager stateManager,
        EqualizerViewModel equalizerViewModel,
        IEqualizerWindowService equalizerWindowService,
        ResourceLoader resourceLoader,
        IPlayerSleepModeService playerSleepModeService,
        ITelemetryClient telemetryClient,
        ILogger<PlayerViewModel> logger)
    {
        _player = Guard.NotNull(player);
        _navigationService = Guard.NotNull(navigationService);
        _mediator = Guard.NotNull(mediator);
        _messenger = Guard.NotNull(messenger);
        _dataLoader = Guard.NotNull(dataLoader);
        _lyricsService = Guard.NotNull(lyricsService);
        _listenTracker = Guard.NotNull(listenTracker);
        _timerManager = Guard.NotNull(timerManager);
        _stateManager = Guard.NotNull(stateManager);
        EqualizerViewModel = Guard.NotNull(equalizerViewModel);
        _equalizerWindowService = Guard.NotNull(equalizerWindowService);
        _resourceLoader = Guard.NotNull(resourceLoader);
        _playerSleepModeService = Guard.NotNull(playerSleepModeService);
        _telemetryClient = Guard.NotNull(telemetryClient);
        _logger = Guard.NotNull(logger);

        _stateManager.PropertyChanged += OnStateManagerPropertyChanged;
        _playerSleepModeService.SleepTimerStateChanged += OnSleepTimerStateChanged;

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

    private void OnSleepTimerStateChanged(object? sender, bool isActive)
    {
        _stateManager.ExecuteOnUIThread(() => OnPropertyChanged(nameof(IsSleepModeActive)));
    }

    private void SubscribeToMessages()
    {
        _subscriptions.Add(_messenger.Subscribe<MediaChangedMessage>(async (message) => await OnMediaChangedAsync(message)));
        _subscriptions.Add(_messenger.Subscribe<MediaStateChanged>(OnMediaStateChanged));
        _subscriptions.Add(_messenger.Subscribe<MediaEndedEvent>(OnMediaEnded));
        _subscriptions.Add(_messenger.Subscribe<MediaAboutToEndEvent>(OnMediaAboutToEnd));
        _subscriptions.Add(_messenger.Subscribe<TrackScoreUpdateMessage>(OnTrackScoreUpdated));
        _subscriptions.Add(_messenger.Subscribe<PlaylistChanged>(OnPlaylistChanged));
        _subscriptions.Add(_messenger.Subscribe<RadioStationChanged>(OnRadioStationChanged));
        _subscriptions.Add(_messenger.Subscribe<RadioMetadataChanged>(OnRadioMetadataChanged));
        _subscriptions.Add(_messenger.Subscribe<BufferingChanged>(OnBufferingChanged));
        _subscriptions.Add(_messenger.Subscribe<CompactModeMessage>(_ => OnCompactModeToggled()));
    }

    private void OnCompactModeToggled()
    {
        // The compact-mode toggle Collapses/Restores MainGrid which contains
        // the PlayerView. WinUI x:Bind bindings that target null-propagating
        // paths (e.g. ViewModel.CurrentTrack.Title) sometimes fall through to
        // the parent DataContext after the visibility flip, displaying the
        // ViewModel's ToString() instead of the underlying value. Re-raising
        // the mode-driven flags forces every dependent binding to refresh.
        _stateManager.ExecuteOnUIThread(() =>
        {
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(IsMusicMode));
            OnPropertyChanged(nameof(IsRadioMode));
            OnPropertyChanged(nameof(CurrentStationName));
            OnPropertyChanged(nameof(CurrentStreamTitle));
            OnPropertyChanged(nameof(CurrentTrack));
        });
    }

    partial void OnModeChanged(EPlaybackMode value)
    {
        OnPropertyChanged(nameof(IsMusicMode));
        OnPropertyChanged(nameof(IsRadioMode));
    }

    private void OnRadioStationChanged(RadioStationChanged message)
    {
        _stateManager.ExecuteOnUIThread(() =>
        {
            CurrentStationName = message.Station.Name;
            CurrentStreamTitle = null;
            Mode = EPlaybackMode.Radio;
            CanSkipNext = false;
            CanSkipPrevious = false;
        });
    }

    private void OnRadioMetadataChanged(RadioMetadataChanged message)
    {
        _stateManager.ExecuteOnUIThread(() =>
        {
            CurrentStreamTitle = message.StreamTitle;
        });
    }

    private void OnBufferingChanged(BufferingChanged message)
    {
        _stateManager.ExecuteOnUIThread(() =>
        {
            IsBuffering = message.IsBuffering;
        });
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

    private void OnBackdropTimerTick(object? sender, EventArgs e)
    {
        if (CurrentTrack != null)
            OnPropertyChanged(nameof(CurrentArtist));
    }

    public void RefreshSleepTime() => OnPropertyChanged(nameof(RemainingSleepTime));

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

        await _telemetryClient.CaptureEventAsync("Player", "TrackChanged");

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

                long duration = message.DurationPlayed ?? 0;
                await _listenTracker.UpdateListeningEventsAsync(previousTrack.Track.Id,
                   previousTrack.Track.ArtistId,
                    previousTrack.Track.AlbumId,
                    previousTrack.Track.GenreId,
                    duration,
                    previousTrack.Track.Duration);
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

                await EqualizerViewModel.ApplyPresetAsync(message.NewTrack);

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

            if (_player.Mode != EPlaybackMode.Radio)
                Mode = _player.Mode;
        });
    }

    private void OnPlaylistChanged(PlaylistChanged message)
    {
        _listenTracker.ClearCache();
    }

    private void LoadBackdrop()
    {
        _timerManager.StopBackdropTimer();
        CurrentArtist?.LoadBackdrop();
        _timerManager.StartBackdropTimer();
    }

    private async Task UpdateListenCountAsync()
    {
        if (CurrentTrack == null)
            return;

        await _listenTracker.UpdateTrackListenAsync(CurrentTrack.Track.Id);

        if (CurrentTrack.Track.GenreId.HasValue)
            await _listenTracker.UpdateGenreListenAsync(CurrentTrack.Track.GenreId.Value);

        if (CurrentTrack.Track.AlbumId.HasValue)
            await _listenTracker.UpdateAlbumListenAsync(CurrentTrack.Track.AlbumId.Value);

        if (CurrentTrack.Track.ArtistId.HasValue)
            await _listenTracker.UpdateArtistListenAsync(CurrentTrack.Track.ArtistId.Value);
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
    private void StopPlayback()
    {
        _player.Stop(true);
        Mode = EPlaybackMode.None;
    }

    [RelayCommand]
    public void SetSleepTimer(int minutes)
    {
        _playerSleepModeService.StartSleepTimer(minutes);
        _messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_sleepTimer_Start")!, Type = NotificationType.Informational });
    }

    [RelayCommand]
    public void StopSleepTimer()
    {
        _playerSleepModeService.StopSleepTimer();

        _messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_sleepTimer_Stop")!, Type = NotificationType.Informational });
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

        await _mediator.Send(new UpdateSkipCountRequest { TrackId = CurrentTrack.Track.Id });
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
        _messenger.Send(new FullScreenMessage(_isFullScreen));
    }

    [RelayCommand]
    private void DisableFullScreen()
    {
        _isFullScreen = false;
        _messenger.Send(new FullScreenMessage(_isFullScreen));
    }

    [RelayCommand]
    private void CompactMode()
    {
        _messenger.Send(new CompactModeMessage());
    }

    [RelayCommand]
    private void Mute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void ToggleEqualizer() => _equalizerWindowService.ShowOrActivate();

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
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();

            if (_stateManager != null)
                _stateManager.PropertyChanged -= OnStateManagerPropertyChanged;

            _playerSleepModeService.SleepTimerStateChanged -= OnSleepTimerStateChanged;

            _timerManager?.Dispose();
            _equalizerWindowService.Close();
        }

        _disposed = true;
    }
}