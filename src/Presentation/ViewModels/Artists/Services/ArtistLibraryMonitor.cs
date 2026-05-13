using Rok.ViewModels.Albums.Handlers;
using Rok.ViewModels.Artists.Handlers;
using Rok.ViewModels.Artists.Interfaces;

namespace Rok.ViewModels.Artists.Services;

public partial class ArtistLibraryMonitor : IArtistLibraryMonitor
{
    private readonly ArtistUpdateMessageHandler _artistUpdateHandler;
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly ArtistImportedMessageHandler _artistImportedHandler;
    private readonly TagUpdatedMessageHandler _tagUpdatedHandler;
    private bool _disposed;

    public event EventHandler? LibraryChanged;

    public ArtistLibraryMonitor(ArtistUpdateMessageHandler albumUpdateHandler, LibraryRefreshMessageHandler libraryRefreshHandler, ArtistImportedMessageHandler albumImportedHandler, TagUpdatedMessageHandler tagUpdatedMessageHandler)
    {
        _artistUpdateHandler = albumUpdateHandler;
        _libraryRefreshHandler = libraryRefreshHandler;
        _artistImportedHandler = albumImportedHandler;
        _tagUpdatedHandler = tagUpdatedMessageHandler;

        Messenger.Subscribe<ArtistUpdateMessage>(OnArtistUpdateMessage);
        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<ArtistImportedMessage>(_artistImportedHandler.Handle);
        Messenger.Subscribe<TagUpdatedMessage>(_tagUpdatedHandler.Handle);

        _artistUpdateHandler.DataChanged += OnLibraryChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _artistImportedHandler.ArtistImported += OnLibraryChanged;
        _tagUpdatedHandler.TagUpdated += OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => LibraryChanged?.Invoke(this, EventArgs.Empty);

    private void OnArtistUpdateMessage(ArtistUpdateMessage message) => _ = _artistUpdateHandler.HandleAsync(message);

    public void ResetUpdateFlags()
    {
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _artistImportedHandler.ResetLibraryUpdatedFlag();
        _tagUpdatedHandler.ResetLibraryUpdatedFlag();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Messenger.Unsubscribe<ArtistUpdateMessage>(OnArtistUpdateMessage);
            Messenger.Unsubscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
            Messenger.Unsubscribe<ArtistImportedMessage>(_artistImportedHandler.Handle);
            Messenger.Unsubscribe<TagUpdatedMessage>(_tagUpdatedHandler.Handle);

            _artistUpdateHandler.DataChanged -= OnLibraryChanged;
            _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
            _artistImportedHandler.ArtistImported -= OnLibraryChanged;
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