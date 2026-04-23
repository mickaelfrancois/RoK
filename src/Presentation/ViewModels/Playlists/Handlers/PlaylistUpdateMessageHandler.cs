using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlists.Services;

namespace Rok.ViewModels.Playlists.Handlers;

public class PlaylistUpdateMessageHandler(PlaylistsDataLoader dataLoader, ILogger<PlaylistUpdateMessageHandler> logger)
{
    public event EventHandler? DataChanged;

    public async Task HandleAsync(PlaylistUpdatedMessage message)
    {
        ActionType action = message.Action;
        PlaylistViewModel? existingPlaylist = dataLoader.ViewModels.FirstOrDefault(c => c.Playlist.Id == message.Id);

        if (action == ActionType.Add && existingPlaylist != null)
        {
            action = ActionType.Update;
        }

        if ((action == ActionType.Update || action == ActionType.Delete) && existingPlaylist == null)
        {
            logger.LogWarning("Playlist {Id} not found for {Action}.", message.Id, action);
            return;
        }

        PlaylistHeaderDto? playlistDto = null;

        if (action == ActionType.Update || action == ActionType.Add)
        {
            playlistDto = await dataLoader.GetPlaylistByIdAsync(message.Id);
            if (playlistDto == null)
            {
                logger.LogError("Failed to retrieve playlist {Id} for {Action}.", message.Id, action);
                return;
            }
        }

        switch (action)
        {
            case ActionType.Add:
                dataLoader.AddPlaylist(playlistDto!);
                logger.LogTrace("Playlist {Id} viewmodel added.", message.Id);
                break;

            case ActionType.Update:
                dataLoader.UpdatePlaylist(message.Id, playlistDto!);
                logger.LogTrace("Playlist {Id} viewmodel updated.", message.Id);
                break;

            case ActionType.Delete:
                dataLoader.RemovePlaylist(message.Id);
                logger.LogTrace("Playlist {Id} viewmodel removed.", message.Id);
                break;
        }

        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}