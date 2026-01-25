using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Randomizer;
using Rok.Infrastructure.Translate;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Artist.Services;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Artists;

public partial class ArtistViewModel : ObservableObject
{
    private static string FallbackPictureUri => App.Current.Resources["ArtistFallbackPictureUri"] as string ?? "ms-appx:///Assets/artistFallback.png";
    private static BitmapImage FallbackPicture => new(new Uri(FallbackPictureUri));

    private readonly NavigationService _navigationService;
    private readonly ResourceLoader _resourceLoader;
    private readonly ILogger<ArtistViewModel> _logger;
    private readonly IPlayerService _playerService;
    private readonly IBackdropLoader _backdropLoader;
    private readonly IDialogService _dialogService;
    private readonly IAppOptions _appOptions;

    private readonly ArtistDataLoader _dataLoader;
    private readonly ArtistPictureService _pictureService;
    private readonly ArtistApiService _apiService;
    private readonly ArtistStatisticsService _statisticsService;
    private readonly ArtistEditService _editService;
    private IEnumerable<TrackDto>? _tracks = null;

    public IPlaylistMenuService PlaylistMenuService { get; }
    public ArtistDto Artist { get; private set; } = new();
    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];
    public RangeObservableCollection<AlbumViewModel> Albums { get; set; } = [];


    public bool IsFavorite
    {
        get => Artist.IsFavorite;
        set => SetProperty(Artist.IsFavorite, value, Artist, (artist, val) => artist.IsFavorite = val);
    }

    [ObservableProperty]
    public partial BitmapImage? Backdrop { get; set; }

    public string SubTitle
    {
        get
        {
            string label = "";
            string separator = "";

            if (Artist.TrackCount > 0)
            {
                label += $"{Artist.TrackCount} ";

                if (Artist.TrackCount > 1)
                    label += _resourceLoader.GetString("tracks");
                else
                    label += _resourceLoader.GetString("track");
                separator = ", ";
            }

            if (Artist.AlbumCount > 0)
            {
                label += $"{separator}{Artist.AlbumCount} ";
                if (Artist.AlbumCount > 1)
                    label += _resourceLoader.GetString("albums");
                else
                    label += _resourceLoader.GetString("album");
                separator = ", ";
            }

            if (Artist.BestofCount > 0)
            {
                label += $"{separator}{Artist.BestofCount} ";
                if (Artist.BestofCount > 1)
                    label += _resourceLoader.GetString("bestOf");
                else
                    label += _resourceLoader.GetString("bestOf");
                separator = ", ";
            }

            if (Artist.LiveCount > 0)
            {
                label += $"{separator}{Artist.LiveCount} ";
                if (Artist.LiveCount > 1)
                    label += _resourceLoader.GetString("live");
                else
                    label += _resourceLoader.GetString("live");
                separator = ", ";
            }

            if (Artist.CompilationCount > 0)
            {
                label += $"{separator}{Artist.CompilationCount} ";
                if (Artist.CompilationCount > 1)
                    label += _resourceLoader.GetString("compilations");
                else
                    label += _resourceLoader.GetString("compilation");
                separator = ", ";
            }

            if (Artist.TotalDurationSeconds > 0)
            {
                label += separator + _resourceLoader.GetString("duration") + DurationTotal + " min";
            }

            return label;
        }
    }

    public string ListenStats
    {
        get
        {
            string label = "";

            if (Artist.ListenCount > 0)
            {
                label += _resourceLoader.GetString("listenCount") + " "
                                + Artist.ListenCount + " "
                                + _resourceLoader.GetString("listenCountTimes") + ", "
                                + _resourceLoader.GetString("lastListen") + " "
                               + Artist.LastListen?.ToString("g");
            }

            return label;
        }
    }

    public string ActiveYears
    {
        get
        {
            bool formedYearDefined = Artist.FormedYear.HasValue && Artist.FormedYear.GetValueOrDefault(0) > 0;

            if (formedYearDefined && Artist.DiedYear.GetValueOrDefault(0) > 0)
                return $"{Artist.FormedYear} - {Artist.DiedYear}";
            if (formedYearDefined && Artist.Disbanded)
                return $"{Artist.FormedYear} -";
            if (formedYearDefined)
                return Artist.FormedYear!.Value.ToString();

            if (Artist.YearMini.HasValue && Artist.YearMaxi.HasValue)
                return $"{Artist.YearMini} - {Artist.YearMaxi}";

            if (Artist.YearMini.HasValue)
                return $"{Artist.YearMini}";

            if (Artist.YearMaxi.HasValue)
                return $"{Artist.YearMaxi}";

            return string.Empty;
        }
    }

    public string DurationTotal
    {
        get
        {
            TimeSpan time = TimeSpan.FromSeconds(Artist.TotalDurationSeconds);
            int roundedMinutes = (int)Math.Round(time.TotalMinutes);
            return roundedMinutes.ToString();
        }
    }

    private BitmapImage? _picture = null;
    public BitmapImage Picture
    {
        get
        {
            if (_picture == null)
                LoadPicture();

            return _picture ?? FallbackPicture;
        }
        set
        {
            SetProperty(ref _picture, value);
            OnPropertyChanged(nameof(IsPictureAvailable));
        }
    }

    public bool IsPictureAvailable
    {
        get
        {
            return _picture != null && _picture.UriSource != FallbackPicture.UriSource;
        }
    }

    public override string ToString() => Artist?.Name ?? string.Empty;


    public ArtistViewModel(
        IBackdropLoader backdropLoader,
        NavigationService navigationService,
        IPlayerService playerService,
        IDialogService dialogService,
        ResourceLoader resourceLoader,
        ArtistDataLoader dataLoader,
        ArtistPictureService pictureService,
        ArtistApiService apiService,
        ArtistStatisticsService statisticsService,
        ArtistEditService editService,
        IAppOptions appOptions,
        IPlaylistMenuService playlistMenuService,
        ILogger<ArtistViewModel> logger)
    {
        _backdropLoader = Guard.Against.Null(backdropLoader);
        _navigationService = Guard.Against.Null(navigationService);
        _playerService = Guard.Against.Null(playerService);
        _dialogService = Guard.Against.Null(dialogService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _dataLoader = Guard.Against.Null(dataLoader);
        _pictureService = Guard.Against.Null(pictureService);
        _apiService = Guard.Against.Null(apiService);
        _statisticsService = Guard.Against.Null(statisticsService);
        _editService = Guard.Against.Null(editService);
        _appOptions = Guard.Against.Null(appOptions);
        PlaylistMenuService = Guard.Against.Null(playlistMenuService);
        _logger = Guard.Against.Null(logger);
    }


    public void SetData(ArtistDto artist)
    {
        Artist = Guard.Against.Null(artist);
    }

    public async Task LoadDataAsync(long artistId, bool loadAlbums = true, bool loadTracks = true, bool fetchApi = true)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        await LoadArtistAsync(artistId);

        if (loadAlbums)
            await LoadAlbumsAsync(artistId);

        if (loadTracks)
            await LoadTracksAsync(artistId);

        if (loadAlbums && loadTracks)
            await UpdateStatisticsIfNeededAsync();

        if (fetchApi)
            await GetDataFromApiAsync();

        stopwatch.Stop();

        _logger.LogInformation("Artist {ArtistId} loaded in {ElapsedMilliseconds} ms (albums: {LoadAlbums}, tracks: {LoadTracks}, api: {FetchApi})",
                                artistId, stopwatch.ElapsedMilliseconds, loadAlbums, loadTracks, fetchApi);
    }

    private async Task LoadArtistAsync(long artistId)
    {
        ArtistDto? artist = await _dataLoader.LoadArtistAsync(artistId);
        if (artist != null)
        {
            Artist = artist;
            LoadBackdrop();
        }
    }

    public void LoadPicture()
    {
        Picture = _pictureService.LoadPicture(Artist.Name);
    }

    public void LoadBackdrop()
    {
        _backdropLoader.LoadBackdrop(Artist.Name, (BitmapImage? backdropImage) =>
        {
            Backdrop = backdropImage;
        });
    }

    private async Task LoadAlbumsAsync(long artistId)
    {
        List<AlbumViewModel> albums = await _dataLoader.LoadAlbumsAsync(artistId);
        Albums.AddRange(albums);
    }

    private async Task LoadTracksAsync(long artistId)
    {
        List<TrackViewModel> tracks = await _dataLoader.LoadTracksAsync(artistId);
        _tracks = tracks.Select(t => t.Track);
        Tracks.AddRange(tracks);

        OnPropertyChanged(nameof(DurationTotal));
    }

    private async Task UpdateStatisticsIfNeededAsync()
    {
        bool updated = await _statisticsService.UpdateIfNeededAsync(Artist, Albums, Tracks);
        if (updated)
        {
            Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
        }
    }

    private async Task GetDataFromApiAsync()
    {
        bool updated = await _apiService.GetAndUpdateArtistDataAsync(Artist);

        if (updated)
        {
            if (_pictureService.PictureExists(Artist.Name))
                LoadPicture();

            ArtistDto? refreshedArtist = await _dataLoader.ReloadArtistAsync(Artist.Id);
            if (refreshedArtist != null)
                Artist = refreshedArtist;

            OnPropertyChanged("");
            Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
        }
    }



    [RelayCommand]
    private void ArtistOpen()
    {
        _navigationService.NavigateToArtist(Artist.Id);
    }

    [RelayCommand]
    private void GenreOpen()
    {
        if (Artist.GenreId.HasValue)
            _navigationService.NavigateToGenre(Artist.GenreId.Value);
    }

    [RelayCommand]
    private async Task ListenAsync()
    {
        if (_tracks == null)
            await LoadTracksAsync(Artist.Id);

        if (_tracks?.Any() == true)
        {
            List<TrackDto> tracks = TracksRandomizer.Randomize(_tracks);
            _playerService.LoadPlaylist(tracks);
        }
    }

    [RelayCommand]
    private async Task ArtistFavoriteAsync()
    {
        bool newFavoriteState = !Artist.IsFavorite;
        await _editService.UpdateFavoriteAsync(Artist, newFavoriteState);

        OnPropertyChanged(nameof(IsFavorite));
        Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
    }

    [RelayCommand]
    private async Task OpenOfficialSiteAsync()
    {
        await _editService.OpenOfficialSiteAsync(Artist);
    }

    [RelayCommand]
    private async Task SelectPictureAsync()
    {
        BitmapImage? newPicture = await _pictureService.SelectAndSavePictureAsync(Artist.Name);

        if (newPicture != null)
        {
            if (Rok.App.MainWindow.DispatcherQueue is { } dq)
                dq.TryEnqueue(() => Picture = newPicture);
            else
                Picture = newPicture;

            Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Picture));
        }
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            url = url.Replace("http://", "https://");

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            _ = Windows.System.Launcher.LaunchUriAsync(uri);
    }

    [RelayCommand]
    private async Task OpenBiographyAsync()
    {
        if (!string.IsNullOrEmpty(Artist.Biography))
        {
            string? rawLanguage = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
            string language = TranslateService.NormalizeLanguageForLibreTranslate(rawLanguage, "fr");

            await _dialogService.ShowTextAsync(Artist.Name, Artist.Biography, showTranslateButton: _appOptions.NovaApiEnabled, language);
        }
    }
}