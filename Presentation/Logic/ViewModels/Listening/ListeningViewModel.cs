using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Listening.Services;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Listening;

public partial class ListeningViewModel : ObservableObject
{
    private readonly ILogger<ListeningViewModel> _logger;
    private readonly IPlayerService _playerService;

    private readonly ListeningPlaylistManager _playlistManager;
    private readonly ListeningPlaybackService _playbackService;

    public int TrackCount => _playlistManager.TrackCount;
    public long Duration => _playlistManager.Duration;
    public ArtistViewModel? Artist => _playlistManager.Artist;
    public RangeObservableCollection<TrackViewModel> Tracks => _playlistManager.Tracks;
    public TrackViewModel? CurrentTrack => _playlistManager.CurrentTrack;

    public AsyncRelayCommand<TrackViewModel> AddMoreFromArtistCommand { get; private set; }
    public RelayCommand ShufflePlaylistCommand { get; private set; }

    public ListeningViewModel(
        IPlayerService playerService,
        ListeningPlaylistManager playlistManager,
        ListeningPlaybackService playbackService,
        ILogger<ListeningViewModel> logger)
    {
        _playerService = Guard.Against.Null(playerService);
        _playlistManager = Guard.Against.Null(playlistManager);
        _playbackService = Guard.Against.Null(playbackService);
        _logger = Guard.Against.Null(logger);

        InitializeCommands();
        SubscribeToMessages();
        SubscribeToEvents();

        InitializeFromPlayerService();
    }

    private void InitializeCommands()
    {
        AddMoreFromArtistCommand = new AsyncRelayCommand<TrackViewModel>(AddMoreFromArtistAsync);
        ShufflePlaylistCommand = new RelayCommand(ShuffleTracks);
    }

    private void SubscribeToMessages()
    {
        Messenger.Subscribe<MediaChangedMessage>(async (message) => await MediaChangedAsync(message));
        Messenger.Subscribe<PlaylistChanged>(async (message) => await PlaylistChangedAsync(message));
    }

    private void SubscribeToEvents()
    {
        _playlistManager.PlaylistChanged += OnPlaylistChanged;
        _playlistManager.CurrentTrackChanged += OnCurrentTrackChanged;
    }

    private void InitializeFromPlayerService()
    {
        if (_playerService.Playlist != null)
        {
            _playlistManager.LoadTracksList(_playerService.Playlist);
#pragma warning disable CS4014
            _playlistManager.SetCurrentTrackAsync(_playerService.CurrentTrack);
#pragma warning restore CS4014
        }
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

    private async Task MediaChangedAsync(MediaChangedMessage message)
    {
        _logger.LogDebug("Listening VM handle media changed, title {Message}.", message.NewTrack.Title);
        await _playlistManager.SetCurrentTrackAsync(message.NewTrack);
    }

    private async Task PlaylistChangedAsync(PlaylistChanged message)
    {
        _logger.LogDebug("Listening VM handle playlist changed.");
        _playlistManager.LoadTracksList(message.Tracks);
        await _playlistManager.SetCurrentTrackAsync(_playerService.CurrentTrack);
    }

    private async Task AddMoreFromArtistAsync(TrackViewModel track)
    {
        IEnumerable<long> currentTrackIds = Tracks.Select(t => t.Track.Id);
        await _playbackService.AddMoreFromArtistAsync(track, currentTrackIds);
    }

    public void ShuffleTracks()
    {
        _playbackService.ShuffleTracks();
    }
}