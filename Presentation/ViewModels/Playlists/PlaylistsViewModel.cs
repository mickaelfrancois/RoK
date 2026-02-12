using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.ViewModels.Playlists.Handlers;
using Rok.ViewModels.Playlists.Services;

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

    [ObservableProperty]
    public partial bool IsGridView { get; set; }
    partial void OnIsGridViewChanged(bool value)
    {
        _appOptions.IsGridView = value;
    }


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



    [RelayCommand]
    private void ToggleDisplayMode()
    {
        IsGridView = !IsGridView;
    }

    [RelayCommand]
    private void RefreshPlaylists()
    {
        Playlists.Clear();
        Playlists.AddRange(_dataLoader.ViewModels.Where(c => !c.Playlist.IsSmart));

        SmartPlaylists.Clear();
        SmartPlaylists.AddRange(_dataLoader.ViewModels.Where(c => c.Playlist.IsSmart));
    }

    [RelayCommand]
    private async Task NewSmartPlaylistAsync()
    {
        await _creationService.CreateSmartPlaylistAsync();
    }

    [RelayCommand]
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