using System.Collections.Specialized;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Player;
using Rok.Application.Randomizer;
using Rok.Application.Services.Filters;
using Rok.Application.Services.Grouping;
using Rok.Commons;
using Rok.Infrastructure.Translate;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Artist;

public partial class ArtistViewModel : ObservableObject, IFilterableArtist, IGroupableArtist
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
    private readonly TagsProvider _tagsProvider;
    private readonly ArtistPictureService _pictureService;
    private readonly ArtistApiService _apiService;
    private readonly ArtistStatisticsService _statisticsService;
    private readonly ArtistEditService _editService;
    private readonly BackdropPicture _backdropPicture;
    private readonly IDominantColorCalculator _dominantColorCalculator;

    private IEnumerable<TrackDto>? _tracks = null;
    private CancellationTokenSource _navigationCts = new();

    public IPlaylistMenuService PlaylistMenuService { get; }
    public ArtistDto Artist { get; private set; } = new();
    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];
    public RangeObservableCollection<AlbumViewModel> Albums { get; set; } = [];


    public ObservableCollection<string> EditableTags { get; set; } = new();

    public ObservableCollection<string> SuggestedTags { get; set; } = new();

    public bool IsFavorite
    {
        get => Artist.IsFavorite;
        set => SetProperty(Artist.IsFavorite, value, Artist, (artist, val) => artist.IsFavorite = val);
    }

    [ObservableProperty]
    public partial Windows.UI.Color DominantColor { get; set; }
    private int _dominantColorCalculating = 0;

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

    public long? GenreId => Artist.GenreId;

    public int ListenCount => Artist.ListenCount;

    public bool IsGenreFavorite => Artist.IsGenreFavorite;

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool ShowNewBadge { get; set; }

    public List<string> Tags => Artist.GetTags();

    string IGroupableArtist.Name => Artist.Name;
    int? IGroupableArtist.YearMini => Artist.YearMini;
    string? IGroupable.CountryCode => Artist.CountryCode;
    DateTime IGroupable.CreatDate => Artist.CreatDate;
    DateTime? IGroupable.LastListen => Artist.LastListen;

    public override string ToString() => Artist?.Name ?? string.Empty;


    public ArtistViewModel(
        IBackdropLoader backdropLoader,
        NavigationService navigationService,
        IPlayerService playerService,
        IDialogService dialogService,
        ResourceLoader resourceLoader,
        ArtistDataLoader dataLoader,
        TagsProvider tagsDataLoader,
        ArtistPictureService pictureService,
        ArtistApiService apiService,
        ArtistStatisticsService statisticsService,
        IDominantColorCalculator dominantColorCalculator,
        ArtistEditService editService,
        BackdropPicture backdropPicture,
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
        _tagsProvider = Guard.Against.Null(tagsDataLoader);
        _pictureService = Guard.Against.Null(pictureService);
        _apiService = Guard.Against.Null(apiService);
        _statisticsService = Guard.Against.Null(statisticsService);
        _dominantColorCalculator = Guard.Against.Null(dominantColorCalculator);
        _editService = Guard.Against.Null(editService);
        _backdropPicture = Guard.Against.Null(backdropPicture);
        _appOptions = Guard.Against.Null(appOptions);
        PlaylistMenuService = Guard.Against.Null(playlistMenuService);
        _logger = Guard.Against.Null(logger);
    }


    public void SetData(ArtistDto artist)
    {
        Artist = Guard.Against.Null(artist);
        IsNew = Artist.CreatDate > DateTime.UtcNow.AddDays(-_appOptions.ArtistRecentThresholdDays);

        if (Artist.PictureDominantColor.HasValue)
            DominantColor = ColorHelper.FromArgb(Artist.PictureDominantColor.Value);
    }

    public async Task LoadDataAsync(long artistId, bool loadAlbums = true, bool loadTracks = true, bool fetchApi = true)
    {
        CancellationToken cancellationToken = InitNavigationCancellation();

        Stopwatch stopwatch = new();
        stopwatch.Start();

        await LoadArtistAsync(artistId);

        if (loadAlbums)
            await LoadAlbumsAsync(artistId);

        if (cancellationToken.IsCancellationRequested)
            return;

        if (loadTracks)
            await LoadTracksAsync(artistId);

        if (cancellationToken.IsCancellationRequested)
            return;

        if (loadAlbums && loadTracks)
            await UpdateStatisticsIfNeededAsync();

        if (cancellationToken.IsCancellationRequested)
            return;

        if (fetchApi)
            await GetDataFromApiAsync();

        if (cancellationToken.IsCancellationRequested)
            return;

        await CalculatePictureDominantColorAsync();

        await InitializeTagsAsync();

        stopwatch.Stop();
        _logger.LogInformation("Artist {ArtistId} loaded in {ElapsedMilliseconds} ms (albums: {LoadAlbums}, tracks: {LoadTracks}, api: {FetchApi})",
                                artistId, stopwatch.ElapsedMilliseconds, loadAlbums, loadTracks, fetchApi);
    }

    public void OnNavigatedFrom()
    {
        InitNavigationCancellation();
        Backdrop = null;
    }


    private CancellationToken InitNavigationCancellation()
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();
        _navigationCts = new CancellationTokenSource();
        return _navigationCts.Token;
    }


    private async Task LoadArtistAsync(long artistId)
    {
        ArtistDto? artist = await _dataLoader.LoadArtistAsync(artistId);
        if (artist != null)
        {
            Artist = artist;
            IsNew = Artist.CreatDate > DateTime.UtcNow.AddDays(-_appOptions.ArtistRecentThresholdDays);

            LoadBackdrop();
        }
    }

    public void LoadPicture()
    {
        try
        {
            Picture = _pictureService.LoadPicture(Artist.Name);

            if (!_pictureService.PictureExists(Artist.Name))
                return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load picture for {ArtistName}", Artist.Name);
        }
    }


    public void LoadBackdrop()
    {
        CancellationToken token = _navigationCts.Token;

        _backdropLoader.LoadBackdrop(Artist.Name, (BitmapImage? backdropImage) =>
        {
            if (!token.IsCancellationRequested)
                Backdrop = backdropImage;
        });
    }

    private async Task CalculatePictureDominantColorAsync()
    {
        if (Artist.PictureDominantColor.HasValue)
            return;

        if (Interlocked.Exchange(ref _dominantColorCalculating, 1) == 1)
            return;

        try
        {
            string filePath = _pictureService.GetPictureFilePath(Artist.Name);
            long? packed = await _dominantColorCalculator.CalculateAsync(filePath);

            Artist.PictureDominantColor = packed;

            if (packed.HasValue)
                await _editService.UpdatePictureDominantColorAsync(Artist.Id, packed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load picture or dominant color for {ArtistName}", Artist.Name);
        }
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

    private async Task InitializeTagsAsync()
    {
        List<string> artistTags = Artist.GetTags();

        EditableTags.Clear();
        foreach (string tag in artistTags)
            EditableTags.Add(tag);

        EditableTags.CollectionChanged -= OnTagsCollectionChanged;
        EditableTags.CollectionChanged += OnTagsCollectionChanged;

        SuggestedTags.Clear();
        List<string> suggestedTags = await _tagsProvider.GetTagsAsync();
        foreach (string tag in suggestedTags)
        {
            SuggestedTags.Add(tag);
        }
    }

    private async void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Artist.TagsAsString = string.Join(",", EditableTags);
        await _editService.UpdateTagsAsync(Artist.Id, EditableTags);

        Debug.WriteLine(Artist.TagsAsString);
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
        CancellationToken token = _navigationCts.Token;

        bool updated = await _apiService.GetAndUpdateArtistDataAsync(Artist, _pictureService, _backdropPicture);

        if (!updated || token.IsCancellationRequested)
            return;

        if (_pictureService.PictureExists(Artist.Name))
            LoadPicture();

        ArtistDto? refreshedArtist = await _dataLoader.ReloadArtistAsync(Artist.Id);
        if (refreshedArtist != null)
            Artist = refreshedArtist;

        Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
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
            List<TrackDto> tracks = _tracks.ToList();
            TracksRandomizer.Randomize(tracks);
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
        CancellationToken token = _navigationCts.Token;

        BitmapImage? newPicture = await _pictureService.SelectAndSavePictureAsync(Artist.Name);

        if (newPicture is null || token.IsCancellationRequested)
            return;

        Artist.PictureDominantColor = null;
        await CalculatePictureDominantColorAsync();

        Picture = newPicture;

        Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Picture));
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