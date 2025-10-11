using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.Query;

namespace Rok.Logic.ViewModels.Playlists;

public partial class PlaylistsViewModel : ObservableObject, IDisposable
{
    private readonly IMediator _mediator;
    private readonly NavigationService _navigationService;
    private readonly ILogger<PlaylistsViewModel> _logger;
    private readonly ResourceLoader _resourceLoader;

    public RangeObservableCollection<PlaylistViewModel> Playlists { get; set; } = [];

    public RelayCommand NewSmartPlaylistCommand { get; private set; }
    public RelayCommand NewPlaylistCommand { get; private set; }


    public PlaylistsViewModel(IMediator mediator, ResourceLoader resourceLoader, NavigationService navigationService, ILogger<PlaylistsViewModel> logger)
    {
        _mediator = mediator;
        _resourceLoader = resourceLoader;
        _navigationService = navigationService;
        _logger = logger;

        NewSmartPlaylistCommand = new RelayCommand(async () => await NewSmartPlaylistAsync());
        NewPlaylistCommand = new RelayCommand(async () => await NewPlaylistAsync());

        Messenger.Subscribe<PlaylistUpdatedMessage>(async (c) => await PlaylistUpdatedMessageHandleAsync(c));
    }


    public async Task LoadDataAsync(bool forceReload)
    {
        bool mustLoad = forceReload || Playlists.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Playlists already loaded, skipping reload.");
            return;
        }

        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Playlists loaded"))
        {
            IEnumerable<PlaylistHeaderDto> playlists = await _mediator.SendMessageAsync(new GetAllPlaylistsQuery());

            Playlists.AddRange(CreatePlaylistsViewModels(playlists.OrderBy(c => c.Name)));
        }
    }


    private static List<PlaylistViewModel> CreatePlaylistsViewModels(IEnumerable<PlaylistHeaderDto> playlists)
    {
        int capacity = playlists.Count();
        List<PlaylistViewModel> playlistViewModels = new(capacity);

        foreach (PlaylistHeaderDto playlist in playlists)
        {
            PlaylistViewModel playlistViewModel = App.ServiceProvider.GetRequiredService<PlaylistViewModel>();
            playlistViewModel.SetData(playlist);
            playlistViewModels.Add(playlistViewModel);
        }

        return playlistViewModels;
    }


    private async Task PlaylistUpdatedMessageHandleAsync(PlaylistUpdatedMessage message)
    {
        PlaylistViewModel? playlistToUpdate = Playlists.FirstOrDefault(c => c.Playlist.Id == message.Id);
        PlaylistHeaderDto playlistDto = default!;

        ActionType action = message.Action;

        if (action == ActionType.Add && playlistToUpdate != null)
            action = ActionType.Update;

        if (action == ActionType.Update || action == ActionType.Delete)
        {
            if (playlistToUpdate == null)
            {
                _logger.LogWarning("Playlist {PlaylistId} not found for update or delete.", message.Id);
                return;
            }
        }

        if (action == ActionType.Update || action == ActionType.Add)
        {
            Result<PlaylistHeaderDto> playlistDtoResult = await _mediator.SendMessageAsync(new GetPlaylistByIdQuery(message.Id));
            if (playlistDtoResult.IsError)
            {
                _logger.LogError("Failed to retrieve playlist {PlaylistId} for update or delete: {ErrorMessage}", message.Id, playlistDtoResult.Error);
                return;
            }
            else
                playlistDto = playlistDtoResult.Value!;
        }

        switch (action)
        {
            case ActionType.Add:
                PlaylistViewModel playlistViewModel = App.ServiceProvider.GetRequiredService<PlaylistViewModel>();
                playlistViewModel.SetData(playlistDto);
                Playlists.Add(playlistViewModel);
                _logger.LogTrace("Playlist {PlaylistId} viewmodel add.", message.Id);
                break;

            case ActionType.Update:
                playlistToUpdate!.SetData(playlistDto);
                _logger.LogTrace("Playlist {PlaylistId} viewmodel updated.", message.Id);
                break;

            case ActionType.Delete:
                Playlists.Remove(playlistToUpdate!);
                _logger.LogTrace("Playlist {PlaylistId} viewmodel removed.", message.Id);
                break;
        }
    }


    private async Task NewSmartPlaylistAsync()
    {
        CreatePlaylistCommand command = new()
        {
            Type = (int)PlaylistType.Smart,
            Name = _resourceLoader.GetString("newPlaylist")
        };

        Result<long> result = await _mediator.SendMessageAsync(command);
        if (result.IsError)
        {
            _logger.LogError("Failed to create new smart playlist: {ErrorMessage}", result.Error);
            return;
        }

        Messenger.Send(new PlaylistUpdatedMessage(result.Value, ActionType.Add));
        _navigationService.NavigateToSmartPlaylist(result.Value);
    }


    private async Task NewPlaylistAsync()
    {
        CreatePlaylistCommand command = new()
        {
            Type = (int)PlaylistType.Classic,
            Name = _resourceLoader.GetString("newPlaylist")
        };

        Result<long> result = await _mediator.SendMessageAsync(command);
        if (result.IsError)
        {
            _logger.LogError("Failed to create new classic playlist: {ErrorMessage}", result.Error);
            return;
        }

        Messenger.Send(new PlaylistUpdatedMessage(result.Value, ActionType.Add));
        _navigationService.NavigateToPlaylist(result.Value);
    }


    #region IDisposable Support

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Playlists.Clear();
            }

            disposedValue = true;
        }
    }


    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    #endregion
}
