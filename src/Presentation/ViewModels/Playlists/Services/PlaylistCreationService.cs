using Rok.Application.Features.Playlists.Requests;

namespace Rok.ViewModels.Playlists.Services;

public class PlaylistCreationService(
    IMediator mediator,
    IMessenger messenger,
    NavigationService navigationService,
    ResourceLoader resourceLoader,
    ILogger<PlaylistCreationService> logger)
{
    public async Task<long?> CreateSmartPlaylistAsync()
    {
        CreatePlaylistRequest command = new()
        {
            Type = (int)PlaylistType.Smart,
            Name = resourceLoader.GetString("newPlaylist")
        };

        Result<long> result = await mediator.Send(command);
        if (result.IsFailure)
        {
            logger.LogError("Failed to create new smart playlist: {ErrorMessage}", result.Errors[0]);
            return null;
        }

        long playlistId = result.Value;
        messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));
        navigationService.NavigateToSmartPlaylist(playlistId);

        return playlistId;
    }

    public async Task<long?> CreateClassicPlaylistAsync()
    {
        CreatePlaylistRequest command = new()
        {
            Type = (int)PlaylistType.Classic,
            Name = resourceLoader.GetString("newPlaylist")
        };

        Result<long> result = await mediator.Send(command);
        if (result.IsFailure)
        {
            logger.LogError("Failed to create new classic playlist: {ErrorMessage}", result.Errors[0]);
            return null;
        }

        long playlistId = result.Value;
        messenger.Send(new PlaylistUpdatedMessage(playlistId, ActionType.Add));
        navigationService.NavigateToPlaylist(playlistId);

        return playlistId;
    }
}