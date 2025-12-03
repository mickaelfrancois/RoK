using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Tracks.Command;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Randomizer;
using Rok.Infrastructure.NovaApi;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Tracks;
using System.IO;
using Windows.Storage;

namespace Rok.Logic.ViewModels.Artists;

public partial class ArtistViewModel : ObservableObject
{
    private static BitmapImage FallbackPicture => new(new Uri("ms-appx:///Assets/artistFallback.png"));

    public ArtistDto Artist { get; private set; } = new();

    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];

    public RangeObservableCollection<AlbumViewModel> Albums { get; set; } = [];

    private IEnumerable<TrackDto>? _tracks = null;

    public bool LastFmPageAvailable { get; set; }

    public bool IsFavorite
    {
        get => Artist.IsFavorite;
        set
        {
            if (Artist.IsFavorite != value)
            {
                Artist.IsFavorite = value;
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
                    label += _resourceLoader.GetString("live");
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
            _picture = value;
            OnPropertyChanged(nameof(Picture));
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

    public AsyncRelayCommand ListenCommand { get; }
    public RelayCommand ArtistOpenCommand { get; }
    public RelayCommand ArtistsByCountryOpenCommand { get; }
    public RelayCommand GenreOpenCommand { get; }
    public AsyncRelayCommand ArtistFavoriteCommand { get; }
    public AsyncRelayCommand SelectPictureCommand { get; }
    public AsyncRelayCommand OpenOfficielSiteCommand { get; }
    public RelayCommand OpenLastFmPageCommand { get; }

    private readonly IMediator _mediator;
    private readonly IArtistPicture _artistPicture;
    private readonly NavigationService _navigationService;
    private readonly ResourceLoader _resourceLoader;
    private readonly ILogger<ArtistViewModel> _logger;
    private readonly IPlayerService _playerService;
    private readonly INovaApiService _novaApiService;
    private readonly ILastFmClient _lastFmClient;
    private readonly IBackdropLoader _backdropLoader;
    private readonly BackdropPicture _backdropPicture;

    public override string ToString() => Artist?.Name ?? string.Empty;



    public ArtistViewModel(BackdropPicture backdropPicture, IBackdropLoader backdropLoader, ILastFmClient lastFmClient, INovaApiService novaApiService, IMediator mediator, IPlayerService playerService, IArtistPicture artistPicture, NavigationService navigationService, ResourceLoader resourceLoader, ILogger<ArtistViewModel> logger)
    {
        _backdropPicture = Guard.Against.Null(backdropPicture);
        _backdropLoader = Guard.Against.Null(backdropLoader);
        _lastFmClient = Guard.Against.Null(lastFmClient);
        _novaApiService = Guard.Against.Null(novaApiService);
        _mediator = Guard.Against.Null(mediator);
        _playerService = Guard.Against.Null(playerService);
        _artistPicture = Guard.Against.Null(artistPicture);
        _navigationService = Guard.Against.Null(navigationService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _logger = Guard.Against.Null(logger);

        GenreOpenCommand = new RelayCommand(() => { });
        ListenCommand = new AsyncRelayCommand(ListenAsync);
        ArtistsByCountryOpenCommand = new RelayCommand(() => { });
        ArtistFavoriteCommand = new AsyncRelayCommand(UpdateFavoriteStateAsync);
        ArtistOpenCommand = new RelayCommand(ArtistOpen);
        SelectPictureCommand = new AsyncRelayCommand(SelectPictureAsync);
        OpenOfficielSiteCommand = new AsyncRelayCommand(OpenOfficialSiteAsync);
        OpenLastFmPageCommand = new RelayCommand(OpenLastFmPage);
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
                                artistId,
                                stopwatch.ElapsedMilliseconds,
                                loadAlbums,
                                loadTracks,
                                fetchApi);

        await CheckLastFmUrlAsync();
    }


    private async Task LoadArtistAsync(long artistId)
    {
        Result<ArtistDto> resultArtist = await _mediator.SendMessageAsync(new GetArtistByIdQuery(artistId));
        if (resultArtist.IsSuccess)
        {
            Artist = resultArtist.Value!;

            LoadBackdrop();
        }
    }


    public void LoadPicture()
    {
        try
        {
            if (_artistPicture.PictureFileExists(Artist.Name))
            {
                string filePath = _artistPicture.GetPictureFile(Artist.Name);
                Picture = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
            else
            {
                Picture = FallbackPicture;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load picture for artist: {ArtistName}", Artist.Name);
            Picture = FallbackPicture;
        }
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
        IEnumerable<AlbumDto> albums = await _mediator.SendMessageAsync(new GetAlbumsByArtistIdQuery(artistId));
        Albums.AddRange(AlbumViewModelMap.CreateViewModels(albums.ToList()));
    }


    private async Task LoadTracksAsync(long artistId)
    {
        _tracks = await _mediator.SendMessageAsync(new GetTracksByArtistIdQuery(artistId));
        if (_tracks != null)
            Tracks.AddRange(TrackViewModelMap.CreateViewModels(_tracks.ToList()));

        OnPropertyChanged(nameof(DurationTotal));
    }


    private bool NeedUpdateStatistics()
    {
        bool mustUpdate = Artist.AlbumCount != Albums.Count(c => !c.Album.IsLive && !c.Album.IsCompilation && !c.Album.IsBestOf);
        mustUpdate |= Artist.LiveCount != Albums.Count(c => c.Album.IsLive);
        mustUpdate |= Artist.CompilationCount != Albums.Count(c => c.Album.IsCompilation);
        mustUpdate |= Artist.BestofCount != Albums.Count(c => c.Album.IsBestOf);
        mustUpdate |= Artist.TrackCount != Tracks.Count;

        return mustUpdate;
    }


    private async Task UpdateStatisticsIfNeededAsync()
    {
        if (NeedUpdateStatistics())
        {
            Artist.TrackCount = Tracks.Count;
            Artist.AlbumCount = Albums.Count(c => !c.Album.IsLive && !c.Album.IsCompilation && !c.Album.IsBestOf);
            Artist.BestofCount = Albums.Count(c => c.Album.IsBestOf);
            Artist.LiveCount = Albums.Count(c => c.Album.IsLive);
            Artist.CompilationCount = Albums.Count(c => c.Album.IsCompilation);
            Artist.TotalDurationSeconds = Tracks.Sum(c => c.Track.Duration);

            int yearMini = Albums
                .Where(a => a.Album.Year.HasValue && !a.Album.IsCompilation && !a.Album.IsLive && !a.Album.IsBestOf)
                .Select(a => a.Album.Year!.Value)
                .DefaultIfEmpty(0)
                .Min();

            int yearMaxi = Albums
                .Where(a => a.Album.Year.HasValue && !a.Album.IsCompilation && !a.Album.IsLive && !a.Album.IsBestOf)
                .Select(a => a.Album.Year!.Value)
                .DefaultIfEmpty(0)
                .Max();
            Artist.YearMini = yearMini == 0 ? null : yearMini;
            Artist.YearMaxi = yearMaxi == 0 ? null : yearMaxi;

            UpdateArtistStatisticsCommand command = new(Artist.Id)
            {
                TrackCount = Artist.TrackCount,
                AlbumCount = Artist.AlbumCount,
                BestOfCount = Artist.BestofCount,
                LiveCount = Artist.LiveCount,
                CompilationCount = Artist.CompilationCount,
                TotalDurationSeconds = Artist.TotalDurationSeconds,
                YearMini = Artist.YearMini,
                YearMaxi = Artist.YearMaxi
            };

            await _mediator.SendMessageAsync(command);
            Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
        }
    }


    private void ArtistOpen()
    {
        _navigationService.NavigateToArtist(Artist.Id);
    }


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


    private async Task UpdateFavoriteStateAsync()
    {
        bool isFavorite = !Artist.IsFavorite;

        await _mediator.SendMessageAsync(new UpdateArtistFavoriteCommand(Artist.Id, isFavorite));

        Artist.IsFavorite = isFavorite;
        OnPropertyChanged(nameof(IsFavorite));

        Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
    }


    private async Task OpenOfficialSiteAsync()
    {
        try
        {
            string? url = Artist?.OfficialSiteUrl;
            if (url == null)
                url = Artist?.WikipediaUrl;

            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url.Trim();
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                _logger.LogWarning("Invalid URL for artist {ArtistName}: {Url}", Artist?.Name, url);
                return;
            }

            bool success = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!success)
            {
                _logger.LogWarning("Unable to open URL {Url} for artist {ArtistName}", url, Artist?.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while opening URL for artist {ArtistName}", Artist?.Name);
        }
    }


    private async Task SelectPictureAsync()
    {
        StorageFile? file = await ImagePickerService.PickAsync();
        if (file is null)
            return;

        try
        {
            string destinationPath = _artistPicture.GetPictureFile(Artist.Name);
            string? folderPath = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);

                await file.CopyAsync(folder, Path.GetFileName(destinationPath), NameCollisionOption.ReplaceExisting);
            }

            await SetPictureFromPathAsync(destinationPath);

            Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Picture));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save selected picture for artist: {ArtistName}", Artist.Name);
        }
    }


    private async Task SetPictureFromPathAsync(string path)
    {
        StorageFile sf = await StorageFile.GetFileFromPathAsync(path);
        using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream = await sf.OpenReadAsync();

        BitmapImage bitmap = new();
        await bitmap.SetSourceAsync(stream);

        // Affecter sur le thread UI
        if (Rok.App.MainWindow.DispatcherQueue is { } dq)
            dq.TryEnqueue(() => Picture = bitmap);
        else
            Picture = bitmap;
    }


    private async Task GetDataFromApiAsync()
    {
        if (string.IsNullOrEmpty(Artist.Name))
            return;

        if (!NovaApiService.IsApiRetryAllowed(Artist.GetMetaDataLastAttempt))
            return;

        await _mediator.SendMessageAsync(new UpdateArtistGetMetaDataLastAttemptCommand(Artist.Id));

        ApiArtistModel? artistApi = await _novaApiService.GetArtistAsync(Artist.Name);
        if (artistApi != null)
        {
            if (string.IsNullOrEmpty(artistApi.MusicBrainzID) == false)
            {
                if (_artistPicture.PictureFileExists(Artist.Name) == false)
                {
                    await _novaApiService.GetArtistPictureAsync(artistApi.MusicBrainzID, "artists", _artistPicture.GetPictureFile(Artist.Name));
                    LoadPicture();
                }

                if (_backdropPicture.HasBackdrops(Artist.Name) == false)
                {
                    await _novaApiService.GetArtistBackdropsAsync(artistApi.MusicBrainzID, artistApi.FanartsCount, _backdropPicture.GetArtistPictureFolder(Artist.Name));
                    OnPropertyChanged(nameof(Backdrop));
                }
            }

            if (CompareArtistFromApi(artistApi))
                await UpdateArtistFromApiAsync(artistApi);
        }
    }


    private bool CompareArtistFromApi(ApiArtistModel artistApi)
    {
        if (Artist.TwitterUrl.AreDifferents(artistApi.Twitter)) return true;
        if (Artist.OfficialSiteUrl.AreDifferents(artistApi.Website)) return true;
        if (Artist.FacebookUrl.AreDifferents(artistApi.Facebook)) return true;
        if (Artist.BornYear.AreDifferents(artistApi.BornYear)) return true;
        if (Artist.Disbanded != artistApi.IsDisbanded) return true;
        if (Artist.DiedYear.AreDifferents(artistApi.DiedYear)) return true;
        if (Artist.FormedYear.AreDifferents(artistApi.FormedYear)) return true;
        if (Artist.Gender.AreDifferents(artistApi.Gender)) return true;
        if (Artist.Mood.AreDifferents(artistApi.Mood)) return true;
        if (Artist.Style.AreDifferents(artistApi.Style)) return true;
        if (Artist.Biography.AreDifferents(artistApi.GetBiography(LanguageHelpers.GetCurrentLanguage()))) return true;

        return false;
    }


    private async Task UpdateArtistFromApiAsync(ApiArtistModel artistApi)
    {
        _logger.LogTrace("Patch artist '{Name}' from API response.", Artist.Name);

        PatchArtistCommand patchArtistCommand = new()
        {
            Id = Artist.Id,
            WikipediaUrl = artistApi.Wikipedia,
            OfficialSiteUrl = artistApi.Website,
            FacebookUrl = artistApi.Facebook,
            TwitterUrl = artistApi.Twitter,
            MusicBrainzID = artistApi.MusicBrainzID,
            Disbanded = artistApi.IsDisbanded,
            BornYear = artistApi.BornYear,
            DiedYear = artistApi.DiedYear,
            FormedYear = artistApi.FormedYear,
            Gender = artistApi.Gender,
            Mood = artistApi.Mood,
            Style = artistApi.Style,
            Biography = artistApi.GetBiography(LanguageHelpers.GetCurrentLanguage()),
            NovaUid = artistApi.ID?.ToString()
        };

        await _mediator.SendMessageAsync(patchArtistCommand);
        Result<ArtistDto> artistResult = await _mediator.SendMessageAsync(new GetArtistByIdQuery(Artist.Id));
        Artist = artistResult.Value!;

        Messenger.Send(new ArtistUpdateMessage(Artist.Id, ActionType.Update));
    }


    private async Task CheckLastFmUrlAsync()
    {
        LastFmPageAvailable = await _lastFmClient.IsArtistPageAvailableAsync(Artist.Name);

        OnPropertyChanged(nameof(LastFmPageAvailable));
    }


    private void OpenLastFmPage()
    {
        if (LastFmPageAvailable)
        {
            string artistPageUrl = _lastFmClient.GetArtistPageUrl(Artist.Name);

            Uri uri = new(artistPageUrl);
            _ = Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }
}
