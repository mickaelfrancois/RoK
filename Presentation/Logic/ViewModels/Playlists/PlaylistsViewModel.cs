using Rok.Logic.ViewModels.Playlists.Handlers;
using Rok.Logic.ViewModels.Playlists.Services;

namespace Rok.Logic.ViewModels.Playlists;

public partial class PlaylistsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<PlaylistsViewModel> _logger;

    private readonly PlaylistsDataLoader _dataLoader;
    private readonly PlaylistCreationService _creationService;
    private readonly PlaylistUpdateMessageHandler _updateHandler;

    public RangeObservableCollection<PlaylistViewModel> Playlists { get; private set; } = [];

    public RelayCommand NewSmartPlaylistCommand { get; private set; }
    public RelayCommand NewPlaylistCommand { get; private set; }

    public PlaylistsViewModel(
        PlaylistsDataLoader dataLoader,
        PlaylistCreationService creationService,
        PlaylistUpdateMessageHandler updateHandler,
        ILogger<PlaylistsViewModel> logger)
    {
        _dataLoader = Guard.Against.Null(dataLoader);
        _creationService = Guard.Against.Null(creationService);
        _updateHandler = Guard.Against.Null(updateHandler);
        _logger = Guard.Against.Null(logger);

        NewSmartPlaylistCommand = new RelayCommand(async () => await NewSmartPlaylistAsync());
        NewPlaylistCommand = new RelayCommand(async () => await NewPlaylistAsync());

        SubscribeToMessages();
        SubscribeToEvents();
    }


    private void SubscribeToMessages()
    {
        Messenger.Subscribe<PlaylistUpdatedMessage>(async (message) => await _updateHandler.HandleAsync(message));
    }

    private void SubscribeToEvents()
    {
        _updateHandler.DataChanged += OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        RefreshPlaylists();
    }

    public async Task LoadDataAsync(bool forceReload)
    {
        bool mustLoad = forceReload || _dataLoader.ViewModels.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Playlists already loaded, skipping reload.");
            return;
        }

        await _dataLoader.LoadPlaylistsAsync();
        RefreshPlaylists();
    }

    private void RefreshPlaylists()
    {
        Playlists.Clear();
        Playlists.AddRange(_dataLoader.ViewModels);
    }

    private async Task NewSmartPlaylistAsync()
    {
        await _creationService.CreateSmartPlaylistAsync();
    }

    private async Task NewPlaylistAsync()
    {
        await _creationService.CreateClassicPlaylistAsync();
    }

    #region IDisposable Support

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _updateHandler.DataChanged -= OnDataChanged;
                _dataLoader.Clear();
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