using Rok.ViewModels.Albums.Handlers;
using Rok.ViewModels.Albums.Interfaces;

namespace Rok.ViewModels.Albums.Services;

public partial class AlbumLibraryMonitor : IAlbumLibraryMonitor
{
    private readonly AlbumUpdateMessageHandler _albumUpdateHandler;
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly AlbumImportedMessageHandler _albumImportedHandler;
    private readonly TagUpdatedMessageHandler _tagUpdatedHandler;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public event EventHandler? LibraryChanged;

    public AlbumLibraryMonitor(IMessenger messenger, AlbumUpdateMessageHandler albumUpdateHandler, LibraryRefreshMessageHandler libraryRefreshHandler, AlbumImportedMessageHandler albumImportedHandler, TagUpdatedMessageHandler tagUpdatedMessageHandler)
    {
        _albumUpdateHandler = albumUpdateHandler;
        _libraryRefreshHandler = libraryRefreshHandler;
        _albumImportedHandler = albumImportedHandler;
        _tagUpdatedHandler = tagUpdatedMessageHandler;

        _subscriptions.Add(messenger.Subscribe<AlbumUpdateMessage>(OnAlbumUpdateMessage));
        _subscriptions.Add(messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle));
        _subscriptions.Add(messenger.Subscribe<AlbumImportedMessage>(_albumImportedHandler.Handle));
        _subscriptions.Add(messenger.Subscribe<TagUpdatedMessage>(_tagUpdatedHandler.Handle));

        _albumUpdateHandler.DataChanged += OnLibraryChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _albumImportedHandler.AlbumImported += OnLibraryChanged;
        _tagUpdatedHandler.TagUpdated += OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => LibraryChanged?.Invoke(this, EventArgs.Empty);

    private void OnAlbumUpdateMessage(AlbumUpdateMessage message) => _ = _albumUpdateHandler.HandleAsync(message);

    public void ResetUpdateFlags()
    {
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _albumImportedHandler.ResetLibraryUpdatedFlag();
        _tagUpdatedHandler.ResetLibraryUpdatedFlag();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();

            _albumUpdateHandler.DataChanged -= OnLibraryChanged;
            _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
            _albumImportedHandler.AlbumImported -= OnLibraryChanged;
            _tagUpdatedHandler.TagUpdated -= OnLibraryChanged;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}