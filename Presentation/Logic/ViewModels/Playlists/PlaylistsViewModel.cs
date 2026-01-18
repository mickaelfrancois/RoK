using Rok.Logic.ViewModels.Playlists.Handlers;
using Rok.Logic.ViewModels.Playlists.Services;

namespace Rok.Logic.ViewModels.Playlists;

public partial class PlaylistsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<PlaylistsViewModel> _logger;

    private readonly PlaylistsDataLoader _dataLoader;
    private readonly PlaylistCreationService _creationService;
    private readonly PlaylistUpdateMessageHandler _updateHandler;
    private readonly IAppOptions _appOptions;

    public RangeObservableCollection<PlaylistViewModel> Playlists { get; private set; } = [];

    public RangeObservableCollection<PlaylistViewModel> SmartPlaylists { get; private set; } = [];

    private bool _isGridView = true;
    public bool IsGridView
    {
        get
        {
            return _isGridView;
        }
        private set
        {
            _isGridView = value;
            _appOptions.IsGridView = value;
            OnPropertyChanged(nameof(IsGridView));
        }
    }

    public RelayCommand NewSmartPlaylistCommand { get; private set; }
    public RelayCommand NewPlaylistCommand { get; private set; }
    public RelayCommand ToggleDisplayModeCommand { get; private set; }

    public PlaylistsViewModel(
        PlaylistsDataLoader dataLoader,
        PlaylistCreationService creationService,
        PlaylistUpdateMessageHandler updateHandler,
        IAppOptions appOptions,
        ILogger<PlaylistsViewModel> logger)
    {
        _dataLoader = Guard.Against.Null(dataLoader);
        _creationService = Guard.Against.Null(creationService);
        _updateHandler = Guard.Against.Null(updateHandler);
        _appOptions = Guard.Against.Null(appOptions);
        _logger = Guard.Against.Null(logger);

        NewSmartPlaylistCommand = new RelayCommand(async () => await NewSmartPlaylistAsync());
        NewPlaylistCommand = new RelayCommand(async () => await NewPlaylistAsync());
        ToggleDisplayModeCommand = new RelayCommand(() => IsGridView = !IsGridView);

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
        IsGridView = _appOptions.IsGridView;

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
        Playlists.AddRange(_dataLoader.ViewModels.Where(c => !c.Playlist.IsSmart));

        SmartPlaylists.Clear();
        SmartPlaylists.AddRange(_dataLoader.ViewModels.Where(c => c.Playlist.IsSmart));
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