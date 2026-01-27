using Rok.Application.Features.Artists.Command;

namespace Rok.Logic.ViewModels.Artist.Services;

public class ArtistEditService(IMediator mediator, ILogger<ArtistEditService> logger)
{
    public async Task UpdateFavoriteAsync(ArtistDto artist, bool isFavorite)
    {
        await mediator.SendMessageAsync(new UpdateArtistFavoriteCommand(artist.Id, isFavorite));
        artist.IsFavorite = isFavorite;
    }

    public async Task<bool> OpenOfficialSiteAsync(ArtistDto artist)
    {
        try
        {
            string? url = artist.OfficialSiteUrl;
            if (url == null)
                url = artist.WikipediaUrl;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url.Trim();
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                logger.LogWarning("Invalid URL for artist {ArtistName}: {Url}", artist.Name, url);
                return false;
            }

            bool success = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!success)
            {
                logger.LogWarning("Unable to open URL {Url} for artist {ArtistName}", url, artist.Name);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while opening URL for artist {ArtistName}", artist.Name);
            return false;
        }
    }

    public async Task UpdateTagsAsync(long id, IEnumerable<string> tags)
    {
        await mediator.SendMessageAsync(new UpdateArtistTagsCommand(id, tags));
    }
}