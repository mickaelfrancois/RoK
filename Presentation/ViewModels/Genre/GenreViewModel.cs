using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Player;
using Rok.Application.Randomizer;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Genre.Services;

namespace Rok.ViewModels.Genre;

public partial class GenreViewModel : ObservableObject
{
    private static string FallbackPictureUri => App.Current.Resources["ArtistFallbackPictureUri"] as string ?? "ms-appx:///Assets/artistFallback.png";
    private static BitmapImage FallbackPicture => new(new Uri(FallbackPictureUri));

    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly GenreDataLoader _dataLoader;
    private readonly ILogger<GenreViewModel> _logger;
    private readonly IBackdropLoader _backdropLoader;
    private readonly ArtistPictureService _pictureService;
    private readonly GenreEditService _editService;

    public RangeObservableCollection<AlbumViewModel> Albums { get; set; } = [];
    public GenreDto Genre { get; private set; } = new();

    public bool IsFavorite
    {
        get => Genre.IsFavorite;
        set => SetProperty(Genre.IsFavorite, value, Genre, (genre, val) => genre.IsFavorite = val);
    }

    [ObservableProperty]
    public partial BitmapImage? Backdrop { get; set; }

    private BitmapImage? _picture = null;
    public BitmapImage Picture
    {
        get
        {
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

    public string SubTitle
    {
        get
        {
            string label = "";
            string separator = "";

            if (Genre.TrackCount > 0)
            {
                label += $"{Genre.TrackCount} ";

                if (Genre.TrackCount > 1)
                    label += _resourceLoader.GetString("tracks");
                else
                    label += _resourceLoader.GetString("track");
                separator = ", ";
            }

            if (Genre.AlbumCount > 0)
            {
                label += $"{separator}{Genre.AlbumCount} ";
                if (Genre.AlbumCount > 1)
                    label += _resourceLoader.GetString("albums");
                else
                    label += _resourceLoader.GetString("album");
                separator = ", ";
            }

            if (Genre.BestofCount > 0)
            {
                label += $"{separator}{Genre.BestofCount} ";
                if (Genre.BestofCount > 1)
                    label += _resourceLoader.GetString("bestOf");
                else
                    label += _resourceLoader.GetString("bestOf");
                separator = ", ";
            }

            if (Genre.LiveCount > 0)
            {
                label += $"{separator}{Genre.LiveCount} ";
                if (Genre.LiveCount > 1)
                    label += _resourceLoader.GetString("live");
                else
                    label += _resourceLoader.GetString("live");
                separator = ", ";
            }

            if (Genre.CompilationCount > 0)
            {
                label += $"{separator}{Genre.CompilationCount} ";
                if (Genre.CompilationCount > 1)
                    label += _resourceLoader.GetString("compilations");
                else
                    label += _resourceLoader.GetString("compilation");
            }

            return label;
        }
    }

    public string ListenStats
    {
        get
        {
            string label = "";

            if (Genre.ListenCount > 0)
            {
                label += _resourceLoader.GetString("listenCount") + " "
                                + Genre.ListenCount + " "
                                + _resourceLoader.GetString("listenCountTimes") + ", "
                                + _resourceLoader.GetString("lastListen") + " "
                               + Genre.LastListen?.ToString("g");
            }

            return label;
        }
    }


    public GenreViewModel(IPlayerService playerService,
                          ResourceLoader resourceLoader,
                          GenreDataLoader dataLoader,
                          ArtistPictureService pictureService,
                          IBackdropLoader backdropLoader,
                          GenreEditService editService,
                          ILogger<GenreViewModel> logger)
    {
        _playerService = playerService;
        _dataLoader = dataLoader;
        _pictureService = pictureService;
        _backdropLoader = backdropLoader;
        _resourceLoader = resourceLoader;
        _editService = editService;
        _logger = logger;
    }


    public async Task LoadDataAsync(long genreId, bool loadAlbums = true, bool loadTracks = true)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        await LoadGenreAsync(genreId);
        await LoadAlbumsAsync(genreId);

        stopwatch.Stop();

        _logger.LogInformation("Genre {GenreId} loaded in {ElapsedMilliseconds} ms (albums: {LoadAlbums}, tracks: {LoadTracks})",
                                genreId, stopwatch.ElapsedMilliseconds, loadAlbums, loadTracks);
    }


    private async Task LoadGenreAsync(long genreId)
    {
        GenreDto? genre = await _dataLoader.LoadGenreAsync(genreId);

        if (genre != null)
            Genre = genre;
    }


    private async Task LoadAlbumsAsync(long genreId)
    {
        List<AlbumViewModel> albums = await _dataLoader.LoadAlbumsAsync(genreId);

        if (albums.Count > 0)
        {
            Albums.AddRange(albums);

            List<AlbumViewModel> albumsWithArtist = albums.Where(a => !string.IsNullOrEmpty(a.Album.ArtistName)).ToList();
            int index = Random.Shared.Next(albumsWithArtist.Count);
            AlbumViewModel randomAlbum = albumsWithArtist[index];

            LoadPicture(randomAlbum.Album.ArtistName);
            LoadBackdrop(randomAlbum.Album.ArtistName);
        }
    }


    public void LoadPicture(string artistName)
    {
        Picture = _pictureService.LoadPicture(artistName);
    }


    public void LoadBackdrop(string artistName)
    {
        _backdropLoader.LoadBackdrop(artistName, (BitmapImage? backdropImage) =>
        {
            Backdrop = backdropImage;
        });
    }


    [RelayCommand]
    private async Task ListenAsync()
    {
        IEnumerable<TrackDto> tracks = await _dataLoader.LoadTracksAsync(Genre.Id);

        if (tracks?.Any() == true)
        {
            List<TrackDto> shuffledTracks = tracks.ToList();
            TracksRandomizer.Randomize(shuffledTracks);
            _playerService.LoadPlaylist(shuffledTracks);
        }
    }

    [RelayCommand]
    private async Task GenreFavoriteAsync()
    {
        bool newFavoriteState = !Genre.IsFavorite;
        await _editService.UpdateFavoriteAsync(Genre, newFavoriteState);

        OnPropertyChanged(nameof(IsFavorite));
    }
}
