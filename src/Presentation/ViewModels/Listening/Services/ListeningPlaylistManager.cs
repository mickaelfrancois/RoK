using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Track;

namespace Rok.ViewModels.Listening.Services;

public partial class ListeningPlaylistManager : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ListeningDataLoader _dataLoader;
    private readonly ListeningEntityUpdateWatcher _watcher;
    private readonly ILogger<ListeningPlaylistManager> _logger;
    private volatile TrackDto? _latestTrack;

    public RangeObservableCollection<TrackViewModel> Tracks { get; private set; } = [];
    public TrackViewModel? CurrentTrack { get; private set; }

    /// <summary>The track shown by the header. Written on the UI thread together with <see cref="CurrentTrack"/>.</summary>
    public TrackDto? DisplayedTrack { get; private set; }

    public ArtistViewModel? Artist { get; private set; }
    public AlbumViewModel? Album { get; private set; }

    public int TrackCount => Tracks.Count;
    public long Duration => Tracks.Sum(c => c.Track.Duration);

    public event EventHandler? PlaylistChanged;
    public event EventHandler? CurrentTrackChanged;

    public ListeningPlaylistManager(DispatcherQueue dispatcherQueue, ListeningDataLoader dataLoader, ListeningEntityUpdateWatcher watcher, ILogger<ListeningPlaylistManager> logger)
    {
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
        _dataLoader = dataLoader;
        _watcher = watcher;

        _watcher.TrackedArtistUpdated += OnTrackedArtistUpdated;
        _watcher.TrackedAlbumUpdated += OnTrackedAlbumUpdated;
    }

    private void OnTrackedArtistUpdated(object? sender, long artistId) => _ = ReloadArtistAsync(artistId);

    private void OnTrackedAlbumUpdated(object? sender, long albumId) => _ = ReloadAlbumAsync(albumId);

    private async Task ReloadArtistAsync(long artistId)
    {
        try
        {
            ArtistViewModel? reloaded = await _dataLoader.LoadArtistAsync(artistId);

            if (reloaded == null || _latestTrack?.ArtistId != artistId)
                return;

            Artist = reloaded;

            _dispatcherQueue.TryEnqueue(() =>
            {
                OnPropertyChanged(nameof(Artist));
                CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Listening failed to reload artist {ArtistId}.", artistId);
        }
    }

    private async Task ReloadAlbumAsync(long albumId)
    {
        try
        {
            AlbumViewModel? reloaded = await _dataLoader.LoadAlbumAsync(albumId);

            if (reloaded == null || _latestTrack?.AlbumId != albumId)
                return;

            Album = reloaded;

            _dispatcherQueue.TryEnqueue(() =>
            {
                reloaded.LoadPicture();
                OnPropertyChanged(nameof(Album));
                CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Listening failed to reload album {AlbumId}.", albumId);
        }
    }

    public void LoadTracksList(List<TrackDto> tracks)
    {
        Tracks.Clear();

        if (tracks != null)
            Tracks.AddRange(_dataLoader.CreateTracksViewModels(tracks));

        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(Duration));
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }


    public async Task SetCurrentTrackAsync(TrackDto? track)
    {
        if (track == null)
        {
            ClearData();
            return;
        }

        _latestTrack = track;

        _dispatcherQueue.TryEnqueue(() =>
        {
            foreach (TrackViewModel trackViewModel in Tracks.Where(c => c.Listening))
                trackViewModel.Listening = false;

            CurrentTrack = Tracks.FirstOrDefault(c => c.Track.Id == track.Id);
            DisplayedTrack = track;

            if (CurrentTrack != null)
            {
                CurrentTrack.Listening = true;
                OnPropertyChanged(nameof(CurrentTrack));
            }

            UpdateUpcomingFlags();
        });

        await LoadArtistIfNeededAsync(track);
        await LoadAlbumIfNeededAsync(track);

        _dispatcherQueue.TryEnqueue(() =>
        {
            CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private async Task LoadArtistIfNeededAsync(TrackDto track)
    {
        if (!track.ArtistId.HasValue)
        {
            if (Artist == null)
                return;

            Artist = null;
            TrackEntities();
            _dispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(Artist)));
            return;
        }

        if (Artist?.Artist.Id == track.ArtistId)
            return;

        ArtistViewModel? newArtist = await _dataLoader.LoadArtistAsync(track.ArtistId.Value);

        if (_latestTrack?.ArtistId != track.ArtistId)
            return;

        if (newArtist == null)
        {
            if (Artist == null)
                return;

            Artist = null;
            TrackEntities();
            _dispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(Artist)));
            return;
        }

        Artist = newArtist;
        TrackEntities();

        _dispatcherQueue.TryEnqueue(() =>
        {
            OnPropertyChanged(nameof(Artist));
        });
    }

    private async Task LoadAlbumIfNeededAsync(TrackDto track)
    {
        if (!track.AlbumId.HasValue)
        {
            if (Album == null)
                return;

            Album = null;
            TrackEntities();
            _dispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(Album)));
            return;
        }

        if (Album?.Album.Id == track.AlbumId)
            return;

        AlbumViewModel? newAlbum = await _dataLoader.LoadAlbumAsync(track.AlbumId.Value);

        if (_latestTrack?.AlbumId != track.AlbumId)
            return;

        if (newAlbum == null)
        {
            if (Album == null)
                return;

            Album = null;
            TrackEntities();
            _dispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(Album)));
            return;
        }

        Album = newAlbum;
        TrackEntities();

        _dispatcherQueue.TryEnqueue(() =>
        {
            newAlbum.LoadPicture();
            OnPropertyChanged(nameof(Album));
        });
    }


    private void ClearData()
    {
        _latestTrack = null;
        CurrentTrack = null;
        Artist = null;
        Album = null;
        TrackEntities();

        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(Album));

        _dispatcherQueue.TryEnqueue(() =>
        {
            DisplayedTrack = null;
            CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void TrackEntities() => _watcher.SetTracked(Artist?.Artist.Id, Album?.Album.Id);

    private void UpdateUpcomingFlags()
    {
        int currentIndex = CurrentTrack != null ? Tracks.IndexOf(CurrentTrack) : -1;

        for (int index = 0; index < Tracks.Count; index++)
            Tracks[index].IsUpcoming = currentIndex >= 0 && index > currentIndex;
    }
}