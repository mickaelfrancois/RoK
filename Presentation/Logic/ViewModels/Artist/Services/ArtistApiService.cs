using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Tracks.Command;
using Rok.Infrastructure.NovaApi;

namespace Rok.Logic.ViewModels.Artist.Services;

public class ArtistApiService(
    IMediator mediator,
    INovaApiService novaApiService,
    ArtistPictureService pictureService,
    BackdropPicture backdropPicture,
    ILogger<ArtistApiService> logger)
{
    public async Task<bool> GetAndUpdateArtistDataAsync(ArtistDto artist)
    {
        if (string.IsNullOrEmpty(artist.Name))
            return false;

        if (!NovaApiService.IsApiRetryAllowed(artist.GetMetaDataLastAttempt))
            return false;

        await mediator.SendMessageAsync(new UpdateArtistGetMetaDataLastAttemptCommand(artist.Id));

        ApiArtistModel? artistApi = await novaApiService.GetArtistAsync(artist.Name);
        if (artistApi == null)
            return false;

        bool anyUpdate = false;

        if (!string.IsNullOrEmpty(artistApi.MusicBrainzID))
        {
            await DownloadPictureIfNeededAsync(artist, artistApi);
            await DownloadBackdropsIfNeededAsync(artist, artistApi);
        }

        anyUpdate |= await UpdateArtistDataIfNeededAsync(artist, artistApi);

        return anyUpdate;
    }

    private async Task DownloadPictureIfNeededAsync(ArtistDto artist, ApiArtistModel artistApi)
    {
        if (pictureService.PictureExists(artist.Name))
            return;

        string picturePath = pictureService.GetPictureFilePath(artist.Name);
        await novaApiService.GetArtistPictureAsync(artistApi.MusicBrainzID!, "artists", picturePath);
    }

    private async Task DownloadBackdropsIfNeededAsync(ArtistDto artist, ApiArtistModel artistApi)
    {
        if (backdropPicture.HasBackdrops(artist.Name))
            return;

        string backdropFolder = backdropPicture.GetArtistPictureFolder(artist.Name);
        await novaApiService.GetArtistBackdropsAsync(artistApi.MusicBrainzID!, artistApi.FanartsCount, backdropFolder);
    }

    private async Task<bool> UpdateArtistDataIfNeededAsync(ArtistDto artist, ApiArtistModel artistApi)
    {
        if (!CompareArtistFromApi(artist, artistApi))
            return false;

        logger.LogTrace("Patch artist '{Name}' from API response.", artist.Name);

        PatchArtistCommand patchArtistCommand = new()
        {
            Id = artist.Id,
            WikipediaUrl = new PatchField<string>(artistApi.Wikipedia),
            OfficialSiteUrl = new PatchField<string>(artistApi.Website),
            FacebookUrl = new PatchField<string>(artistApi.Facebook),
            TwitterUrl = new PatchField<string>(artistApi.Twitter),
            MusicBrainzID = new PatchField<string>(artistApi.MusicBrainzID),
            Disbanded = new PatchField<bool>(artistApi.IsDisbanded),
            BornYear = new PatchField<int>(artistApi.BornYear.GetValueOrDefault()),
            DiedYear = new PatchField<int>(artistApi.DiedYear.GetValueOrDefault()),
            FormedYear = new PatchField<int>(artistApi.FormedYear.GetValueOrDefault()),
            Gender = new PatchField<string>(artistApi.Gender),
            Mood = new PatchField<string>(artistApi.Mood),
            Style = new PatchField<string>(artistApi.Style),
            Biography = new PatchField<string>(artistApi.GetBiography(LanguageHelpers.GetCurrentLanguage())),
            NovaUid = new PatchField<string>(artistApi.ID?.ToString() ?? "")
        };

        await mediator.SendMessageAsync(patchArtistCommand);

        return true;
    }

    private static bool CompareArtistFromApi(ArtistDto artist, ApiArtistModel artistApi)
    {
        if (artist.TwitterUrl.AreDifferents(artistApi.Twitter)) return true;
        if (artist.OfficialSiteUrl.AreDifferents(artistApi.Website)) return true;
        if (artist.FacebookUrl.AreDifferents(artistApi.Facebook)) return true;
        if (artist.BornYear.AreDifferents(artistApi.BornYear)) return true;
        if (artist.Disbanded != artistApi.IsDisbanded) return true;
        if (artist.DiedYear.AreDifferents(artistApi.DiedYear)) return true;
        if (artist.FormedYear.AreDifferents(artistApi.FormedYear)) return true;
        if (artist.Gender.AreDifferents(artistApi.Gender)) return true;
        if (artist.Mood.AreDifferents(artistApi.Mood)) return true;
        if (artist.Style.AreDifferents(artistApi.Style)) return true;
        if (artist.Biography.AreDifferents(artistApi.GetBiography(LanguageHelpers.GetCurrentLanguage()))) return true;

        return false;
    }
}