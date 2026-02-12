using System.Threading;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Services.Player;


namespace Rok.Services;

public partial class PlaylistMenuService : IPlaylistMenuService, IDisposable
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;
    private readonly ILogger<PlaylistMenuService> _logger;
    private readonly IPlayerService _playerService;

    public event EventHandler? PlaylistsChanged;

    private IEnumerable<PlaylistMenuItem>? _cachedPlaylistItems;
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);


    public PlaylistMenuService(IMediator mediator, IPlayerService playerService, ResourceLoader resourceManager, ILogger<PlaylistMenuService> logger)
    {
        _mediator = mediator;
        _playerService = playerService;
        _resourceLoader = resourceManager;
        _logger = logger;

        Messenger.Subscribe<PlaylistUpdatedMessage>(_ => OnPlaylistsChanged());
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


    public async Task AddAlbumToPlaylistAsync(long playlistId, long albumId)
    {
        try
        {
            Result<long> result = await _mediator.SendMessageAsync(new AddAlbumToPlaylistCommand
            {
                PlaylistId = playlistId,
                AlbumId = albumId
            });

            if (result.IsSuccess)
            {
                Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Update));
                Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add")!, Type = NotificationType.Success });

                _logger.LogInformation("Album '{AlbumId}' add to playlist '{PlaylistId}'", albumId, playlistId);
            }
            else
            {
                _logger.LogError("Failed to add track '{AlbumId}' to playlist '{PlaylistId}': {Error}", albumId, playlistId, result.Error);
            }
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to playlist");
        }
    }


    public async Task AddArtistToPlaylistAsync(long playlistId, long artistId)
    {
        try
        {
            Result<long> result = await _mediator.SendMessageAsync(new AddArtistToPlaylistCommand
            {
                PlaylistId = playlistId,
                ArtistId = artistId
            });

            if (result.IsSuccess)
            {
                Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Update));
                Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add")!, Type = NotificationType.Success });

                _logger.LogInformation("Album '{ArtistId}' add to playlist '{PlaylistId}'", artistId, playlistId);
            }
            else
            {
                _logger.LogError("Failed to add track '{ArtistId}' to playlist '{PlaylistId}': {Error}", artistId, playlistId, result.Error);
            }
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to playlist");
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


    public async Task CreateNewPlaylistWithAlbumAsync(string playlistName, long albumId)
    {
        try
        {
            Result<long> playlistResult = await _mediator.SendMessageAsync(new CreatePlaylistCommand() { Name = playlistName, Type = (int)PlaylistType.Classic });
            long playlistId = playlistResult.Value;
            Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));

            await AddAlbumToPlaylistAsync(playlistId, albumId);
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to playlist");
        }
    }


    public async Task CreateNewPlaylistWithArtistAsync(string playlistName, long artistId)
    {
        try
        {
            Result<long> playlistResult = await _mediator.SendMessageAsync(new CreatePlaylistCommand() { Name = playlistName, Type = (int)PlaylistType.Classic });
            long playlistId = playlistResult.Value;
            Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));

            await AddAlbumToPlaylistAsync(playlistId, artistId);
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to playlist");
        }
    }


    public async Task AddArtistToCurrentListeningAsync(long artistId)
    {
        try
        {
            IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByArtistIdQuery(artistId));
            if (tracks == null || !tracks.Any())
            {
                Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
                _logger.LogWarning("No tracks found for artist '{ArtistId}'", artistId);
                return;
            }

            _playerService.AddTracksToPlaylist(tracks.ToList());
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to current listening");
        }
    }

    public async Task AddAlbumToCurrentListeningAsync(long albumId)
    {
        try
        {
            IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByAlbumIdQuery(albumId));
            if (tracks == null || !tracks.Any())
            {
                Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
                _logger.LogWarning("No tracks found for album '{AlbumId}'", albumId);
                return;
            }

            _playerService.AddTracksToPlaylist(tracks.ToList());
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to current listening");
        }
    }

    public async Task AddTrackToCurrentListeningAsync(long trackId)
    {
        try
        {
            Result<TrackDto> trackResult = await _mediator.SendMessageAsync(new GetTrackByIdQuery(trackId));
            if (!trackResult.IsSuccess || trackResult.Value == null)
            {
                Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
                _logger.LogWarning("No track found for id '{TrackId}'", trackId);
                return;
            }

            _playerService.AddTracksToPlaylist(new List<TrackDto> { trackResult.Value });
        }
        catch (Exception ex)
        {
            Messenger.Send(new ShowNotificationMessage() { Message = _resourceLoader.GetString("notification_playlist_track_add_error")!, Type = NotificationType.Error });
            _logger.LogError(ex, "Error while add tracks to current listening");
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