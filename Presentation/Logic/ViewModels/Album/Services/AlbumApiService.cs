using System.Threading;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Albums.Command;
using Rok.Infrastructure.MusicData;

namespace Rok.Logic.ViewModels.Album.Services;


public class AlbumApiService(
    IMediator mediator,
    IMusicDataApiService musicDataApiService,
    AlbumPictureService pictureService,
    ILogger<AlbumApiService> logger)
{
    public async Task<bool> GetAndUpdateAlbumDataAsync(AlbumDto album)
    {
        if (string.IsNullOrEmpty(album.Name) || string.IsNullOrEmpty(album.ArtistName))
            return false;

        if (!MusicDataApiService.IsApiRetryAllowed(album.GetMetaDataLastAttempt))
            return false;

        await mediator.SendMessageAsync(new UpdateAlbumGetMetaDataLastAttemptCommand(album.Id));
        album.GetMetaDataLastAttempt = DateTime.UtcNow;

        MusicDataAlbumDto? albumApi = await musicDataApiService.GetAlbumAsync(album.Name, album.ArtistName, album.MusicBrainzID);
        if (albumApi == null)
            return false;

        if (!string.IsNullOrEmpty(albumApi.MusicBrainzID))
        {
            await DownloadPictureIfNeededAsync(album, albumApi, CancellationToken.None);

            if (CompareAlbumFromApi(album, albumApi))
                return await UpdateAlbumDataIfNeededAsync(album, albumApi);
        }

        return false;
    }

    private async Task DownloadPictureIfNeededAsync(AlbumDto album, MusicDataAlbumDto albumApi, CancellationToken cancellationToken)
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
            Id = album.Id,
            Label = albumApi.Label,
            Sales = albumApi.Sales,
            MusicBrainzID = albumApi.MusicBrainzID,
            ReleaseDate = albumApi.ReleaseDate,
            Wikipedia = albumApi.Wikipedia,
            AllMusicID = album.AllMusicID,
            IsLive = album.IsLive,
            IsBestOf = album.IsBestOf,
            IsCompilation = album.IsCompilation,
            AmazonID = album.AmazonID,
            AudioDbArtistID = album.AudioDbArtistID,
            AudioDbID = album.AudioDbID,
            DiscogsID = album.DiscogsID,
            GeniusID = album.GeniusID,
            LyricWikiID = album.LyricWikiID,
            MusicMozID = album.MusicMozID,
            ReleaseGroupMusicBrainzID = album.ReleaseGroupMusicBrainzID,
            WikidataID = album.WikidataID,
            WikipediaID = album.WikipediaID,
        };

        if (string.IsNullOrWhiteSpace(album.Biography) && !string.IsNullOrWhiteSpace(albumApi.Biography))
            command.Biography = albumApi.Biography;

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

        return false;
    }
}