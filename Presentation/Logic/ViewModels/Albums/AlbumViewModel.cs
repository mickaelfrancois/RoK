using Microsoft.UI.Xaml.Controls;
using Rok.Application.Features.Albums.Command;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Tracks.Command;
using Rok.Application.Features.Tracks.Query;
using Rok.Dialogs;
using Rok.Infrastructure.NovaApi;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Tracks;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rok.Logic.ViewModels.Albums;

public partial class AlbumViewModel : ObservableObject
{
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

    public BitmapImage? _backdrop = null;
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
    public AsyncRelayCommand EditAlbumCommand { get; }
    public RelayCommand OpenLastFmPageCommand { get; }

    private readonly IMediator _mediator;
    private readonly NavigationService _navigationService;
    private readonly IAlbumPicture _albumPicture;
    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly ILogger<AlbumViewModel> _logger;
    private readonly INovaApiService _novaApiService;
    private readonly ILastFmClient _lastFmClient;
    private readonly IBackdropLoader _backdropLoader;

    public override string ToString()
    {
        if (Album == null)
            return string.Empty;
        return Album.Name;
    }


    public AlbumViewModel(IBackdropLoader backdropLoader, ILastFmClient lastFmClient, INovaApiService novaApiService, IMediator mediator, NavigationService navigationService, IPlayerService playerService, IAlbumPicture albumPicture, ResourceLoader resourceLoader, ILogger<AlbumViewModel> logger)
    {
        _backdropLoader = Guard.Against.Null(backdropLoader);
        _lastFmClient = Guard.Against.Null(lastFmClient);
        _novaApiService = Guard.Against.Null(novaApiService);
        _mediator = Guard.Against.Null(mediator);
        _navigationService = Guard.Against.Null(navigationService);
        _playerService = Guard.Against.Null(playerService);
        _albumPicture = Guard.Against.Null(albumPicture);
        _resourceLoader = Guard.Against.Null(resourceLoader);
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
        _tracks = await _mediator.SendMessageAsync(new GetTracksByAlbumIdQuery(albumId));
        Tracks.AddRange(TrackViewModelMap.CreateViewModels(_tracks.ToList()));
    }


