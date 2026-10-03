using Rok.Application.Messages;
using Rok.Shared.Enums;

namespace Rok.ViewModels.Listening.Services;

/// <summary>
/// Watches artist and album update messages and reports the ones that concern the entities shown in the header.
/// </summary>
public sealed class ListeningEntityUpdateWatcher : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Lock _lock = new();
    private long? _artistId;
    private long? _albumId;
    private bool _disposed;

    public ListeningEntityUpdateWatcher(IMessenger messenger)
    {
        _subscriptions.Add(messenger.Subscribe<ArtistUpdateMessage>(OnArtistUpdate));
        _subscriptions.Add(messenger.Subscribe<AlbumUpdateMessage>(OnAlbumUpdate));
    }

    /// <summary>Raised when the tracked artist was updated or its picture changed. May be raised from any thread.</summary>
    public event EventHandler<long>? TrackedArtistUpdated;

    /// <summary>Raised when the tracked album was updated or its picture changed. May be raised from any thread.</summary>
    public event EventHandler<long>? TrackedAlbumUpdated;

    /// <summary>Sets the artist and album currently displayed in the header.</summary>
    public void SetTracked(long? artistId, long? albumId)
    {
        lock (_lock)
        {
            _artistId = artistId;
            _albumId = albumId;
        }
    }

    private void OnArtistUpdate(ArtistUpdateMessage message)
    {
        if (!IsRefreshAction(message.Action))
        {
            return;
        }

        long? tracked;

        lock (_lock)
        {
            tracked = _artistId;
        }

        if (tracked == message.Id)
        {
            TrackedArtistUpdated?.Invoke(this, message.Id);
        }
    }

    private void OnAlbumUpdate(AlbumUpdateMessage message)
    {
        if (!IsRefreshAction(message.Action))
        {
            return;
        }

        long? tracked;

        lock (_lock)
        {
            tracked = _albumId;
        }

        if (tracked == message.Id)
        {
            TrackedAlbumUpdated?.Invoke(this, message.Id);
        }
    }

    private static bool IsRefreshAction(ActionType action) => action is ActionType.Update or ActionType.Picture;

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }
}