using Rok.Application.Features.Playlists.Command;
using Rok.Services;

namespace Rok.ViewModels.Playlists.Services;

public class PlaylistCreationService(
    IMediator mediator,
    NavigationService navigationService,
    ResourceLoader resourceLoader,
    ILogger<PlaylistCreationService> logger)
{
    public async Task<long?> CreateSmartPlaylistAsync()
    {
        CreatePlaylistCommand command = new()
        {
            Type = (int)PlaylistType.Smart,
            Name = resourceLoader.GetString("newPlaylist")
        };

        Result<long> result = await mediator.SendMessageAsync(command);
        if (result.IsError)
        {
            logger.LogError("Failed to create new smart playlist: {ErrorMessage}", result.Error);
            return null;
        }

        long playlistId = result.Value;
        Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));
        navigationService.NavigateToSmartPlaylist(playlistId);

        return playlistId;
    }

    public async Task<long?> CreateClassicPlaylistAsync()
    {
        CreatePlaylistCommand command = new()
        {
            Type = (int)PlaylistType.Classic,
            Name = resourceLoader.GetString("newPlaylist")
        };

        Result<long> result = await mediator.SendMessageAsync(command);
        if (result.IsError)
        {
            logger.LogError("Failed to create new classic playlist: {ErrorMessage}", result.Error);
            return null;
        }

        long playlistId = result.Value;
        Messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));
        navigationService.NavigateToPlaylist(playlistId);

        return playlistId;
    }
}