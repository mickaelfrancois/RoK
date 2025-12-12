using Rok.Logic.ViewModels.Albums.Services;

namespace Rok.Logic.ViewModels.Albums.Handlers;

public class AlbumUpdateMessageHandler(AlbumsDataLoader dataLoader, ILogger<AlbumUpdateMessageHandler> logger)
{
    public event EventHandler? DataChanged;

    public async Task HandleAsync(AlbumUpdateMessage message)
    {
        ActionType action = message.Action;
        AlbumViewModel? existingAlbum = dataLoader.ViewModels.FirstOrDefault(c => c.Album.Id == message.Id);

        if (action == ActionType.Add && existingAlbum != null)
        {
            action = ActionType.Update;
        }

        if ((action == ActionType.Update || action == ActionType.Delete) && existingAlbum == null)
        {
            logger.LogWarning("Album {Id} not found for {Action}.", message.Id, action);
            return;
        }

        AlbumDto? albumDto = null;

        if (action == ActionType.Update || action == ActionType.Add)
        {
            albumDto = await dataLoader.GetAlbumByIdAsync(message.Id);
            if (albumDto == null)
            {
                logger.LogError("Failed to retrieve album {Id} for {Action}.", message.Id, action);
                return;
            }
        }

        switch (action)
        {
            case ActionType.Add:
                dataLoader.AddAlbum(albumDto!);
                logger.LogTrace("Album {Id} viewmodel added.", message.Id);
                break;

            case ActionType.Update:
                dataLoader.UpdateAlbum(message.Id, albumDto!);
                logger.LogTrace("Album {Id} viewmodel updated.", message.Id);
                break;

            case ActionType.Delete:
                dataLoader.RemoveAlbum(message.Id);
                logger.LogTrace("Album {Id} viewmodel removed.", message.Id);
                break;
        }

        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}