using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.Application.Features.Playlists.Messages;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlists.Handlers;
using Rok.ViewModels.Playlists.Services;

namespace Rok.ViewModels.Playlists;

public partial class PlaylistsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<PlaylistsViewModel> _logger;

    private readonly PlaylistsDataLoader _dataLoader;
    private readonly PlaylistCreationService _creationService;
    private readonly PlaylistImportService _importService;
    private readonly PlaylistUpdateMessageHandler _updateHandler;
    private readonly PlaylistImportedMessageHandler _importedHandler;
    private readonly IAppOptions _appOptions;
    private readonly IMessenger _messenger;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly List<IDisposable> _subscriptions = new();

    public RangeObservableCollection<PlaylistViewModel> Playlists { get; private set; } = [];

    public RangeObservableCollection<PlaylistViewModel> SmartPlaylists { get; private set; } = [];

    public bool HasNoData => Playlists.Count == 0 && SmartPlaylists.Count == 0;

    [ObservableProperty]
    public partial bool IsGridView { get; set; }
    partial void OnIsGridViewChanged(bool value)
    {
        _appOptions.IsGridView = value;
    }


    public PlaylistsViewModel(
        PlaylistsDataLoader dataLoader,
        PlaylistCreationService creationService,
        PlaylistImportService importService,
        PlaylistUpdateMessageHandler updateHandler,
        PlaylistImportedMessageHandler importedHandler,
        IAppOptions appOptions,
        IMessenger messenger,
        ILogger<PlaylistsViewModel> logger)
    {
        _dataLoader = Guard.NotNull(dataLoader);
        _creationService = Guard.NotNull(creationService);
        _importService = Guard.NotNull(importService);
        _updateHandler = Guard.NotNull(updateHandler);
        _importedHandler = Guard.NotNull(importedHandler);
        _appOptions = Guard.NotNull(appOptions);
        _messenger = Guard.NotNull(messenger);
        _logger = Guard.NotNull(logger);

        SubscribeToMessages();
        SubscribeToEvents();
    }


    private void SubscribeToMessages()
    {
        _subscriptions.Add(_messenger.Subscribe<PlaylistUpdatedMessage>(async (message) => await _updateHandler.HandleAsync(message)));
        _subscriptions.Add(_messenger.Subscribe<PlaylistImportedMessage>(async (message) => await _importedHandler.HandleAsync(message)));
    }

    private void SubscribeToEvents()
    {
        _updateHandler.DataChanged += OnDataChanged;
        _importedHandler.DataChanged += OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() => RefreshPlaylists());
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

        OnPropertyChanged(nameof(HasNoData));
    }

    [RelayCommand]
    private Task NewSmartPlaylistAsync()
    {
        return _creationService.CreateSmartPlaylistAsync();
    }

    [RelayCommand]
    private Task NewPlaylistAsync()
    {
        return _creationService.CreateClassicPlaylistAsync();
    }

    [RelayCommand]
    private async Task ImportPlaylistsAsync()
    {
        try
        {
            await _importService.RunAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playlist import failed");
        }
    }


    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                foreach (IDisposable subscription in _subscriptions)
                    subscription.Dispose();
                _subscriptions.Clear();

                _updateHandler.DataChanged -= OnDataChanged;
                _importedHandler.DataChanged -= OnDataChanged;
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
}