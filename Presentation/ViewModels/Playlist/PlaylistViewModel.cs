using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Player;
using Rok.ViewModels.Playlist.Services;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Playlist;

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

    public int TrackMaximum
    {
        get => Playlist.TrackMaximum;
        set => Playlist.TrackMaximum = value;
    }

    public string Name
    {
        get => Playlist.Name;
        set
        {
            Playlist.Name = value;
            _ = SavePlaylistAsync();
        }
    }


    [ObservableProperty]
    public partial BitmapImage? Backdrop { get; set; }

    [ObservableProperty]
    public partial BitmapImage? Picture { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTitleSorted))]
    [NotifyPropertyChangedFor(nameof(IsArtistSorted))]
    [NotifyPropertyChangedFor(nameof(IsAlbumSorted))]
    [NotifyPropertyChangedFor(nameof(IsScoreSorted))]
    public partial string? CurrentSortColumn { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; }



    public int TrackCount => _tracks?.Count() ?? Playlist.TrackCount;

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
            long duration = _tracks?.Sum(c => c.Duration) ?? Playlist.Duration;
            TimeSpan time = TimeSpan.FromSeconds(duration);
            return (int)time.TotalHours + time.ToString(@"\:mm\:ss");
        }
    }

    public bool IsTitleSorted => CurrentSortColumn == "Title";
    public bool IsArtistSorted => CurrentSortColumn == "Artist";
    public bool IsAlbumSorted => CurrentSortColumn == "Album";
    public bool IsScoreSorted => CurrentSortColumn == "Score";

    public string GetSortGlyph(bool descending) => descending ? "\uE74B" : "\uE74A";


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

        OnPropertyChanged(nameof(SubTitle));
    }

    private async Task LoadPlaylistAsync(long playlistId)
    {
        PlaylistHeaderDto? playlist = await _dataLoader.LoadPlaylistAsync(playlistId);
        if (playlist == null)
            return;

        Playlist = playlist;
        LoadBackdrop();
        LoadPicture();
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
        if (playlistId <= 0)
            return;

        List<TrackViewModel> tracks = await _dataLoader.LoadTracksAsync(playlistId);
        _tracks = tracks.Select(t => t.Track);
        _originalTracks = tracks.ToList();
        Tracks.InitWithAddRange(tracks);
    }

    private async Task SavePlaylistAsync()
    {
        bool updated = await _updateService.SavePlaylistAsync(Playlist, Tracks);

        if (!updated)
            return;

        LoadPicture();
        OnPropertyChanged();
    }



    [RelayCommand]
    private async Task SavePlaylistAsync(List<PlaylistGroupDto>? groups)
    {
        bool updated = await _updateService.SavePlaylistAsync(Playlist, Tracks, forceUpdate: true, groups);

        if (!updated)
            return;

        LoadPicture();
        OnPropertyChanged();
    }

    [RelayCommand]
    private async Task MoveTrackAsync()
    {
        if (Tracks.Count == 0 || Playlist.Id == 0)
            return;

        await _updateService.SaveTracksPositionAsync(Playlist.Id, Tracks.Select(c => c.Track.Id).ToList());
    }

    [RelayCommand]
    private async Task ListenAsync(TrackViewModel? track)
    {
        if (_tracks == null)
        {
            if (Playlist.Type == (int)PlaylistType.Smart)
                await GenerateAsync();
            else
                await LoadTracksAsync(Playlist.Id);
        }

        if (_tracks?.Any() == true)
            _playerService.LoadPlaylist(_tracks.ToList(), track?.Track);
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        await _generationService.GenerateTracksAsync(Playlist);

        List<TrackViewModel> trackViewModels = await _dataLoader.LoadTracksAsync(Playlist.Id);
        _tracks = trackViewModels.Select(t => t.Track);
        _originalTracks = trackViewModels.ToList();
        Tracks.InitWithAddRange(trackViewModels);

        await SavePlaylistAsync();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void PlaylistOpen()
    {
        if (Playlist.Id <= 0)
            return;

        if (Playlist.Type == (int)PlaylistType.Smart)
            _navigationService.NavigateToSmartPlaylist(Playlist.Id);
        else
            _navigationService.NavigateToPlaylist(Playlist.Id);
    }


    [RelayCommand]
    public void ShufflePlaylist()
    {
        if (Tracks.Count == 0)
            return;

        Tracks.Shuffle();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Playlist.Id <= 0)
            return;

        bool deleted = await _updateService.DeletePlaylistAsync(Playlist.Id, Playlist.Name);

        if (!deleted)
            return;

        _navigationService.RemoveLastEntry();
        _navigationService.NavigateToPlaylists();
    }

    [RelayCommand]
    private async Task RemoveFromPlaylistAsync(long trackId)
    {
        bool removed = await _updateService.RemoveTrackAsync(Playlist.Id, trackId);

        if (!removed)
            return;

        TrackViewModel? track = Tracks.FirstOrDefault(c => c.Track.Id == trackId);
        if (track != null)
            Tracks.Remove(track);

        TrackViewModel? originalTrack = _originalTracks.FirstOrDefault(t => t.Track.Id == trackId);
        if (originalTrack != null)
            _originalTracks.Remove(originalTrack);

        _tracks = _tracks!.Where(t => t.Id != trackId).ToList();

        OnPropertyChanged(nameof(SubTitle));
    }

    [RelayCommand]
    private void Sort(string? column)
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