using Microsoft.Extensions.Logging;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Shared;
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
        if (!album.IsLock)
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
        UpdateAlbumRequest command = new() { Id = album.Id };

        if (!BuildPatch(command, album, albumApi))
            return false;

        logger.LogTrace("Patch album '{Name}' from API response.", album.Name);

        await mediator.Send(command);

        return true;
    }

    private static bool BuildPatch(UpdateAlbumRequest command, AlbumDto album, MusicDataAlbumDto albumApi)
    {
        bool hasChanges = false;

        hasChanges |= FillIfEmpty(command.MusicBrainzID, album.MusicBrainzID, albumApi.MusicBrainzID);
        hasChanges |= FillIfEmpty(command.ReleaseGroupMusicBrainzID, album.ReleaseGroupMusicBrainzID, albumApi.ReleaseGroupMusicBrainzID);
        hasChanges |= FillIfEmpty(command.Biography, album.Biography, albumApi.Biography);
        hasChanges |= FillIfEmpty(command.ReleaseFormat, album.ReleaseFormat, albumApi.ReleaseFormat);

        hasChanges |= UpdateIfDifferent(command.Label, album.Label, albumApi.Label);
        hasChanges |= UpdateIfDifferent(command.Sales, album.Sales, albumApi.Sales);
        hasChanges |= UpdateIfDifferent(command.Wikipedia, album.Wikipedia, albumApi.Wikipedia);
        hasChanges |= UpdateIfDifferent(command.AllMusicID, album.AllMusicID, albumApi.AllMusicID);
        hasChanges |= UpdateIfDifferent(command.AmazonID, album.AmazonID, albumApi.AmazonID);
        hasChanges |= UpdateIfDifferent(command.AudioDbArtistID, album.AudioDbArtistID, albumApi.AudioDbArtistID);
        hasChanges |= UpdateIfDifferent(command.AudioDbID, album.AudioDbID, albumApi.AudioDbID);
        hasChanges |= UpdateIfDifferent(command.DiscogsID, album.DiscogsID, albumApi.DiscogsID);
        hasChanges |= UpdateIfDifferent(command.GeniusID, album.GeniusID, albumApi.GeniusID);
        hasChanges |= UpdateIfDifferent(command.LyricWikiID, album.LyricWikiID, albumApi.LyricWikiID);
        hasChanges |= UpdateIfDifferent(command.MusicMozID, album.MusicMozID, albumApi.MusicMozID);
        hasChanges |= UpdateIfDifferent(command.WikidataID, album.WikidataID, albumApi.WikidataID);
        hasChanges |= UpdateIfDifferent(command.WikipediaID, album.WikipediaID, albumApi.WikipediaID);
        hasChanges |= UpdateIfDifferent(command.ReleaseDate, album.ReleaseDate, albumApi.ReleaseDate);

        return hasChanges;
    }

    private static bool FillIfEmpty(PatchField<string?> field, string? current, string? api)
    {
        if (string.IsNullOrWhiteSpace(api) || !string.IsNullOrWhiteSpace(current))
            return false;

        field.Set(api);

        return true;
    }

    private static bool UpdateIfDifferent(PatchField<string?> field, string? current, string? api)
    {
        if (string.IsNullOrWhiteSpace(api) || !current.AreDifferent(api))
            return false;

        field.Set(api);

        return true;
    }

    private static bool UpdateIfDifferent(PatchField<DateTime?> field, DateTime? current, DateTime? api)
    {
        if (!api.HasValue || current == api)
            return false;

        field.Set(api);

        return true;
    }
}
