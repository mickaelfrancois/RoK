using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Services;

namespace Rok.ViewModels.Artists.Handlers;

public class ArtistUpdateMessageHandler(ArtistsDataLoader dataLoader, ILogger<ArtistUpdateMessageHandler> logger)
{
    public event EventHandler? DataChanged;

    public async Task HandleAsync(ArtistUpdateMessage message)
    {
        ActionType action = message.Action;
        ArtistViewModel? existingArtist = dataLoader.ViewModels.FirstOrDefault(c => c.Artist.Id == message.Id);

        if (action == ActionType.Add && existingArtist != null)
        {
            action = ActionType.Update;
        }

        if ((action == ActionType.Update || action == ActionType.Delete) && existingArtist == null)
        {
            logger.LogWarning("Artist {Id} not found for {Action}.", message.Id, action);
            return;
        }

        ArtistDto? artistDto = null;

        if (action == ActionType.Update || action == ActionType.Add)
        {
            artistDto = await dataLoader.GetArtistByIdAsync(message.Id);
            if (artistDto == null)
            {
                logger.LogError("Failed to retrieve artist {Id} for {Action}.", message.Id, action);
                return;
            }
        }

        switch (action)
        {
            case ActionType.Add:
                dataLoader.AddArtist(artistDto!);
                logger.LogTrace("Artist {Id} viewmodel added.", message.Id);
                break;

            case ActionType.Update:
                dataLoader.UpdateArtist(message.Id, artistDto!);
                logger.LogTrace("Artist {Id} viewmodel updated.", message.Id);
                break;

            case ActionType.Delete:
                dataLoader.RemoveArtist(message.Id);
                logger.LogTrace("Artist {Id} viewmodel removed.", message.Id);
                break;

            case ActionType.Picture:
                dataLoader.RefreshArtistPicture(message.Id);
                logger.LogTrace("Artist {Id} picture updated.", message.Id);
                break;
        }

        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}