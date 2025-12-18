using Rok.Application.Features.Albums.Command;
using Rok.Infrastructure.NovaApi;

namespace Rok.Logic.ViewModels.Album.Services;


public class AlbumApiService(
    IMediator mediator,
    INovaApiService novaApiService,
    AlbumPictureService pictureService,
    ILogger<AlbumApiService> logger)
{
    public async Task<bool> GetAndUpdateAlbumDataAsync(AlbumDto album)
    {
        if (string.IsNullOrEmpty(album.Name) || string.IsNullOrEmpty(album.ArtistName))
            return false;

        if (!NovaApiService.IsApiRetryAllowed(album.GetMetaDataLastAttempt))
            return false;

        await mediator.SendMessageAsync(new UpdateAlbumGetMetaDataLastAttemptCommand(album.Id));

        ApiAlbumModel? albumApi = await novaApiService.GetAlbumAsync(album.Name, album.ArtistName);
        if (albumApi == null)
            return false;

        await DownloadPictureIfNeededAsync(album, albumApi);
        bool dataUpdated = await UpdateAlbumDataIfNeededAsync(album, albumApi);

        return dataUpdated;
    }

    private async Task DownloadPictureIfNeededAsync(AlbumDto album, ApiAlbumModel albumApi)
    {
        if (pictureService.PictureExists(album.AlbumPath))
            return;

        if (string.IsNullOrEmpty(albumApi.MusicBrainzID))
            return;

        string picturePath = pictureService.GetPictureFilePath(album.AlbumPath);
        await novaApiService.GetAlbumPicturesAsync(albumApi.MusicBrainzID, picturePath);
    }

    private async Task<bool> UpdateAlbumDataIfNeededAsync(AlbumDto album, ApiAlbumModel albumApi)
    {
        if (!CompareAlbumFromApi(album, albumApi))
            return false;

        logger.LogTrace("Patch album '{Name}' from API response.", album.Name);

        PatchAlbumCommand patchAlbumCommand = new()
        {
            Id = album.Id,
            Label = new PatchField<string>(albumApi.Label),
            Sales = new PatchField<string>(albumApi.Sales),
            Mood = new PatchField<string>(albumApi.Mood),
            MusicBrainzID = new PatchField<string>(albumApi.MusicBrainzID),
            Speed = new PatchField<string>(albumApi.Speed),
            ReleaseDate = new PatchField<DateTime?>(albumApi.ReleaseDate),
            ReleaseFormat = new PatchField<string>(albumApi.ReleaseFormat),
            Wikipedia = new PatchField<string>(albumApi.Wikipedia),
            Theme = new PatchField<string>(albumApi.Theme)
        };

        await mediator.SendMessageAsync(patchAlbumCommand);

        return true;
    }

    private static bool CompareAlbumFromApi(AlbumDto album, ApiAlbumModel albumApi)
    {
        if (album.Label.AreDifferents(albumApi.Label)) return true;
        if (album.Sales.AreDifferents(albumApi.Sales)) return true;
        if (album.Mood.AreDifferents(albumApi.Mood)) return true;
        if (album.MusicBrainzID.AreDifferents(albumApi.MusicBrainzID)) return true;
        if (album.Speed.AreDifferents(albumApi.Speed)) return true;
        if (album.ReleaseDate != albumApi.ReleaseDate) return true;
        if (album.ReleaseFormat.AreDifferents(albumApi.ReleaseFormat)) return true;
        if (album.Wikipedia.AreDifferents(albumApi.Wikipedia)) return true;
        if (album.Theme.AreDifferents(albumApi.Theme)) return true;

        return false;
    }
}