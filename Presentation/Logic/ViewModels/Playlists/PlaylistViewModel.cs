using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Playlists;

public partial class PlaylistViewModel : ObservableObject
{
    private static BitmapImage FallbackPicture => new(new Uri("ms-appx:///Assets/artistFallback.png"));
    public PlaylistHeaderDto Playlist { get; private set; } = new();

    public RangeObservableCollection<TrackViewModel> Tracks { get; set; } = [];
    private IEnumerable<TrackDto>? _tracks = null;

    public string Name
    {
        get
        {
            return Playlist.Name;
        }
        set
        {
            if (Playlist.Name != value)
            {
                Playlist.Name = value;
                _ = SavePlaylistAsync(forceUpdate: true);
            }
        }
    }

    public int TrackMaximum
    {
        get
        {
            return Playlist.TrackMaximum;
        }
    }

    public int TrackCount
    {
        get
        {
            return Playlist.TrackCount;
        }
    }

    public int ArtistCount
    {
        get
        {
            if (_tracks == null)
                return 0;
            return _tracks.DistinctBy(c => c.ArtistId).Count();
        }
    }

    public string SubTitle
    {
        get
        {
            string label = $"{TrackCount} ";
            if (TrackCount > 1)
                label += _resourceLoader.GetString("tracks");
            else
                label += _resourceLoader.GetString("track");

            label += ", " + DurationTotalStr;

            return label;
        }
    }

    public string DurationTotalStr
    {
        get
        {
            TimeSpan time = TimeSpan.FromSeconds(Playlist.Duration);
            return (int)time.TotalHours + time.ToString(@"\:mm\:ss");
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

    private BitmapImage? _picture = null;
    public BitmapImage? Picture
    {
        get => _picture;
        set
        {
            _picture = value;
            OnPropertyChanged();
        }
    }

    public AsyncRelayCommand ListenCommand { get; private set; }
    public AsyncRelayCommand GenerateCommand { get; private set; }
    public RelayCommand PlaylistOpenCommand { get; private set; }
    public AsyncRelayCommand DeleteCommand { get; private set; }
    public AsyncRelayCommand<long> RemoveFromPlaylistCommand { get; private set; }
    public AsyncRelayCommand<List<PlaylistGroupDto>> SavePlaylistCommand { get; private set; }

    private readonly IMediator _mediator;
    private readonly NavigationService _navigationService;
    private readonly IArtistPicture _artistPicture;
    private readonly BackdropPicture _backdropPicture;
    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly ILogger<PlaylistViewModel> _logger;
    private readonly IPlaylistService _playlistService;


    public PlaylistViewModel(IPlaylistService playlistService, IMediator mediator, NavigationService navigationService, IPlayerService playerService, IArtistPicture artistPicture, BackdropPicture backdropPicture, ResourceLoader resourceLoader, ILogger<PlaylistViewModel> logger)
    {
        _playlistService = Guard.Against.Null(playlistService);
        _mediator = Guard.Against.Null(mediator);
        _navigationService = Guard.Against.Null(navigationService);
        _playerService = Guard.Against.Null(playerService);
        _artistPicture = Guard.Against.Null(artistPicture);
        _backdropPicture = Guard.Against.Null(backdropPicture);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _logger = Guard.Against.Null(logger);

        ListenCommand = new AsyncRelayCommand(ListenAsync);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        PlaylistOpenCommand = new RelayCommand(OpenPlaylist);
        DeleteCommand = new AsyncRelayCommand(DeletePlaylistAsync);
        RemoveFromPlaylistCommand = new AsyncRelayCommand<long>(RemoveTrackAsync);
        SavePlaylistCommand = new AsyncRelayCommand<List<PlaylistGroupDto>>((c) => SavePlaylistAsync(forceUpdate: true, c));
    }


    /// <summary>
    /// Sets the playlist data for the current instance.
    /// </summary>
    /// <param name="playlist">The playlist data to set. Cannot be <see langword="null"/>.</param>
    public void SetData(PlaylistHeaderDto playlist)
    {
        string oldPicture = Playlist.Picture;

        Playlist = Guard.Against.Null(playlist);

        if (oldPicture != Playlist.Picture)
        {
            LoadPicture();
            LoadBackrop();
        }

        OnPropertyChanged();
    }


    public async Task LoadDataAsync(long playlistId)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        await LoadPlaylistAsync(playlistId);
        await LoadTracksAsync();

        if (!Tracks.Any() && Playlist.Type == (int)PlaylistType.Smart)
        {
            await GenerateAsync();
            _tracks = await _mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(playlistId));
        }

        await SavePlaylistAsync();

        stopwatch.Stop();
        _logger.LogInformation("Playlist {PlaylistId} loaded in {ElapsedMilliseconds}ms", playlistId, stopwatch.ElapsedMilliseconds);
    }


    private async Task LoadPlaylistAsync(long playlistId)
    {
        Result<PlaylistHeaderDto> result = await _mediator.SendMessageAsync(new GetPlaylistByIdQuery(playlistId));
        if (result.IsSuccess)
        {
            Playlist = result.Value!;

            LoadBackrop();
            LoadPicture();
        }
        else
            _logger.LogError("Playlist {PlaylistId} not found", playlistId);
    }