    private async Task LoadAlbumAsync(long albumId)
    {
        Result<AlbumDto> resultAlbum = await _mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId));
        if (resultAlbum.IsSuccess)
        {
            Album = resultAlbum.Value!;

            LoadBackrop();
        }
    }


    public void LoadPicture()
    {
        try
        {
            if (_albumPicture.PictureFileExists(Album.AlbumPath))
            {
                string filePath = _albumPicture.GetPictureFile(Album.AlbumPath);
                Picture = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
            else
            {
                Picture = (BitmapImage)App.Current.Resources["albumCoverFallback"];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load picture for album: {AlbumName}", Album.Name);
            Picture = (BitmapImage)App.Current.Resources["albumCoverFallback"]; ;
        }
    }


    public void LoadBackrop()
    {
        _backdropLoader.LoadBackdrop(Album.ArtistName, (BitmapImage? backdropImage) =>
        {
            Backdrop = backdropImage;
        });
    }


    private bool NeedUpdateStatistics()
    {
        bool mustUpdate = Album.TrackCount != Tracks.Count;
        mustUpdate |= Album.Duration != Tracks.Sum(c => c.Track.Duration);

        return mustUpdate;
    }


    private async Task UpdateStatisticsIfNeededAsync()
    {
        if (NeedUpdateStatistics())
        {
            Album.TrackCount = Tracks.Count;
            Album.Duration = Tracks.Sum(c => c.Track.Duration);

            UpdateAlbumStatisticsCommand command = new(Album.Id)
            {
                TrackCount = Album.TrackCount,
                Duration = Album.Duration,
            };

            await _mediator.SendMessageAsync(command);
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
        bool isFavorite = !Album.IsFavorite;

        await _mediator.SendMessageAsync(new UpdateAlbumFavoriteCommand(Album.Id, isFavorite));

        Album.IsFavorite = isFavorite;
        OnPropertyChanged(nameof(IsFavorite));

        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
    }


    private async Task GetDataFromApiAsync()
    {
        if (string.IsNullOrEmpty(Album.Name) || string.IsNullOrEmpty(Album.ArtistName))
            return;

        if (!NovaApiService.IsApiRetryAllowed(Album.GetMetaDataLastAttempt))
            return;

        await _mediator.SendMessageAsync(new UpdateAlbumGetMetaDataLastAttemptCommand(Album.Id));

        ApiAlbumModel? albumApi = await _novaApiService.GetAlbumAsync(Album.Name, Album.ArtistName);
        if (albumApi != null)
        {
            if (_albumPicture.PictureFileExists(Album.AlbumPath) == false)
            {
                if (string.IsNullOrEmpty(albumApi.MusicBrainzID) == false)
                {
                    await _novaApiService.GetAlbumPicturesAsync(albumApi.MusicBrainzID, _albumPicture.GetPictureFile(Album.AlbumPath));
                    LoadPicture();
                }
            }

            if (CompareAlbumFromApi(albumApi))
                await UpdateAlbumFromApiAsync(albumApi);
        }
    }


    private bool CompareAlbumFromApi(ApiAlbumModel albumApi)
    {
        if (Album.Label.AreDifferents(albumApi.Label)) return true;
        if (Album.Sales.AreDifferents(albumApi.Sales)) return true;
        if (Album.Mood.AreDifferents(albumApi.Mood)) return true;
        if (Album.MusicBrainzID.AreDifferents(albumApi.MusicBrainzID)) return true;
        if (Album.Speed.AreDifferents(albumApi.Speed)) return true;
        if (Album.ReleaseDate != albumApi.ReleaseDate) return true;
        if (Album.ReleaseFormat.AreDifferents(albumApi.ReleaseFormat)) return true;
        if (Album.Wikipedia.AreDifferents(albumApi.Wikipedia)) return true;
        if (Album.Theme.AreDifferents(albumApi.Theme)) return true;

        return false;
    }


    private async Task UpdateAlbumFromApiAsync(ApiAlbumModel albumApi)
    {
        _logger.LogTrace("Patch album '{Name}' from API response.", Album.Name);

        PatchAlbumCommand patchAlbumCommand = new()
        {
            Id = Album.Id,
            Label = new PatchField<string>(albumApi.Label),
            Sales = new PatchField<string>(albumApi.Sales),
            Mood = new PatchField<string>(albumApi.Mood),
            MusicBrainzID = new PatchField<string>(albumApi.MusicBrainzID),
            Speed = new PatchField<string>(albumApi.Speed),
            ReleaseDate = new PatchField<DateTime?>(albumApi.ReleaseDate),
            ReleaseFormat = new PatchField<string>(albumApi.ReleaseFormat),
            Wikipedia = new PatchField<string>(albumApi.Wikipedia),
            Theme = new PatchField<string>(albumApi.Theme)
        };

        await _mediator.SendMessageAsync(patchAlbumCommand);
        Result<AlbumDto> albumResult = await _mediator.SendMessageAsync(new GetAlbumByIdQuery(Album.Id));
        Album = albumResult.Value!;

        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
    }


    private async Task EditAlbumAsync()
    {
        EditAlbumDialog dialog = new()
        {
            IsBestOf = Album.IsBestOf,
            IsLive = Album.IsLive,
            IsCompilation = Album.IsCompilation,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        PatchAlbumCommand patchAlbumCommand = new()
        {
            Id = Album.Id,
            IsBestOf = new PatchField<bool>(dialog.IsBestOf),
            IsLive = new PatchField<bool>(dialog.IsLive),
            IsCompilation = new PatchField<bool>(Album.IsCompilation)
        };

        await _mediator.SendMessageAsync(patchAlbumCommand);

        Album.IsBestOf = dialog.IsBestOf;
        Album.IsLive = dialog.IsLive;
        Album.IsCompilation = dialog.IsCompilation;

        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        OnPropertyChanged(nameof(Album));
    }


    private async Task SelectPictureAsync()
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");

        InitializeWithWindow.Initialize(openPicker, Rok.App.MainWindowHandle);

        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file is null)
            return;

        try
        {
            string destinationPath = _albumPicture.GetPictureFile(Album.AlbumPath);
            string? folderPath = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);

                await file.CopyAsync(folder, Path.GetFileName(destinationPath), NameCollisionOption.ReplaceExisting);
            }

            await SetPictureFromPathAsync(destinationPath);

            Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Picture));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save selected picture for album: {AlbumName}", Album.Name);
        }
    }


    private async Task SetPictureFromPathAsync(string path)
    {
        StorageFile sf = await StorageFile.GetFileFromPathAsync(path);
        using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream = await sf.OpenReadAsync();

        BitmapImage bitmap = new();
        await bitmap.SetSourceAsync(stream);

        if (Rok.App.MainWindow.DispatcherQueue is { } dq)
            dq.TryEnqueue(() => Picture = bitmap);
        else
            Picture = bitmap;
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

