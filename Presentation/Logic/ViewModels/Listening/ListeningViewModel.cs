using Microsoft.UI.Dispatching;
using Rok.Application.Features.Tracks.Query;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Listening;

public partial class ListeningViewModel : ObservableObject
{
    private readonly ILogger<ListeningViewModel> _logger;

    private readonly IPlayerService _playerService;

    private readonly IMediator _mediator;

    public int TrackCount
    {
        get
        {
            return Tracks.Count;
        }
    }

    public long Duration
    {
        get
        {
            return Tracks.Sum(c => c.Track.Duration);
        }
    }

    public ArtistViewModel? Artist { get; private set; }

    public RangeObservableCollection<TrackViewModel> Tracks { get; private set; } = [];

    public TrackViewModel? CurrentTrack { get; private set; }

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public AsyncRelayCommand<TrackViewModel> AddMoreFromArtistCommand { get; private set; }


    public ListeningViewModel(IMediator mediator, IPlayerService playerService, ILogger<ListeningViewModel> logger)
    {
        _mediator = Guard.Against.Null(mediator);
        _playerService = Guard.Against.Null(playerService);
        _logger = Guard.Against.Null(logger);

        AddMoreFromArtistCommand = new AsyncRelayCommand<TrackViewModel>(AddMoreFromArtistAsync);

        Messenger.Subscribe<MediaChangedMessage>(async (message) => await MediaChangedAsync(message));
        Messenger.Subscribe<PlaylistChanged>(async (message) => await PlaylistChangedAsync(message));

        if (_playerService.Playlist != null)
        {
            LoadTracksList(_playerService.Playlist);
#pragma warning disable 4014
            SetCurrentTrackAsync(_playerService.CurrentTrack);
#pragma warning restore 4014
        }
    }


    public void LoadTracksList(List<TrackDto> tracks)
    {
        Tracks.Clear();

        if (tracks != null)
            Tracks.AddRange(CreateTracksViewModels(tracks));

        OnPropertyChanged(nameof(TrackCount));
    }


    public async Task SetCurrentTrackAsync(TrackDto track)
    {
        if (track == null)
            ClearData();
        else
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                foreach (TrackViewModel track in Tracks.Where(c => c.Listening))
                    track.Listening = false;

                CurrentTrack = Tracks.FirstOrDefault(c => c.Track.Id == track.Id);

                if (CurrentTrack != null)
                {
                    CurrentTrack.Listening = true;
                    OnPropertyChanged(nameof(CurrentTrack));
                }
            });


            if (Artist?.Artist.Id != track.ArtistId && track.ArtistId.HasValue)
            {
                Artist = App.ServiceProvider.GetService<ArtistViewModel>()!;
                await Artist.LoadDataAsync(track.ArtistId.Value, loadAlbums: false, loadTracks: false, fetchApi: false);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    OnPropertyChanged(nameof(Artist));
                });
            }
        }
    }


    private void ClearData()
    {
        CurrentTrack = null;
        Artist = null;

        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(Artist));
    }


    private static List<TrackViewModel> CreateTracksViewModels(List<TrackDto> tracks)
    {
        List<TrackViewModel> list = [];

        tracks.ForEach(track =>
        {
            TrackViewModel trackViewModel = App.ServiceProvider.GetService<TrackViewModel>()!;
            trackViewModel.SetData(track);

            list.Add(trackViewModel);
        });

        return list;
    }


    private async Task MediaChangedAsync(MediaChangedMessage message)
    {
        _logger.LogDebug("Listening VM handle media changed, title {Message}.", message.NewTrack.Title);

        await SetCurrentTrackAsync(message.NewTrack);
    }


    private async Task PlaylistChangedAsync(PlaylistChanged message)
    {
        _logger.LogDebug("Listening VM handle playlist changed.");

        LoadTracksList(message.Tracks);
        await SetCurrentTrackAsync(_playerService.CurrentTrack);
    }


    private async Task AddMoreFromArtistAsync(TrackViewModel track)
    {
        if (!track.Track.ArtistId.HasValue)
            return;

        long artistId = track.Track.ArtistId.Value;
        int maxTracks = 3;

        IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByArtistIdQuery(artistId));

        List<TrackDto> shuffledTracks = tracks.ToList();
        if (shuffledTracks.Count == 0)
            return;

        shuffledTracks.Shuffle();
        shuffledTracks.RemoveAll(c => Tracks.Any(t => t.Track.Id == c.Id));
        shuffledTracks = shuffledTracks.Take(maxTracks).ToList();

        if (shuffledTracks.Count == 0)
            return;

        _playerService.InsertTracksToPlaylist(shuffledTracks);
    }
}