    private void LoadBackrop()
    {
        try
        {
            if (string.IsNullOrEmpty(Playlist.Picture) == false)
            {
                List<string> backdrops = _backdropPicture.GetBackdrops(Playlist.Picture);
                if (backdrops.Count == 0)
                    return;

                string filePath = backdrops[0];
                Backdrop = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backdrop for playlist: {Name}", Playlist.Name);
        }
    }


    public void LoadPicture()
    {
        try
        {
            if (!string.IsNullOrEmpty(Playlist.Picture) && _artistPicture.PictureFileExists(Playlist.Picture))
            {
                string filePath = _artistPicture.GetPictureFile(Playlist.Picture);
                Picture = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }

            if (Picture == null)
                Picture = FallbackPicture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load picture for playlist: {Name}", Playlist.Name);
            Picture = FallbackPicture;
        }
    }


    private async Task LoadTracksAsync()
    {
        if (Playlist.Id > 0)
        {
            _tracks = await _mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(Playlist.Id));
            Tracks.InitWithAddRange(TrackViewModelMap.CreateViewModels(_tracks));
        }
    }


    private async Task SavePlaylistAsync(bool forceUpdate = false, List<PlaylistGroupDto>? groups = null)
    {
        TrackViewModel? track = Tracks.FirstOrDefault(c => string.IsNullOrEmpty(c.ArtistName) == false && _artistPicture.PictureFileExists(c.ArtistName));

        UpdatePlaylistCommand command = new()
        {
            Id = Playlist.Id,
            Name = Playlist.Name,
            Type = Playlist.Type,
            Duration = Tracks.Sum(c => c.Track.Duration),
            TrackCount = Tracks.Count,
            TrackMaximum = Playlist.TrackMaximum,
            DurationMaximum = Playlist.DurationMaximum,
            Picture = track?.ArtistName!,
            Groups = groups ?? Playlist.Groups
        };

        if (forceUpdate || command.Name != Playlist.Name ||
            command.Duration != Playlist.Duration ||
            command.TrackCount != Playlist.TrackCount ||
            command.TrackMaximum != Playlist.TrackMaximum ||
            command.DurationMaximum != Playlist.DurationMaximum ||
            command.Picture != Playlist.Picture ||
            command.Groups != Playlist.Groups)
        {
            _logger.LogInformation("Updating playlist statistics for {Name} (Id: {Id})", Playlist.Name, Playlist.Id);

            Result result = await _mediator.SendMessageAsync(command);

            if (result.IsError)
            {
                _logger.LogError("Failed to update playlist statistics for {Name} (Id: {Id}). Error: {Error}", Playlist.Name, Playlist.Id, result.Error);
                return;
            }

            if (command.Picture != null)
                Playlist.Picture = command.Picture;
            Playlist.Name = command.Name;
            Playlist.TrackCount = command.TrackCount;
            Playlist.TrackMaximum = command.TrackMaximum;
            Playlist.DurationMaximum = command.DurationMaximum;
            Playlist.Groups = command.Groups;

            LoadPicture();
            OnPropertyChanged();

            Messenger.Send(new PlaylistUpdatedMessage(Playlist.Id, ActionType.Update));
        }
    }


    private async Task ListenAsync()
    {
        if (_tracks == null)
        {
            if (Playlist.Type == (int)PlaylistType.Smart)
                await GenerateAsync();
            else
                await LoadTracksAsync();
        }

        if (_tracks?.Any() == true)
        {
            _playerService.LoadPlaylist(_tracks.ToList());
        }
    }


    private async Task GenerateAsync()
    {
        _logger.LogTrace("Generate tracks for playlist '{Name}'.", Playlist.Name);

        PlaylistTracksDto playlistTracks = await _playlistService.GenerateAsync(Playlist);

        await SaveTracksAsync(playlistTracks.Tracks);
        await LoadTracksAsync();
        await SavePlaylistAsync();

        OnPropertyChanged();
    }


    private async Task SaveTracksAsync(List<TrackDto> tracks)
    {
        if (tracks == null)
            return;

        int index = 1;
        CreatePlaylistTracksCommand command = new() { PlaylistId = Playlist.Id };

        foreach (TrackDto track in tracks)
            command.Tracks.Add(new CreatePlaylistTracksDto { TrackId = track.Id, Position = index++ });

        await _mediator.SendMessageAsync(command);
    }


    private void OpenPlaylist()
    {
        if (Playlist.Id > 0)
        {
            if (Playlist.Type == (int)PlaylistType.Smart)
                _navigationService.NavigateToSmartPlaylist(Playlist.Id);
            else
                _navigationService.NavigateToPlaylist(Playlist.Id);
        }
    }


    private async Task DeletePlaylistAsync()
    {
        if (Playlist.Id > 0)
        {
            Result<bool> result = await _mediator.SendMessageAsync(new DeletePlaylistCommand { Id = Playlist.Id });

            if (result.IsSuccess)
            {
                Messenger.Send(new PlaylistUpdatedMessage(Playlist.Id, ActionType.Delete));

                _navigationService.RemoveLastEntry();
                _navigationService.NavigateToPlaylists();
            }
            else
                _logger.LogError("Failed to delete playlist: {Name}. Error: {Error}", Playlist.Name, result.Error);
        }
    }


    private async Task RemoveTrackAsync(long trackId)
    {
        Result result = await _mediator.SendMessageAsync(new RemoveTrackFromPlaylistCommand() { PlaylistId = Playlist.Id, TrackId = trackId });
        if (result.IsSuccess)
        {
            TrackViewModel? track = Tracks.FirstOrDefault(c => c.Track.Id == trackId);
            if (track != null)
                Tracks.Remove(track);

            _tracks = _tracks!.Where(t => t.Id != trackId).ToList();

            Messenger.Send(new PlaylistUpdatedMessage(Playlist.Id, ActionType.Update));
        }
        else
            Messenger.Send(new ShowNotificationMessage() { Message = "Failed to remove track from playlist", Type = NotificationType.Error });
    }
}
