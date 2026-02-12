using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Infrastructure.Translate;
using Rok.Services.Player;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Album;

public partial class AlbumViewModel : ObservableObject
{
    private readonly NavigationService _navigationService;
    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly ILogger<AlbumViewModel> _logger;
    private readonly IBackdropLoader _backdropLoader;
    private readonly IDialogService _dialogService;
    private readonly IAppOptions _appOptions;

    private readonly AlbumDataLoader _dataLoader;
    private readonly TagsProvider _tagsProvider;
    private readonly AlbumPictureService _pictureService;
    private readonly AlbumApiService _apiService;
    private readonly AlbumStatisticsService _statisticsService;
    private readonly AlbumEditService _editService;
    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];
    private IEnumerable<TrackDto>? _tracks = null;

    public AlbumDto Album { get; private set; } = new();
    public IPlaylistMenuService PlaylistMenuService { get; }


    public ObservableCollection<string> Tags { get; set; } = new();

    public ObservableCollection<string> SuggestedTags { get; set; } = new();

    public bool IsFavorite
    {
        get => Album.IsFavorite;
        set => SetProperty(Album.IsFavorite, value, Album, (album, val) => album.IsFavorite = val);
    }

    [ObservableProperty]
    public partial BitmapImage? Backdrop { get; set; }

    public override string ToString()
    {
        if (Album == null)
            return string.Empty;
        return Album.Name;
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
        set => SetProperty(ref _picture, value);
    }



    public AlbumViewModel(
        IBackdropLoader backdropLoader,
        ILastFmClient lastFmClient,
        NavigationService navigationService,
        IPlayerService playerService,
        ResourceLoader resourceLoader,
        AlbumDataLoader dataLoader,
        TagsProvider tagsDataLoader,
        AlbumPictureService pictureService,
        AlbumApiService apiService,
        AlbumStatisticsService statisticsService,
        AlbumEditService editService,
        IAppOptions appOptions,
        IDialogService dialogService,
        IPlaylistMenuService playlistMenuService,
        ILogger<AlbumViewModel> logger)
    {
        _backdropLoader = Guard.Against.Null(backdropLoader);
        _navigationService = Guard.Against.Null(navigationService);
        _playerService = Guard.Against.Null(playerService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _dataLoader = Guard.Against.Null(dataLoader);
        _tagsProvider = Guard.Against.Null(tagsDataLoader);
        _pictureService = Guard.Against.Null(pictureService);
        _apiService = Guard.Against.Null(apiService);
        _statisticsService = Guard.Against.Null(statisticsService);
        _editService = Guard.Against.Null(editService);
        _appOptions = Guard.Against.Null(appOptions);
        _dialogService = Guard.Against.Null(dialogService);
        PlaylistMenuService = Guard.Against.Null(playlistMenuService);
        _logger = Guard.Against.Null(logger);
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
        await InitializeTagsAsync();

        stopwatch.Stop();
        _logger.LogInformation("Album {AlbumId} loaded in {ElapsedMilliseconds} ms", albumId, stopwatch.ElapsedMilliseconds);
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

    private async Task InitializeTagsAsync()
    {
        List<string> albumTags = Album.GetTags();

        Tags.Clear();
        foreach (string tag in albumTags)
            Tags.Add(tag);

        Tags.CollectionChanged -= OnTagsCollectionChanged;
        Tags.CollectionChanged += OnTagsCollectionChanged;

        SuggestedTags.Clear();
        List<string> suggestedTags = await _tagsProvider.GetTagsAsync();
        foreach (string tag in suggestedTags)
        {
            SuggestedTags.Add(tag);
        }
    }

    private async void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Album.TagsAsString = string.Join(",", Tags);
        await _editService.UpdateTagsAsync(Album.Id, Tags);

        Debug.WriteLine(Album.TagsAsString);
    }

    private async Task UpdateStatisticsIfNeededAsync()
    {
        bool updated = await _statisticsService.UpdateIfNeededAsync(Album, Tracks);
        if (updated)
        {
            Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        }
    }


    [RelayCommand]
    private void AlbumOpen()
    {
        _navigationService.NavigateToAlbum(Album.Id);
    }

    [RelayCommand]
    private void ArtistOpen()
    {
        if (Album.ArtistId.HasValue)
            _navigationService.NavigateToArtist(Album.ArtistId.Value);
    }

    [RelayCommand]
    private void GenreOpen()
    {
        if (Album.GenreId.HasValue)
            _navigationService.NavigateToGenre(Album.GenreId.Value);
    }

    [RelayCommand]
    private async Task ListenAsync()
    {
        if (_tracks == null)
            await LoadTracksAsync(Album.Id);

        if (_tracks?.Any() == true)
            _playerService.LoadPlaylist(_tracks.ToList());
    }

    [RelayCommand]
    private async Task AlbumFavoriteAsync()
    {
        bool newFavoriteState = !Album.IsFavorite;
        await _editService.UpdateFavoriteAsync(Album, newFavoriteState);

        OnPropertyChanged(nameof(IsFavorite));
        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
    }

    [RelayCommand]
    private async Task GetDataFromApiAsync()
    {
        bool updated = await _apiService.GetAndUpdateAlbumDataAsync(Album, _pictureService);

        if (!updated)
            return;

        if (_pictureService.PictureExists(Album.AlbumPath))
            LoadPicture();

        AlbumDto? refreshedAlbum = await _dataLoader.ReloadAlbumAsync(Album.Id);
        if (refreshedAlbum != null)
            Album = refreshedAlbum;

        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
    }

    [RelayCommand]
    private async Task EditAlbumAsync()
    {
        bool updated = await _editService.EditAlbumAsync(Album);

        if (!updated)
            return;

        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        OnPropertyChanged(nameof(Album));
    }

    [RelayCommand]
    private async Task SelectPictureAsync()
    {
        BitmapImage? newPicture = await _pictureService.SelectAndSavePictureAsync(Album.AlbumPath);

        if (newPicture is null)
            return;

        if (Rok.App.MainWindow.DispatcherQueue is { } dq)
            dq.TryEnqueue(() => Picture = newPicture);
        else
            Picture = newPicture;

        Messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Picture));
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
        if (string.IsNullOrEmpty(Album.Biography))
            return;

        string? rawLanguage = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
        string language = TranslateService.NormalizeLanguageForLibreTranslate(rawLanguage, "fr");

        await _dialogService.ShowTextAsync(Album.Name, Album.Biography, showTranslateButton: _appOptions.NovaApiEnabled, language);
    }
}