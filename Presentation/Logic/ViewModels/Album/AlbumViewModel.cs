using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Album.Services;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Albums;

public partial class AlbumViewModel : ObservableObject
{
    private readonly NavigationService _navigationService;
    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly ILogger<AlbumViewModel> _logger;
    private readonly ILastFmClient _lastFmClient;
    private readonly IBackdropLoader _backdropLoader;

    private readonly AlbumDataLoader _dataLoader;
    private readonly AlbumPictureService _pictureService;
    private readonly AlbumApiService _apiService;
    private readonly AlbumStatisticsService _statisticsService;
    private readonly AlbumEditService _editService;

    public AlbumDto Album { get; private set; } = new();
    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];

    private IEnumerable<TrackDto>? _tracks = null;

    public bool LastFmPageAvailable { get; set; }

    public bool IsFavorite
    {
        get => Album.IsFavorite;
        set
        {
            if (Album.IsFavorite != value)
            {
                Album.IsFavorite = value;
                OnPropertyChanged();
            }
        }
    }

    public string SubTitle
    {
        get
        {
            string label = "";
            string separator = "";

            if (Album.TrackCount > 0)
            {
                label += $"{Album.TrackCount} ";
                if (Album.TrackCount > 1)
                    label += _resourceLoader.GetString("tracks");
                else
                    label += _resourceLoader.GetString("track");
                separator = ", ";
            }

            if (Album.Duration > 0)
            {
                label += separator + DurationTotal + " min";
                separator = ", ";
            }

            if (!string.IsNullOrEmpty(ReleaseDateYear))
                label += separator + ' ' + ReleaseDateYear;

            return label;
        }
    }

    public string ReleaseDateYear
    {
        get
        {
            if (Album.ReleaseDate.HasValue)
                return Album.ReleaseDate.Value.Year.ToString();
            else if (Album.Year.HasValue)
                return Album.Year.Value.ToString();

            return "N/A";
        }
    }

    public string DurationTotal
    {
        get
        {
            TimeSpan time = TimeSpan.FromSeconds(Album.Duration);
            int roundedMinutes = (int)Math.Round(time.TotalMinutes);
            return roundedMinutes.ToString();
        }
    }

    private BitmapImage? _picture = null;
    public BitmapImage? Picture
    {
        get
        {
            if (_picture == null)
                LoadPicture();

            return _picture;
        }
        set
        {
            _picture = value;
            OnPropertyChanged();
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

    public AsyncRelayCommand ListenCommand { get; private set; }
    public RelayCommand AlbumOpenCommand { get; private set; }
    public RelayCommand ArtistOpenCommand { get; private set; }
    public RelayCommand GenreOpenCommand { get; private set; }
    public RelayCommand OpenArtistsByCountryCommand { get; private set; }
    public AsyncRelayCommand AlbumFavoriteCommand { get; private set; }
    public AsyncRelayCommand SelectPictureCommand { get; private set; }
    public AsyncRelayCommand EditAlbumCommand { get; private set; }
    public RelayCommand OpenLastFmPageCommand { get; private set; }

    public override string ToString()
    {
        if (Album == null)
            return string.Empty;
        return Album.Name;
    }

    public AlbumViewModel(
        IBackdropLoader backdropLoader,
        ILastFmClient lastFmClient,
        NavigationService navigationService,
        IPlayerService playerService,
        ResourceLoader resourceLoader,
        AlbumDataLoader dataLoader,
        AlbumPictureService pictureService,
        AlbumApiService apiService,
        AlbumStatisticsService statisticsService,
        AlbumEditService editService,
        ILogger<AlbumViewModel> logger)
    {
        _backdropLoader = Guard.Against.Null(backdropLoader);
        _lastFmClient = Guard.Against.Null(lastFmClient);
        _navigationService = Guard.Against.Null(navigationService);
        _playerService = Guard.Against.Null(playerService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _dataLoader = Guard.Against.Null(dataLoader);
        _pictureService = Guard.Against.Null(pictureService);
        _apiService = Guard.Against.Null(apiService);
        _statisticsService = Guard.Against.Null(statisticsService);
        _editService = Guard.Against.Null(editService);
        _logger = Guard.Against.Null(logger);

        ListenCommand = new AsyncRelayCommand(ListenAsync);
        AlbumFavoriteCommand = new AsyncRelayCommand(UpdateFavoriteStateAsync);
        AlbumOpenCommand = new RelayCommand(AlbumOpen);
        ArtistOpenCommand = new RelayCommand(ArtistOpen);
        GenreOpenCommand = new RelayCommand(() => { });
        OpenArtistsByCountryCommand = new RelayCommand(() => { });
        SelectPictureCommand = new AsyncRelayCommand(SelectPictureAsync);
        EditAlbumCommand = new AsyncRelayCommand(EditAlbumAsync);
        OpenLastFmPageCommand = new RelayCommand(OpenLastFmPage);
    }


    public void SetData(AlbumDto album)
    {
        Album = Guard.Against.Null(album);
    }

    public async Task LoadDataAsync(long albumId)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        await LoadAlbumAsync(albumId);
        await LoadTracksAsync(albumId);
        await UpdateStatisticsIfNeededAsync();
        await GetDataFromApiAsync();

        stopwatch.Stop();
        _logger.LogInformation("Album {AlbumId} loaded in {ElapsedMilliseconds} ms", albumId, stopwatch.ElapsedMilliseconds);

        await CheckLastFmUrlAsync();
    }

    private async Task LoadTracksAsync(long albumId)
    {
        List<TrackViewModel> tracks = await _dataLoader.LoadTracksAsync(albumId);
        _tracks = tracks.Select(t => t.Track);
        Tracks.AddRange(tracks);
    }

    private async Task LoadAlbumAsync(long albumId)
    {
        AlbumDto? album = await _dataLoader.LoadAlbumAsync(albumId);
        if (album != null)
        {
            Album = album;
            LoadBackrop();
        }
    }

    public void LoadPicture()
    {
        Picture = _pictureService.LoadPicture(Album.AlbumPath);
    }

    public void LoadBackrop()
    {
        _backdropLoader.LoadBackdrop(Album.ArtistName, (BitmapImage? backdropImage) =>
        {
            Backdrop = backdropImage;
        });
    }

    private async Task UpdateStatisticsIfNeededAsync()
    {
        bool updated = await _statisticsService.UpdateIfNeededAsync(Album, Tracks);
        if (updated)
        {
            Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        }
    }

    private void AlbumOpen()
    {
        _navigationService.NavigateToAlbum(Album.Id);
    }

    private void ArtistOpen()
    {
        if (Album.ArtistId.HasValue)
            _navigationService.NavigateToArtist(Album.ArtistId.Value);
    }

    private async Task ListenAsync()
    {
        if (_tracks == null)
            await LoadTracksAsync(Album.Id);

        if (_tracks?.Any() == true)
            _playerService.LoadPlaylist(_tracks.ToList());
    }

    private async Task UpdateFavoriteStateAsync()
    {
        bool newFavoriteState = !Album.IsFavorite;
        await _editService.UpdateFavoriteAsync(Album, newFavoriteState);

        OnPropertyChanged(nameof(IsFavorite));
        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
    }

    private async Task GetDataFromApiAsync()
    {
        bool updated = await _apiService.GetAndUpdateAlbumDataAsync(Album);

        if (updated)
        {
            if (_pictureService.PictureExists(Album.AlbumPath))
                LoadPicture();

            AlbumDto? refreshedAlbum = await _dataLoader.ReloadAlbumAsync(Album.Id);
            if (refreshedAlbum != null)
                Album = refreshedAlbum;

            Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        }
    }

    private async Task EditAlbumAsync()
    {
        bool updated = await _editService.EditAlbumAsync(Album);

        if (updated)
        {
            Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
            OnPropertyChanged(nameof(Album));
        }
    }

    private async Task SelectPictureAsync()
    {
        BitmapImage? newPicture = await _pictureService.SelectAndSavePictureAsync(Album.AlbumPath);

        if (newPicture != null)
        {
            if (Rok.App.MainWindow.DispatcherQueue is { } dq)
                dq.TryEnqueue(() => Picture = newPicture);
            else
                Picture = newPicture;

            Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Picture));
        }
    }

    private async Task CheckLastFmUrlAsync()
    {
        LastFmPageAvailable = await _lastFmClient.IsAlbumPageAvailableAsync(Album.ArtistName, Album.Name);
        OnPropertyChanged(nameof(LastFmPageAvailable));
    }

    private void OpenLastFmPage()
    {
        if (LastFmPageAvailable)
        {
            string artistPageUrl = _lastFmClient.GetAlbumPageUrl(Album.ArtistName, Album.Name);
            Uri uri = new(artistPageUrl);
            _ = Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }
}