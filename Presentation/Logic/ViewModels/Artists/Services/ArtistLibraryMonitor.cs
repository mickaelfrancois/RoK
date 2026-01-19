using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Artists.Handlers;
using Rok.Logic.ViewModels.Artists.Interfaces;

namespace Rok.Logic.ViewModels.Artists.Services;

public partial class ArtistLibraryMonitor : IArtistLibraryMonitor
{
    private readonly ArtistUpdateMessageHandler _artistUpdateHandler;
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly ArtistImportedMessageHandler _artistImportedHandler;
    private bool _disposed;

    public event EventHandler? LibraryChanged;

    public ArtistLibraryMonitor(ArtistUpdateMessageHandler albumUpdateHandler, LibraryRefreshMessageHandler libraryRefreshHandler, ArtistImportedMessageHandler albumImportedHandler)
    {
        _artistUpdateHandler = albumUpdateHandler;
        _libraryRefreshHandler = libraryRefreshHandler;
        _artistImportedHandler = albumImportedHandler;

        Messenger.Subscribe<ArtistUpdateMessage>(async message => await _artistUpdateHandler.HandleAsync(message));
        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<ArtistImportedMessage>(_artistImportedHandler.Handle);

        _artistUpdateHandler.DataChanged += OnLibraryChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _artistImportedHandler.ArtistImported += OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => LibraryChanged?.Invoke(this, EventArgs.Empty);

    public void ResetUpdateFlags()
    {
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _artistImportedHandler.ResetLibraryUpdatedFlag();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _artistUpdateHandler.DataChanged -= OnLibraryChanged;
            _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
            _artistImportedHandler.ArtistImported -= OnLibraryChanged;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}