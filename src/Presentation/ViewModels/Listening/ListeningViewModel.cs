using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Player;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Listening.Services;
using Rok.ViewModels.Player.Services;
using Rok.ViewModels.Track;
using ResourceLoader = Windows.ApplicationModel.Resources.ResourceLoader;

namespace Rok.ViewModels.Listening;

public sealed partial class ListeningViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ListeningViewModel> _logger;
    private readonly IPlayerService _playerService;
    private readonly ResourceLoader _resourceLoader;
    private readonly ListeningPlaylistManager _playlistManager;
    private readonly ListeningPlaybackService _playbackService;
    private readonly NavigationService _navigationService;
    private readonly IPlayerSleepModeService _playerSleepModeService;
    private readonly PlayerStateManager _stateManager;
    private readonly IMessenger _messenger;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public int TrackCount => _playlistManager.TrackCount;
    public long Duration => _playlistManager.Duration;
    public ArtistViewModel? Artist => _playlistManager.Artist;
    public RangeObservableCollection<TrackViewModel> Tracks => _playlistManager.Tracks;
    public TrackViewModel? CurrentTrack => _playlistManager.CurrentTrack;
    public bool IsSleepModeActive => _playerSleepModeService.IsSleepTimerActive;
    public int RemainingSleepTime => _playerSleepModeService.GetRemainingSleepTimeInSeconds();

    /// <summary>
    /// Asks the view to confirm a multi-track removal from the queue, passing the number of tracks that would be removed.
    /// Set by the page so the confirmation dialog stays out of the view model. Returns true when the user confirms.
    /// </summary>
    public Func<int, Task<bool>>? RemovalConfirmationRequested { get; set; }

    public ListeningViewModel(
        IPlayerService playerService,
        ListeningPlaylistManager playlistManager,
        ListeningPlaybackService playbackService,
        NavigationService navigationService,
        PlayerStateManager stateManager,
        IPlayerSleepModeService playerSleepModeService,
        IMessenger messenger,
        ResourceLoader resourceLoader,
        ILogger<ListeningViewModel> logger)
    {
        _playerService = Guard.NotNull(playerService);
        _playlistManager = Guard.NotNull(playlistManager);
        _playbackService = Guard.NotNull(playbackService);
        _playerSleepModeService = Guard.NotNull(playerSleepModeService);
        _stateManager = Guard.NotNull(stateManager);
        _navigationService = Guard.NotNull(navigationService);
        _messenger = Guard.NotNull(messenger);
        _resourceLoader = Guard.NotNull(resourceLoader);
        _logger = Guard.NotNull(logger);

        SubscribeToMessages();
        SubscribeToEvents();

        InitializeFromPlayerService();
    }


    private void SubscribeToMessages()
    {
        _subscriptions.Add(_messenger.Subscribe<MediaChangedMessage>(async (message) => await MediaChangedAsync(message)));
        _subscriptions.Add(_messenger.Subscribe<PlaylistChanged>(async (message) => await PlaylistChangedAsync(message)));
    }

    private void SubscribeToEvents()
    {
        _playlistManager.PlaylistChanged += OnPlaylistChanged;
        _playlistManager.CurrentTrackChanged += OnCurrentTrackChanged;
        _playerSleepModeService.SleepTimerStateChanged += OnSleepTimerStateChanged;
    }

    public void RefreshSleepTime() => OnPropertyChanged(nameof(RemainingSleepTime));

    private void InitializeFromPlayerService()
    {
        if (_playerService.Playlist != null)
        {
            _playlistManager.LoadTracksList(_playerService.Playlist);
            _ = _playlistManager.SetCurrentTrackAsync(_playerService.CurrentTrack);
        }
    }

    private void OnSleepTimerStateChanged(object? sender, bool isActive)
    {
        _stateManager.ExecuteOnUIThread(() => OnPropertyChanged(nameof(IsSleepModeActive)));
    }

    private void OnPlaylistChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(Duration));
    }

    private void OnCurrentTrackChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(Artist));
    }

    private Task MediaChangedAsync(MediaChangedMessage message)
    {
        _logger.LogDebug("Listening VM handle media changed, title {Message}.", message.NewTrack.Title);
        return _playlistManager.SetCurrentTrackAsync(message.NewTrack);
    }

    private Task PlaylistChangedAsync(PlaylistChanged message)
    {
        _logger.LogDebug("Listening VM handle playlist changed.");
        _playlistManager.LoadTracksList(message.Tracks);
        return _playlistManager.SetCurrentTrackAsync(_playerService.CurrentTrack);
    }

    [RelayCommand]
    private Task AddMoreFromArtistAsync(TrackViewModel track)
    {
        IEnumerable<long> currentTrackIds = Tracks.Select(t => t.Track.Id);
        return _playbackService.AddMoreFromArtistAsync(track, currentTrackIds);
    }

    [RelayCommand]
    private Task RemoveTrackFromQueueAsync(TrackViewModel track)
        => RemoveFromQueueAsync(
            _playerService.CountUpcomingByTrack(track.Track.Id),
            () => _playerService.RemoveUpcomingByTrack(track.Track.Id));

    [RelayCommand]
    private Task RemoveAlbumFromQueueAsync(TrackViewModel track)
    {
        if (!track.Track.AlbumId.HasValue)
            return Task.CompletedTask;

        long albumId = track.Track.AlbumId.Value;

        return RemoveFromQueueAsync(
            _playerService.CountUpcomingByAlbum(albumId),
            () => _playerService.RemoveUpcomingByAlbum(albumId));
    }

    [RelayCommand]
    private Task RemoveArtistFromQueueAsync(TrackViewModel track)
    {
        if (!track.Track.ArtistId.HasValue)
            return Task.CompletedTask;

        long artistId = track.Track.ArtistId.Value;

        return RemoveFromQueueAsync(
            _playerService.CountUpcomingByArtist(artistId),
            () => _playerService.RemoveUpcomingByArtist(artistId));
    }

    [RelayCommand]
    private Task RemoveGenreFromQueueAsync(TrackViewModel track)
    {
        if (!track.Track.GenreId.HasValue)
            return Task.CompletedTask;

        long genreId = track.Track.GenreId.Value;

        return RemoveFromQueueAsync(
            _playerService.CountUpcomingByGenre(genreId),
            () => _playerService.RemoveUpcomingByGenre(genreId));
    }

    private async Task RemoveFromQueueAsync(int upcomingCount, Func<int> remove)
    {
        if (upcomingCount <= 0)
            return;

        if (upcomingCount >= 2 && RemovalConfirmationRequested != null)
        {
            bool confirmed = await RemovalConfirmationRequested(upcomingCount);

            if (!confirmed)
                return;
        }

        remove();
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
    private void ShufflePlaylist()
    {
        _playbackService.ShuffleTracks();
    }

    [RelayCommand]
    private void ArtistOpen()
    {
        long? artistId = CurrentTrack?.Track.ArtistId;
        if (!artistId.HasValue)
            return;

        _navigationService.NavigateToArtist(artistId.Value);
    }

    [RelayCommand]
    private void AlbumOpen()
    {
        long? albumId = CurrentTrack?.Track.AlbumId;
        if (!albumId.HasValue)
            return;

        _navigationService.NavigateToAlbum(albumId.Value);
    }

    [RelayCommand]
    private void TrackOpen()
    {
        long? trackId = CurrentTrack?.Track.Id;
        if (!trackId.HasValue)
            return;

        _navigationService.NavigateToTrack(trackId.Value);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
        _disposed = true;
    }
}