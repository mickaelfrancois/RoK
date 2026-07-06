using System.Globalization;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Mapping;

namespace Rok.ViewModels.Artist.Services;

public class ArtistEditService(IMediator mediator, IDialogService dialogService, ILogger<ArtistEditService> logger)
{
    public async Task<bool> EditArtistAsync(ArtistDto artist)
    {
        ArtistEditValues current = new()
        {
            MusicBrainzID = artist.MusicBrainzID,
            FormedYear = artist.FormedYear?.ToString(CultureInfo.InvariantCulture),
            BornYear = artist.BornYear?.ToString(CultureInfo.InvariantCulture),
            DiedYear = artist.DiedYear?.ToString(CultureInfo.InvariantCulture),
            Disbanded = artist.Disbanded,
            Members = artist.Members,
            Biography = artist.Biography
        };

        ArtistEditValues? edited = await dialogService.ShowEditArtistAsync(current);

        if (edited is null)
            return false;

        int? formedYear = ParseYear(edited.FormedYear);
        int? bornYear = ParseYear(edited.BornYear);
        int? diedYear = ParseYear(edited.DiedYear);

        // Start from the full command so URLs and other fields not shown in the dialog are
        // preserved, then overwrite only the edited subset.
        UpdateArtistRequest command = artist.ToCommand();
        command.MusicBrainzID = edited.MusicBrainzID;
        command.FormedYear = formedYear;
        command.BornYear = bornYear;
        command.DiedYear = diedYear;
        command.Disbanded = edited.Disbanded;
        command.Members = edited.Members;
        command.Biography = edited.Biography;

        await mediator.Send(command);

        artist.MusicBrainzID = edited.MusicBrainzID;
        artist.FormedYear = formedYear;
        artist.BornYear = bornYear;
        artist.DiedYear = diedYear;
        artist.Disbanded = edited.Disbanded;
        artist.Members = edited.Members;
        artist.Biography = edited.Biography;

        return true;
    }

    /// <summary>Parses a year typed in the dialog; empty or non-numeric input maps to <see langword="null"/>.</summary>
    private static int? ParseYear(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
            ? year
            : null;
    }

    public async Task UpdateFavoriteAsync(ArtistDto artist, bool isFavorite)
    {
        await mediator.Send(new UpdateArtistFavoriteRequest(artist.Id, isFavorite));
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

    public Task UpdateTagsAsync(long id, IEnumerable<string> tags)
    {
        return mediator.Send(new UpdateArtistTagsRequest(id, tags));
    }

    public Task UpdatePictureDominantColorAsync(long id, long? colorValue)
    {
        return mediator.Send(new UpdateArtistPictureDominantColorRequest(id, colorValue));
    }
}