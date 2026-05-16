using Rok.ViewModels.Albums.Handlers;
using Rok.ViewModels.Tracks.Handlers;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Tracks.Services;

public partial class TrackLibraryMonitor : ITrackLibraryMonitor
{
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly TrackImportedMessageHandler _trackImportedHandler;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public event EventHandler? LibraryChanged;

    public TrackLibraryMonitor(IMessenger messenger, LibraryRefreshMessageHandler libraryRefreshHandler, TrackImportedMessageHandler trackImportedHandler)
    {
        _libraryRefreshHandler = libraryRefreshHandler;
        _trackImportedHandler = trackImportedHandler;

        _subscriptions.Add(messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle));
        _subscriptions.Add(messenger.Subscribe<AlbumImportedMessage>(_trackImportedHandler.Handle));

        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _trackImportedHandler.TrackImported += OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => LibraryChanged?.Invoke(this, EventArgs.Empty);

    public void ResetUpdateFlags()
    {
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _trackImportedHandler.ResetLibraryUpdatedFlag();
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

            _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
            _trackImportedHandler.TrackImported -= OnLibraryChanged;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}