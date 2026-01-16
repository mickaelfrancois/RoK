using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Playlist.Services;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Playlists;

public partial class PlaylistViewModel : ObservableObject
{
    private readonly NavigationService _navigationService;
    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly ILogger<PlaylistViewModel> _logger;
    private readonly IBackdropLoader _backdropLoader;

    private readonly PlaylistDataLoader _dataLoader;
    private readonly PlaylistPictureService _pictureService;
    private readonly PlaylistUpdateService _updateService;
    private readonly PlaylistGenerationService _generationService;

    private List<TrackViewModel> _originalTracks = [];

    public PlaylistHeaderDto Playlist { get; private set; } = new();
    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];

    private IEnumerable<TrackDto>? _tracks = null;

    public string Name
    {
        get => Playlist.Name;
        set
        {
            if (Playlist.Name != value)
            {
                Playlist.Name = value;
                _ = SavePlaylistAsync(forceUpdate: true);
            }
        }
    }

    public int TrackMaximum
    {
        get => Playlist.TrackMaximum;
        set => Playlist.TrackMaximum = value;
    }

    public int TrackCount => Playlist.TrackCount;

    public int ArtistCount => _tracks?.DistinctBy(c => c.ArtistId).Count() ?? 0;

    public string SubTitle
    {
        get
        {
            string label = $"{TrackCount} ";
            label += TrackCount > 1 ? _resourceLoader.GetString("tracks") : _resourceLoader.GetString("track");
            label += ", " + DurationTotalStr;
            return label;
        }
    }

    public string DurationTotalStr
    {
        get
        {
            TimeSpan time = TimeSpan.FromSeconds(Playlist.Duration);
            return (int)time.TotalHours + time.ToString(@"\:mm\:ss");
        }
    }

    private BitmapImage? _backdrop = null;
    public BitmapImage? Backdrop
    {
        get => _backdrop;
        set
        {
            _backdrop = value;
            OnPropertyChanged();
        }
    }

    private BitmapImage? _picture = null;
    public BitmapImage? Picture
    {
        get => _picture;
        set
        {
            _picture = value;
            OnPropertyChanged();
        }
    }

    private string? _currentSortColumn;
    public string? CurrentSortColumn
    {
        get => _currentSortColumn;
        set
        {
            if (SetProperty(ref _currentSortColumn, value))
            {
                OnPropertyChanged(nameof(IsTitleSorted));
                OnPropertyChanged(nameof(IsArtistSorted));
                OnPropertyChanged(nameof(IsAlbumSorted));
                OnPropertyChanged(nameof(IsScoreSorted));
            }
        }
    }

    private bool _sortDescending;
    public bool SortDescending
    {
        get => _sortDescending;
        set => SetProperty(ref _sortDescending, value);
    }

    public bool IsTitleSorted => CurrentSortColumn == "Title";
    public bool IsArtistSorted => CurrentSortColumn == "Artist";
    public bool IsAlbumSorted => CurrentSortColumn == "Album";
    public bool IsScoreSorted => CurrentSortColumn == "Score";

    public string GetSortGlyph(bool descending) => descending ? "\uE74B" : "\uE74A";

    public AsyncRelayCommand ListenCommand { get; private set; }
    public AsyncRelayCommand GenerateCommand { get; private set; }
    public RelayCommand PlaylistOpenCommand { get; private set; }
    public AsyncRelayCommand DeleteCommand { get; private set; }
    public AsyncRelayCommand<long> RemoveFromPlaylistCommand { get; private set; }
    public AsyncRelayCommand<List<PlaylistGroupDto>> SavePlaylistCommand { get; private set; }
    public AsyncRelayCommand MoveTrackCommand { get; private set; }
    public RelayCommand<string> SortCommand { get; private set; }

    public PlaylistViewModel(
        IBackdropLoader backdropLoader,
        NavigationService navigationService,
        IPlayerService playerService,
        ResourceLoader resourceLoader,
        PlaylistDataLoader dataLoader,
        PlaylistPictureService pictureService,
        PlaylistUpdateService updateService,
        PlaylistGenerationService generationService,
        ILogger<PlaylistViewModel> logger)
    {
        _backdropLoader = Guard.Against.Null(backdropLoader);
        _navigationService = Guard.Against.Null(navigationService);
        _playerService = Guard.Against.Null(playerService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _dataLoader = Guard.Against.Null(dataLoader);
        _pictureService = Guard.Against.Null(pictureService);
        _updateService = Guard.Against.Null(updateService);
        _generationService = Guard.Against.Null(generationService);
        _logger = Guard.Against.Null(logger);

        ListenCommand = new AsyncRelayCommand(ListenAsync);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        PlaylistOpenCommand = new RelayCommand(OpenPlaylist);
        DeleteCommand = new AsyncRelayCommand(DeletePlaylistAsync);
        RemoveFromPlaylistCommand = new AsyncRelayCommand<long>(RemoveTrackAsync);
        SavePlaylistCommand = new AsyncRelayCommand<List<PlaylistGroupDto>>((c) => SavePlaylistAsync(forceUpdate: true, c));
        MoveTrackCommand = new AsyncRelayCommand(async () => await MoveTrackAsync());
        SortCommand = new RelayCommand<string>(SortTracks);
    }

    public void SetData(PlaylistHeaderDto playlist)
    {
        string oldPicture = Playlist.Picture;
        Playlist = Guard.Against.Null(playlist);

        if (oldPicture != Playlist.Picture)
        {
            LoadPicture();
            LoadBackdrop();
        }

        OnPropertyChanged();
    }

    public async Task LoadDataAsync(long playlistId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        await LoadPlaylistAsync(playlistId);
        await LoadTracksAsync(playlistId);

        if (!Tracks.Any() && Playlist.Type == (int)PlaylistType.Smart)
        {
            await GenerateAsync();
            List<TrackViewModel> tracks = await _dataLoader.LoadTracksAsync(playlistId);
            _tracks = tracks.Select(t => t.Track);
        }

        await SavePlaylistAsync();

        stopwatch.Stop();
        _logger.LogInformation("Playlist {PlaylistId} loaded in {ElapsedMilliseconds}ms", playlistId, stopwatch.ElapsedMilliseconds);
    }

    private async Task LoadPlaylistAsync(long playlistId)
    {
        PlaylistHeaderDto? playlist = await _dataLoader.LoadPlaylistAsync(playlistId);
        if (playlist != null)
        {
            Playlist = playlist;
            LoadBackdrop();
            LoadPicture();
        }
    }

    private void LoadBackdrop()
    {
        _backdropLoader.LoadBackdrop(Playlist.Picture, (BitmapImage? backdropImage) =>
        {
            Backdrop = backdropImage;
        });
    }

    public void LoadPicture()
    {
        Picture = _pictureService.LoadPicture(Playlist.Picture);
    }

    private async Task LoadTracksAsync(long playlistId)
    {
        if (playlistId > 0)
        {
            List<TrackViewModel> tracks = await _dataLoader.LoadTracksAsync(playlistId);
            _tracks = tracks.Select(t => t.Track);
            _originalTracks = tracks.ToList();
            Tracks.InitWithAddRange(tracks);
        }
    }

    private async Task SavePlaylistAsync(bool forceUpdate = false, List<PlaylistGroupDto>? groups = null)
    {
        bool updated = await _updateService.SavePlaylistAsync(Playlist, Tracks, forceUpdate, groups);

        if (updated)
        {
            LoadPicture();
            OnPropertyChanged();
        }
    }

    private async Task MoveTrackAsync()
    {
        if (Tracks.Count == 0 || Playlist.Id == 0)
            return;

        await _updateService.SaveTracksPositionAsync(Playlist.Id, Tracks.Select(c => c.Track.Id).ToList());
    }

    private async Task ListenAsync()
    {
        if (_tracks == null)
        {
            if (Playlist.Type == (int)PlaylistType.Smart)
                await GenerateAsync();
            else
                await LoadTracksAsync(Playlist.Id);
        }

        if (_tracks?.Any() == true)
        {
            _playerService.LoadPlaylist(_tracks.ToList());
        }
    }

    private async Task GenerateAsync()
    {
        await _generationService.GenerateTracksAsync(Playlist);

        List<TrackViewModel> trackViewModels = await _dataLoader.LoadTracksAsync(Playlist.Id);
        _tracks = trackViewModels.Select(t => t.Track);
        _originalTracks = trackViewModels.ToList();
        Tracks.InitWithAddRange(trackViewModels);

        await SavePlaylistAsync();
        OnPropertyChanged();
    }

    private void OpenPlaylist()
    {
        if (Playlist.Id > 0)
        {
            if (Playlist.Type == (int)PlaylistType.Smart)
                _navigationService.NavigateToSmartPlaylist(Playlist.Id);
            else
                _navigationService.NavigateToPlaylist(Playlist.Id);
        }
    }

    private async Task DeletePlaylistAsync()
    {
        if (Playlist.Id > 0)
        {
            bool deleted = await _updateService.DeletePlaylistAsync(Playlist.Id, Playlist.Name);

            if (deleted)
            {
                _navigationService.RemoveLastEntry();
                _navigationService.NavigateToPlaylists();
            }
        }
    }

    private async Task RemoveTrackAsync(long trackId)
    {
        bool removed = await _updateService.RemoveTrackAsync(Playlist.Id, trackId);

        if (removed)
        {
            TrackViewModel? track = Tracks.FirstOrDefault(c => c.Track.Id == trackId);
            if (track != null)
                Tracks.Remove(track);

            TrackViewModel? originalTrack = _originalTracks.FirstOrDefault(t => t.Track.Id == trackId);
            if (originalTrack != null)
                _originalTracks.Remove(originalTrack);

            _tracks = _tracks!.Where(t => t.Id != trackId).ToList();
        }
    }

    private void SortTracks(string? column)
    {
        if (string.IsNullOrEmpty(column))
            return;

        if (CurrentSortColumn == column)
        {
            if (!SortDescending)
            {
                SortDescending = true;
            }
            else
            {
                CurrentSortColumn = null;
                SortDescending = false;
                Tracks.InitWithAddRange(_originalTracks);
                return;
            }
        }
        else
        {
            CurrentSortColumn = column;
            SortDescending = false;
        }

        IEnumerable<TrackViewModel> sorted = column switch
        {
            "Title" => SortDescending ? Tracks.OrderByDescending(t => t.Title) : Tracks.OrderBy(t => t.Title),
            "Artist" => SortDescending ? Tracks.OrderByDescending(t => t.ArtistName) : Tracks.OrderBy(t => t.ArtistName),
            "Album" => SortDescending ? Tracks.OrderByDescending(t => t.AlbumName) : Tracks.OrderBy(t => t.AlbumName),
            "Score" => SortDescending ? Tracks.OrderByDescending(t => t.Score) : Tracks.OrderBy(t => t.Score),
            _ => _originalTracks
        };

        Tracks.InitWithAddRange(sorted.ToList());
    }
}