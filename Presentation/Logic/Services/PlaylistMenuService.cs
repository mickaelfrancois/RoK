using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Playlists.Query;
using System.Threading;


namespace Rok.Logic.Services;

public class PlaylistMenuService : IPlaylistMenuService, IDisposable
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;
    private readonly ILogger<PlaylistMenuService> _logger;

    public event EventHandler? PlaylistsChanged;

    private IEnumerable<PlaylistMenuItem>? _cachedPlaylistItems;
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);


    public PlaylistMenuService(IMediator mediator, ResourceLoader resourceManager, ILogger<PlaylistMenuService> logger)
    {
        _mediator = mediator;
        _resourceLoader = resourceManager;
        _logger = logger;

        Messenger.Subscribe<PlaylistCreatedMessage>(_ => OnPlaylistsChanged());
        Messenger.Subscribe<PlaylistNameUpdatedMessage>(_ => OnPlaylistsChanged());
        Messenger.Subscribe<PlaylistDeletedMessage>(_ => OnPlaylistsChanged());
    }


    public async Task<IEnumerable<PlaylistMenuItem>> GetPlaylistMenuItemsAsync()
    {
        await _cacheSemaphore.WaitAsync();

        try
        {
            if (_cachedPlaylistItems == null)
                return await RefreshCacheAsync();
            else
                return _cachedPlaylistItems;
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }


    public async Task AddTrackToPlaylistAsync(long playlistId, long trackId)
    {
        try
        {
            Result<long> result = await _mediator.SendMessageAsync(new AddTrackToPlaylistCommand
            {
                PlaylistId = playlistId,
                TrackId = trackId
            });

            if (result.IsSuccess)
            {
                Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Update));
                Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add")!, Type = NotificationType.Success });

                _logger.LogInformation("Track '{TrackId}' add to playlist '{PlaylistId}'", trackId, playlistId);
            }
            else
            {
                if (result.Error!.Code == "DUPLICATE")
                {
                    Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_duplicate")!, Type = NotificationType.Warning });
                    _logger.LogWarning("Track '{TrackId}' already exists in playlist '{PlaylistId}'", trackId, playlistId);
                }
                else
                {
                    Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
                    _logger.LogError("Failed to add track '{TrackId}' to playlist '{PlaylistId}': {Error}", trackId, playlistId, result.Error);
                }
            }
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add track to playlist");
        }
    }


    public async Task CreateNewPlaylistWithTrackAsync(string playlistName, long trackId)
    {
        try
        {
            Result<long> playlistResult = await _mediator.SendMessageAsync(new CreatePlaylistCommand() { Name = playlistName, Type = (int)PlaylistType.Classic });
            long playlistId = playlistResult.Value;
            Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));

            await AddTrackToPlaylistAsync(playlistId, trackId);
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add track to playlist");
        }
    }


    private async Task<IEnumerable<PlaylistMenuItem>> RefreshCacheAsync()
    {
        try
        {
            IEnumerable<PlaylistHeaderDto> playlists = await _mediator.SendMessageAsync(new GetAllPlaylistsQuery() { FilterType = PlaylistType.Classic });

            _cachedPlaylistItems = playlists.Select(p => new PlaylistMenuItem
            {
                Id = p.Id,
                Name = p.Name,
                Icon = "\uE90B" // Icon playlist
            }).ToList();

            return _cachedPlaylistItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting playlists");
            return _cachedPlaylistItems ?? Enumerable.Empty<PlaylistMenuItem>();
        }
    }


    private void InvalidateCache()
    {
        _cachedPlaylistItems = null;
    }


    private void OnPlaylistsChanged()
    {
        InvalidateCache();
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }


    public void Dispose()
    {
        _cacheSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}