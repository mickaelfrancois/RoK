using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Albums.Interfaces;

namespace Rok.Logic.ViewModels.Albums.Services;

public partial class AlbumLibraryMonitor : IAlbumLibraryMonitor
{
    private readonly AlbumUpdateMessageHandler _albumUpdateHandler;
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly AlbumImportedMessageHandler _albumImportedHandler;
    private readonly TagUpdatedMessageHandler _tagUpdatedHandler;
    private bool _disposed;

    public event EventHandler? LibraryChanged;

    public AlbumLibraryMonitor(AlbumUpdateMessageHandler albumUpdateHandler, LibraryRefreshMessageHandler libraryRefreshHandler, AlbumImportedMessageHandler albumImportedHandler, TagUpdatedMessageHandler tagUpdatedMessageHandler)
    {
        _albumUpdateHandler = albumUpdateHandler;
        _libraryRefreshHandler = libraryRefreshHandler;
        _albumImportedHandler = albumImportedHandler;
        _tagUpdatedHandler = tagUpdatedMessageHandler;

        Messenger.Subscribe<AlbumUpdateMessage>(async message => await _albumUpdateHandler.HandleAsync(message));
        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<AlbumImportedMessage>(_albumImportedHandler.Handle);
        Messenger.Subscribe<TagUpdatedMessage>(_tagUpdatedHandler.Handle);

        _albumUpdateHandler.DataChanged += OnLibraryChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _albumImportedHandler.AlbumImported += OnLibraryChanged;
        _tagUpdatedHandler.TagUpdated += OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => LibraryChanged?.Invoke(this, EventArgs.Empty);

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