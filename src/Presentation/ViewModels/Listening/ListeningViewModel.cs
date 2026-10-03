using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Player;
using Rok.ViewModels.Album;
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
    private readonly ListeningSleepTimerTracker _sleepTimerTracker;
    private readonly TimeProvider _timeProvider;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public ArtistViewModel? Artist => _playlistManager.Artist;
    public AlbumViewModel? Album => _playlistManager.Album;
    public RangeObservableCollection<TrackViewModel> Tracks => _playlistManager.Tracks;
    public TrackViewModel? CurrentTrack => _playlistManager.CurrentTrack;
    public bool IsSleepModeActive => _playerSleepModeService.IsSleepTimerActive;
    public TrackDto? HeaderTrack { get; private set; }
    public ListeningHeaderState HeaderState { get; private set; } = ListeningHeaderState.From(null, null, null);
    public string QueueSummaryText { get; private set; } = string.Empty;
    public string SleepButtonLabel { get; private set; } = string.Empty;
    public string ArtistFavoriteLabel { get; private set; } = string.Empty;
    public string AlbumFavoriteLabel { get; private set; } = string.Empty;
    public string AlbumYear => Album?.ReleaseDateYear ?? string.Empty;

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
        ListeningSleepTimerTracker sleepTimerTracker,
        TimeProvider timeProvider,
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
        _sleepTimerTracker = Guard.NotNull(sleepTimerTracker);
        _timeProvider = Guard.NotNull(timeProvider);
        _resourceLoader = Guard.NotNull(resourceLoader);
        _logger = Guard.NotNull(logger);

        SubscribeToMessages();
        SubscribeToEvents();

        InitializeFromPlayerService();
        RecomputeHeader();
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
        _sleepTimerTracker.RemainingMinutesChanged += OnRemainingMinutesChanged;
    }

    public void RefreshSleepTime() => UpdateSleepButtonLabel();

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
        _stateManager.ExecuteOnUIThread(() =>
        {
            OnPropertyChanged(nameof(IsSleepModeActive));
            UpdateSleepButtonLabel();
        });
    }

    private void OnRemainingMinutesChanged(object? sender, int minutes)
    {
        _stateManager.ExecuteOnUIThread(UpdateSleepButtonLabel);
    }

    private void OnPlaylistChanged(object? sender, EventArgs e)
    {
        _stateManager.ExecuteOnUIThread(RecomputeHeader);
    }

    private void OnCurrentTrackChanged(object? sender, EventArgs e)
    {
        _stateManager.ExecuteOnUIThread(() =>
        {
            OnPropertyChanged(nameof(CurrentTrack));
            OnPropertyChanged(nameof(Artist));
            OnPropertyChanged(nameof(Album));
            RecomputeHeader();
        });
    }

    private void RecomputeHeader()
    {
        TrackDto? track = _playlistManager.DisplayedTrack;

        HeaderTrack = track;
        HeaderState = ListeningHeaderState.From(track, Artist?.Artist, Album?.Album);

        UpdateQueueSummary();
        UpdateSleepButtonLabel();
        UpdateFavoriteLabels();

        OnPropertyChanged(nameof(HeaderTrack));
        OnPropertyChanged(nameof(HeaderState));
        OnPropertyChanged(nameof(AlbumYear));
    }

    private void UpdateQueueSummary()
    {
        List<long> durations = Tracks.Select(t => t.Track.Duration).ToList();
        int currentIndex = CurrentTrack is null ? -1 : Tracks.IndexOf(CurrentTrack);

        ListeningQueueSummary summary = ListeningQueueSummary.Compute(
            durations,
            currentIndex,
            _playerService.Position,
            _timeProvider.GetLocalNow());

        ListeningSummaryLabels labels = new(
            _resourceLoader.GetString("listeningSummaryTrack"),
            _resourceLoader.GetString("listeningSummaryTracks"),
            _resourceLoader.GetString("listeningSummaryHoursMinutes"),
            _resourceLoader.GetString("listeningSummaryHours"),
            _resourceLoader.GetString("listeningSummaryMinutes"),
            _resourceLoader.GetString("listeningSummaryEndsAt"));

        QueueSummaryText = ListeningQueueSummaryFormatter.Format(summary, labels, CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(QueueSummaryText));
    }

    private void UpdateSleepButtonLabel()
    {
        SleepButtonLabel = ListeningHeaderLabels.SleepButton(
            IsSleepModeActive,
            _playerSleepModeService.GetRemainingSleepTimeInSeconds(),
            _resourceLoader.GetString("listeningSleepIdle"),
            _resourceLoader.GetString("listeningSleepRemaining"));
        OnPropertyChanged(nameof(SleepButtonLabel));
    }

    private void UpdateFavoriteLabels()
    {
        string addFormat = _resourceLoader.GetString("listeningFavoriteAdd");
        string removeFormat = _resourceLoader.GetString("listeningFavoriteRemove");

        ArtistFavoriteLabel = Artist is null
            ? string.Empty
            : ListeningHeaderLabels.FavoriteToggle(Artist.Artist.Name, Artist.IsFavorite, addFormat, removeFormat);
        AlbumFavoriteLabel = Album is null
            ? string.Empty
            : ListeningHeaderLabels.FavoriteToggle(Album.Album.Name, Album.IsFavorite, addFormat, removeFormat);

        OnPropertyChanged(nameof(ArtistFavoriteLabel));
        OnPropertyChanged(nameof(AlbumFavoriteLabel));
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
        long? artistId = HeaderTrack?.ArtistId;
        if (!artistId.HasValue)
            return;

        _navigationService.NavigateToArtist(artistId.Value);
    }

    [RelayCommand]
    private void AlbumOpen()
    {
        long? albumId = HeaderTrack?.AlbumId;
        if (!albumId.HasValue)
            return;

        _navigationService.NavigateToAlbum(albumId.Value);
    }

    [RelayCommand]
    private void GenreOpen()
    {
        long? genreId = HeaderTrack?.GenreId;
        if (!genreId.HasValue)
            return;

        _navigationService.NavigateToGenre(genreId.Value);
    }

    [RelayCommand]
    private async Task ToggleArtistFavoriteAsync()
    {
        if (Artist is null)
            return;

        await Artist.ArtistFavoriteCommand.ExecuteAsync(null);
        _stateManager.ExecuteOnUIThread(UpdateFavoriteLabels);
    }

    [RelayCommand]
    private async Task ToggleAlbumFavoriteAsync()
    {
        if (Album is null)
            return;

        await Album.AlbumFavoriteCommand.ExecuteAsync(null);
        _stateManager.ExecuteOnUIThread(UpdateFavoriteLabels);
    }

    [RelayCommand]
    private void TrackOpen()
    {
        long? trackId = HeaderTrack?.Id;
        if (!trackId.HasValue)
            return;

        _navigationService.NavigateToTrack(trackId.Value);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _playlistManager.PlaylistChanged -= OnPlaylistChanged;
        _playlistManager.CurrentTrackChanged -= OnCurrentTrackChanged;
        _playerSleepModeService.SleepTimerStateChanged -= OnSleepTimerStateChanged;
        _sleepTimerTracker.RemainingMinutesChanged -= OnRemainingMinutesChanged;

        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
        _disposed = true;
    }
}