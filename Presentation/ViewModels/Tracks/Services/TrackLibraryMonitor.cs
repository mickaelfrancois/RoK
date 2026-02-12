using Rok.ViewModels.Albums.Handlers;
using Rok.ViewModels.Tracks.Handlers;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Tracks.Services;

public partial class TrackLibraryMonitor : ITrackLibraryMonitor
{
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly TrackImportedMessageHandler _trackImportedHandler;
    private bool _disposed;

    public event EventHandler? LibraryChanged;

    public TrackLibraryMonitor(LibraryRefreshMessageHandler libraryRefreshHandler, TrackImportedMessageHandler trackImportedHandler)
    {
        _libraryRefreshHandler = libraryRefreshHandler;
        _trackImportedHandler = trackImportedHandler;

        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<AlbumImportedMessage>(_trackImportedHandler.Handle);

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