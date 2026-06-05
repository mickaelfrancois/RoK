using System.Collections.Specialized;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.ListeningEvents;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Player;
using Rok.Application.Services.Filters;
using Rok.Application.Services.Grouping;
using Rok.Commons;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Common;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Album;

public partial class AlbumViewModel : ObservableObject, IFilterableAlbum, IGroupableAlbum
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
    private readonly IAlbumApiService _apiService;
    private readonly AlbumStatisticsService _statisticsService;
    private readonly AlbumEditService _editService;
    private readonly IDominantColorCalculator _dominantColorCalculator;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;

    private CancellationTokenSource _navigationCts = new();

    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];
    private IEnumerable<TrackDto>? _tracks = null;

    public AlbumDto Album { get; private set; } = new();
    public IPlaylistMenuService PlaylistMenuService { get; }

    public ObservableCollection<string> EditableTags { get; set; } = new();

    public ObservableCollection<string> SuggestedTags { get; set; } = new();

    public bool IsFavorite
    {
        get => Album.IsFavorite;
        set => SetProperty(Album.IsFavorite, value, Album, (album, val) => album.IsFavorite = val);
    }

    public long? GenreId => Album.GenreId;

    public int ListenCount => Album.ListenCount;

    public bool IsLive => Album.IsLive;

    public bool IsBestOf => Album.IsBestOf;

    public bool IsCompilation => Album.IsCompilation;

    public bool IsGenreFavorite => Album.IsGenreFavorite;

    public bool IsArtistFavorite => Album.IsArtistFavorite;

    public bool IsAlbumFavorite => Album.IsFavorite;

    public DateTime? ReleaseDate => Album.ReleaseDate;

    [ObservableProperty]
    public partial Windows.UI.Color DominantColor { get; set; }
    private int _dominantColorCalculating = 0;

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool ShowNewBadge { get; set; }

    [ObservableProperty]
    public partial bool ShowAnniversaryBadge { get; set; }

    [ObservableProperty]
    public partial string AnniversaryTooltip { get; set; } = string.Empty;

    public ListeningStatsViewModel ListeningStats { get; } = new();

    public List<string> Tags => Album.GetTags();

    string IGroupableAlbum.Name => Album.Name;
    string IGroupableAlbum.ArtistName => Album.ArtistName;
    int? IGroupableAlbum.Year => Album.Year;
    string? IGroupable.CountryCode => Album.CountryCode;
    DateTime IGroupable.CreatDate => Album.CreatDate;
    DateTime? IGroupable.LastListen => Album.LastListen;

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
            var time = TimeSpan.FromSeconds(Album.Duration);
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
        IAlbumApiService apiService,
        AlbumStatisticsService statisticsService,
        IDominantColorCalculator dominantColorCalculator,
        AlbumEditService editService,
        IAppOptions appOptions,
        IDialogService dialogService,
        IPlaylistMenuService playlistMenuService,
        IMessenger messenger,
        TimeProvider timeProvider,
        ILogger<AlbumViewModel> logger)
    {
        _backdropLoader = Guard.NotNull(backdropLoader);
        _navigationService = Guard.NotNull(navigationService);
        _playerService = Guard.NotNull(playerService);
        _resourceLoader = Guard.NotNull(resourceLoader);
        _dataLoader = Guard.NotNull(dataLoader);
        _tagsProvider = Guard.NotNull(tagsDataLoader);
        _pictureService = Guard.NotNull(pictureService);
        _apiService = Guard.NotNull(apiService);
        _messenger = Guard.NotNull(messenger);
        _statisticsService = Guard.NotNull(statisticsService);
        _dominantColorCalculator = Guard.NotNull(dominantColorCalculator);
        _editService = Guard.NotNull(editService);
        _appOptions = Guard.NotNull(appOptions);
        _dialogService = Guard.NotNull(dialogService);
        PlaylistMenuService = Guard.NotNull(playlistMenuService);
        _timeProvider = Guard.NotNull(timeProvider);
        _logger = Guard.NotNull(logger);
    }

    public void SetData(AlbumDto album)
    {
        Album = Guard.NotNull(album);

        IsNew = Album.CreatDate > DateTime.UtcNow.AddDays(-_appOptions.AlbumRecentThresholdDays);

        UpdateAnniversaryBadge();
        UpdateDominantColor();
    }

    private void UpdateDominantColor()
    {
        DominantColor = Album.PictureDominantColor.HasValue
            ? ColorHelper.FromArgb(Album.PictureDominantColor.Value)
            : default;
    }

    public async Task LoadDataAsync(long albumId)
    {
        CancellationToken cancellationToken = InitNavigationCancellation();

        Stopwatch stopwatch = new();
        stopwatch.Start();

        bool albumLoaded = await LoadAlbumAsync(albumId);

        if (!albumLoaded || cancellationToken.IsCancellationRequested)
            return;

        await LoadTracksAsync(albumId, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            return;

        await UpdateStatisticsIfNeededAsync();

        if (cancellationToken.IsCancellationRequested)
            return;

        await LoadListeningStatsAsync();

        if (cancellationToken.IsCancellationRequested)
            return;

        await GetDataFromApiAsync();

        if (cancellationToken.IsCancellationRequested)
            return;

        await InitializeTagsAsync();

        if (cancellationToken.IsCancellationRequested)
            return;

        await CalculatePictureDominantColorAsync();

        stopwatch.Stop();
        _logger.LogInformation("Album {AlbumId} loaded in {ElapsedMilliseconds} ms", albumId, stopwatch.ElapsedMilliseconds);
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

    private async Task LoadTracksAsync(long albumId, CancellationToken cancellationToken)
    {
        List<TrackViewModel> tracks = await _dataLoader.LoadTracksAsync(albumId);

        if (cancellationToken.IsCancellationRequested)
            return;

        _tracks = tracks.Select(t => t.Track);
        Tracks.InitWithAddRange(tracks);
    }

    private async Task<bool> LoadAlbumAsync(long albumId)
    {
        AlbumDto? album = await _dataLoader.LoadAlbumAsync(albumId);
        if (album == null)
            return false;

        Album = album;
        IsNew = Album.CreatDate > DateTime.UtcNow.AddDays(-_appOptions.AlbumRecentThresholdDays);

        UpdateAnniversaryBadge();
        UpdateDominantColor();
        LoadBackrop();

        return true;
    }

    private void UpdateAnniversaryBadge()
    {
        int? age = AlbumsFilter.GetAnniversaryAge(Album.ReleaseDate, DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));

        ShowAnniversaryBadge = age is not null;

        if (age is not null)
        {
            string resourceKey = age == 1 ? "albumAnniversaryTooltipOne" : "albumAnniversaryTooltipMany";
            AnniversaryTooltip = string.Format(_resourceLoader.GetString(resourceKey), age);
        }
    }

    private async Task LoadListeningStatsAsync()
    {
        HashSet<long> listenedTrackIds = [];

        try
        {
            ListeningStatsDto stats = await _dataLoader.LoadListeningStatsAsync(Album.Id);
            ListeningStats.SetStats(stats);
            listenedTrackIds = stats.ListenedTrackIds.ToHashSet();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load listening stats for album {AlbumId}", Album.Id);
        }

        // A track counts as listened from either source: the legacy counter (history predating
        // listening events, resettable by the user) or a completed listening event.
        int listenedCount = Tracks.Count(t => t.Track.ListenCount > 0 || listenedTrackIds.Contains(t.Track.Id));
        ListeningStats.SetProgression(listenedCount, Tracks.Count);
    }

    public void LoadPicture()
    {
        try
        {
            Picture = _pictureService.LoadPicture(Album.AlbumPath);

            if (!_pictureService.PictureExists(Album.AlbumPath))
                return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load picture for {AlbumPath}", Album.AlbumPath);
        }
    }

    private void LoadBackrop()
    {
        CancellationToken token = _navigationCts.Token;

        _backdropLoader.LoadBackdrop(Album.ArtistName, (BitmapImage? backdropImage) =>
        {
            if (!token.IsCancellationRequested)
                Backdrop = backdropImage;
        });
    }

    private async Task CalculatePictureDominantColorAsync()
    {
        if (Album.PictureDominantColor.HasValue)
            return;

        if (Interlocked.Exchange(ref _dominantColorCalculating, 1) == 1)
            return;

        try
        {
            string filePath = _pictureService.GetPictureFilePath(Album.AlbumPath);
            long? packed = await _dominantColorCalculator.CalculateAsync(filePath);

            Album.PictureDominantColor = packed;
            UpdateDominantColor();

            if (packed.HasValue)
                await _editService.UpdatePictureDominantColorAsync(Album.Id, packed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load picture or dominant color for {AlbumPath}", Album.AlbumPath);
        }
    }

    private async Task InitializeTagsAsync()
    {
        List<string> albumTags = Album.GetTags();

        EditableTags.Clear();
        foreach (string tag in albumTags)
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
        Album.TagsAsString = string.Join(",", EditableTags);
        await _editService.UpdateTagsAsync(Album.Id, EditableTags);

        Debug.WriteLine(Album.TagsAsString);
    }

    private async Task UpdateStatisticsIfNeededAsync()
    {
        bool updated = await _statisticsService.UpdateIfNeededAsync(Album, Tracks);

        if (updated)
            _messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
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
    private async Task ListenAsync(TrackViewModel? startTrack = null)
    {
        if (_tracks == null)
            await LoadTracksAsync(Album.Id, CancellationToken.None);

        if (_tracks?.Any() == true)
            _playerService.LoadPlaylist(_tracks.ToList(), startTrack?.Track);
    }

    [RelayCommand]
    private async Task AlbumFavoriteAsync()
    {
        bool newFavoriteState = !Album.IsFavorite;
        await _editService.UpdateFavoriteAsync(Album, newFavoriteState);

        OnPropertyChanged(nameof(IsFavorite));
        _messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
    }

    [RelayCommand]
    private async Task GetDataFromApiAsync()
    {
        CancellationToken token = _navigationCts.Token;

        AlbumApiUpdateResult result = await _apiService.GetAndUpdateAlbumDataAsync(Album, _pictureService);

        if (token.IsCancellationRequested)
            return;

        if (result.PictureDownloaded)
        {
            LoadPicture();
            _messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Picture));
        }

        if (result.DataUpdated)
        {
            AlbumDto? refreshedAlbum = await _dataLoader.ReloadAlbumAsync(Album.Id);
            if (refreshedAlbum != null)
            {
                Album = refreshedAlbum;
                UpdateAnniversaryBadge();
                UpdateDominantColor();
            }

            _messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        }
    }

    [RelayCommand]
    private async Task EditAlbumAsync()
    {
        bool updated = await _editService.EditAlbumAsync(Album);

        if (!updated)
            return;

        _messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Update));
        OnPropertyChanged(nameof(Album));
    }

    [RelayCommand]
    private async Task SelectPictureAsync()
    {
        CancellationToken token = _navigationCts.Token;

        BitmapImage? newPicture = await _pictureService.SelectAndSavePictureAsync(Album.AlbumPath);

        if (newPicture is null || token.IsCancellationRequested)
            return;

        Album.PictureDominantColor = null;
        await CalculatePictureDominantColorAsync();

        Picture = newPicture;

        _messenger.Send(new AlbumUpdateMessage(Album.Id, ActionType.Picture));
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
        string language = LanguageHelpers.NormalizeLanguageCode(rawLanguage, "fr");

        await _dialogService.ShowTextAsync(Album.Name, Album.Biography, showTranslateButton: _appOptions.NovaApiEnabled, language);
    }
}