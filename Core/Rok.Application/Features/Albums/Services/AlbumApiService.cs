using Microsoft.Extensions.Logging;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Albums.Command;
using Rok.Application.Interfaces;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Albums.Services;

public class AlbumApiService(
    IMediator mediator,
    IMusicDataApiService musicDataApiService,
    ILogger<AlbumApiService> logger)
{
    public async Task<bool> GetAndUpdateAlbumDataAsync(AlbumDto album, IAlbumPictureService pictureService)
    {
        if (string.IsNullOrEmpty(album.Name) || string.IsNullOrEmpty(album.ArtistName))
            return false;

        if (!musicDataApiService.IsApiRetryAllowed(album.GetMetaDataLastAttempt))
            return false;

        await mediator.SendMessageAsync(new UpdateAlbumGetMetaDataLastAttemptCommand(album.Id));
        album.GetMetaDataLastAttempt = DateTime.UtcNow;

        MusicDataAlbumDto? albumApi = await musicDataApiService.GetAlbumAsync(album.Name, album.ArtistName, album.MusicBrainzID, album.ArtistMusicBrainzID);
        if (albumApi == null)
            return false;

        if (!string.IsNullOrEmpty(albumApi.MusicBrainzID))
        {
            await DownloadPictureIfNeededAsync(album, albumApi, pictureService, CancellationToken.None);

            if (CompareAlbumFromApi(album, albumApi))
                return await UpdateAlbumDataIfNeededAsync(album, albumApi);
        }

        return false;
    }

    private async Task DownloadPictureIfNeededAsync(AlbumDto album, MusicDataAlbumDto albumApi, IAlbumPictureService pictureService, CancellationToken cancellationToken)
    {
        if (pictureService.PictureExists(album.AlbumPath))
            return;

        if (string.IsNullOrEmpty(albumApi.MusicBrainzID))
            return;

        string picturePath = pictureService.GetPictureFilePath(album.AlbumPath);
        await musicDataApiService.DownloadCoverAsync(albumApi, picturePath, cancellationToken);
    }

    private async Task<bool> UpdateAlbumDataIfNeededAsync(AlbumDto album, MusicDataAlbumDto albumApi)
    {
        logger.LogTrace("Patch album '{Name}' from API response.", album.Name);

        UpdateAlbumCommand command = new()
        {
            Id = album.Id
        };

        if (!string.IsNullOrEmpty(albumApi.Label))
            command.Label.Set(albumApi.Label);
        if (!string.IsNullOrEmpty(albumApi.Sales))
            command.Sales.Set(albumApi.Sales);
        if (!string.IsNullOrEmpty(albumApi.MusicBrainzID))
            command.MusicBrainzID.Set(albumApi.MusicBrainzID);
        if (albumApi.ReleaseDate.HasValue)
            command.ReleaseDate.Set(albumApi.ReleaseDate);
        if (!string.IsNullOrEmpty(albumApi.Wikipedia))
            command.Wikipedia.Set(albumApi.Wikipedia);
        if (!string.IsNullOrEmpty(albumApi.AllMusicID))
            command.AllMusicID.Set(albumApi.AllMusicID);
        if (!string.IsNullOrEmpty(albumApi.AmazonID))
            command.AmazonID.Set(albumApi.AmazonID);
        if (!string.IsNullOrEmpty(albumApi.AudioDbArtistID))
            command.AudioDbArtistID.Set(albumApi.AudioDbArtistID);
        if (!string.IsNullOrEmpty(albumApi.AudioDbID))
            command.AudioDbID.Set(albumApi.AudioDbID);
        if (!string.IsNullOrEmpty(albumApi.DiscogsID))
            command.DiscogsID.Set(albumApi.DiscogsID);
        if (!string.IsNullOrEmpty(albumApi.GeniusID))
            command.GeniusID.Set(albumApi.GeniusID);
        if (!string.IsNullOrEmpty(albumApi.LyricWikiID))
            command.LyricWikiID.Set(albumApi.LyricWikiID);
        if (!string.IsNullOrEmpty(albumApi.MusicMozID))
            command.MusicMozID.Set(albumApi.MusicMozID);
        if (!string.IsNullOrEmpty(albumApi.ReleaseGroupMusicBrainzID))
            command.ReleaseGroupMusicBrainzID.Set(albumApi.ReleaseGroupMusicBrainzID);
        if (!string.IsNullOrEmpty(albumApi.WikidataID))
            command.WikidataID.Set(albumApi.WikidataID);
        if (!string.IsNullOrEmpty(albumApi.WikipediaID))
            command.WikipediaID.Set(albumApi.WikipediaID);
        if (string.IsNullOrWhiteSpace(album.Biography) && !string.IsNullOrWhiteSpace(albumApi.Biography))
            command.Biography.Set(albumApi.Biography);

        await mediator.SendMessageAsync(command);

        return true;
    }

    private static bool CompareAlbumFromApi(AlbumDto album, MusicDataAlbumDto albumApi)
    {
        if (album.Label.AreDifferents(albumApi.Label)) return true;
        if (album.Sales.AreDifferents(albumApi.Sales)) return true;
        if (album.MusicBrainzID.AreDifferents(albumApi.MusicBrainzID)) return true;
        if (album.ReleaseDate != albumApi.ReleaseDate) return true;
        if (album.ReleaseFormat.AreDifferents(albumApi.ReleaseFormat)) return true;
        if (album.Wikipedia.AreDifferents(albumApi.Wikipedia)) return true;
        if (album.AllMusicID.AreDifferents(albumApi.AllMusicID)) return true;
        if (album.AmazonID.AreDifferents(albumApi.AmazonID)) return true;
        if (album.AudioDbArtistID.AreDifferents(albumApi.AudioDbArtistID)) return true;
        if (album.AudioDbID.AreDifferents(albumApi.AudioDbID)) return true;
        if (album.DiscogsID.AreDifferents(albumApi.DiscogsID)) return true;
        if (album.GeniusID.AreDifferents(albumApi.GeniusID)) return true;
        if (album.LyricWikiID.AreDifferents(albumApi.LyricWikiID)) return true;
        if (album.MusicMozID.AreDifferents(albumApi.MusicMozID)) return true;
        if (album.ReleaseGroupMusicBrainzID.AreDifferents(albumApi.ReleaseGroupMusicBrainzID)) return true;
        if (album.WikidataID.AreDifferents(albumApi.WikidataID)) return true;
        if (album.WikipediaID.AreDifferents(albumApi.WikipediaID)) return true;

        return string.IsNullOrWhiteSpace(album.Biography) && !string.IsNullOrWhiteSpace(albumApi.Biography);
    }
}
