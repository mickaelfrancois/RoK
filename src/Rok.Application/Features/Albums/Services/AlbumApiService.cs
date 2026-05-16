using Microsoft.Extensions.Logging;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Albums.Services;

public class AlbumApiService(
    IMediator mediator,
    IMusicDataApiService musicDataApiService,
    ILogger<AlbumApiService> logger) : IAlbumApiService
{
    public async Task<AlbumApiUpdateResult> GetAndUpdateAlbumDataAsync(AlbumDto album, IAlbumPictureService pictureService, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(album.Name) || string.IsNullOrEmpty(album.ArtistName))
            return AlbumApiUpdateResult.None;

        if (!musicDataApiService.IsApiRetryAllowed(album.GetMetaDataLastAttempt))
            return AlbumApiUpdateResult.None;

        await mediator.Send(new UpdateAlbumGetMetaDataLastAttemptRequest(album.Id));
        album.GetMetaDataLastAttempt = DateTime.UtcNow;

        MusicDataAlbumDto? albumApi = await musicDataApiService.GetAlbumAsync(album.Name, album.ArtistName, album.MusicBrainzID, album.ArtistMusicBrainzID);
        if (albumApi == null)
            return AlbumApiUpdateResult.None;

        if (string.IsNullOrEmpty(albumApi.MusicBrainzID))
            return AlbumApiUpdateResult.None;

        bool pictureDownloaded = await DownloadPictureIfNeededAsync(album, albumApi, pictureService, cancellationToken);

        bool dataUpdated = false;
        if (CompareAlbumFromApi(album, albumApi))
            dataUpdated = await UpdateAlbumDataIfNeededAsync(album, albumApi);

        return new AlbumApiUpdateResult(dataUpdated, pictureDownloaded);
    }

    private async Task<bool> DownloadPictureIfNeededAsync(AlbumDto album, MusicDataAlbumDto albumApi, IAlbumPictureService pictureService, CancellationToken cancellationToken)
    {
        if (pictureService.PictureExists(album.AlbumPath))
            return false;

        if (string.IsNullOrEmpty(albumApi.MusicBrainzID))
            return false;

        string picturePath = pictureService.GetPictureFilePath(album.AlbumPath);
        await musicDataApiService.DownloadCoverAsync(albumApi, picturePath, cancellationToken);

        return pictureService.PictureExists(album.AlbumPath);
    }

    private async Task<bool> UpdateAlbumDataIfNeededAsync(AlbumDto album, MusicDataAlbumDto albumApi)
    {
        logger.LogTrace("Patch album '{Name}' from API response.", album.Name);

        UpdateAlbumRequest command = new()
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

        await mediator.Send(command);

        return true;
    }

    private static bool CompareAlbumFromApi(AlbumDto album, MusicDataAlbumDto albumApi)
    {
        if (album.Label.AreDifferent(albumApi.Label)) return true;
        if (album.Sales.AreDifferent(albumApi.Sales)) return true;
        if (album.MusicBrainzID.AreDifferent(albumApi.MusicBrainzID)) return true;
        if (album.ReleaseDate != albumApi.ReleaseDate) return true;
        if (album.ReleaseFormat.AreDifferent(albumApi.ReleaseFormat)) return true;
        if (album.Wikipedia.AreDifferent(albumApi.Wikipedia)) return true;
        if (album.AllMusicID.AreDifferent(albumApi.AllMusicID)) return true;
        if (album.AmazonID.AreDifferent(albumApi.AmazonID)) return true;
        if (album.AudioDbArtistID.AreDifferent(albumApi.AudioDbArtistID)) return true;
        if (album.AudioDbID.AreDifferent(albumApi.AudioDbID)) return true;
        if (album.DiscogsID.AreDifferent(albumApi.DiscogsID)) return true;
        if (album.GeniusID.AreDifferent(albumApi.GeniusID)) return true;
        if (album.LyricWikiID.AreDifferent(albumApi.LyricWikiID)) return true;
        if (album.MusicMozID.AreDifferent(albumApi.MusicMozID)) return true;
        if (album.ReleaseGroupMusicBrainzID.AreDifferent(albumApi.ReleaseGroupMusicBrainzID)) return true;
        if (album.WikidataID.AreDifferent(albumApi.WikidataID)) return true;
        if (album.WikipediaID.AreDifferent(albumApi.WikipediaID)) return true;

        return string.IsNullOrWhiteSpace(album.Biography) && !string.IsNullOrWhiteSpace(albumApi.Biography);
    }
}
