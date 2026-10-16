using Microsoft.Extensions.Logging;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Shared;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Artists.Services;

public class ArtistApiService(
    IMediator mediator,
    IMusicDataApiService musicDataApiService,
    ILogger<ArtistApiService> logger) : IArtistApiService
{
    public Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture, CancellationToken cancellationToken = default)
    {
        return GetAndUpdateArtistDataAsync(artist, pictureService, backdropPicture, ArtistApiMergeMode.Default, cancellationToken);
    }

    public async Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture, ArtistApiMergeMode mode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(artist.Name))
            return ArtistApiUpdateResult.None;

        if (!musicDataApiService.IsApiRetryAllowed(artist.GetMetaDataLastAttempt))
            return ArtistApiUpdateResult.None;

        await mediator.Send(new UpdateArtistGetMetaDataLastAttemptRequest(artist.Id));
        artist.GetMetaDataLastAttempt = DateTime.UtcNow;

        MusicDataArtistDto? artistApi = await musicDataApiService.GetArtistAsync(artist.Name, artist.MusicBrainzID);
        if (artistApi == null)
            return ArtistApiUpdateResult.None;

        if (string.IsNullOrEmpty(artistApi.MusicBrainzID))
            return ArtistApiUpdateResult.None;

        bool pictureDownloaded = await DownloadPictureIfNeededAsync(artist, artistApi, pictureService, cancellationToken);
        bool backdropsDownloaded = await DownloadBackdropsIfNeededAsync(artist, artistApi, backdropPicture, cancellationToken);

        bool dataUpdated = false;
        if (!artist.IsLock)
            dataUpdated = await UpdateArtistDataIfNeededAsync(artist, artistApi, mode);

        return new ArtistApiUpdateResult(dataUpdated, pictureDownloaded, backdropsDownloaded);
    }

    private async Task<bool> DownloadPictureIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi, IArtistPictureService pictureService, CancellationToken cancellationToken)
    {
        if (pictureService.PictureExists(artist.Name))
            return false;

        if (string.IsNullOrWhiteSpace(artistApi.PictureUrl))
            return false;

        string picturePath = pictureService.GetPictureFilePath(artist.Name);

        await musicDataApiService.DownloadArtistPictureAsync(artistApi, picturePath, cancellationToken);

        return pictureService.PictureExists(artist.Name);
    }

    private async Task<bool> DownloadBackdropsIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi, IBackdropPicture backdropPicture, CancellationToken cancellationToken)
    {
        if (backdropPicture.HasBackdrops(artist.Name))
            return false;

        string backdropFolder = backdropPicture.GetArtistPictureFolder(artist.Name);

        await musicDataApiService.DownloadArtistBackdropsAsync(artistApi, backdropFolder, cancellationToken);

        return backdropPicture.HasBackdrops(artist.Name);
    }

    private async Task<bool> UpdateArtistDataIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi, ArtistApiMergeMode mode)
    {
        UpdateArtistRequest command = new() { Id = artist.Id };

        if (!BuildPatch(command, artist, artistApi, mode))
            return false;

        logger.LogTrace("Patch artist '{Name}' with API data.", artist.Name);

        await mediator.Send(command);

        return true;
    }

    private static bool BuildPatch(UpdateArtistRequest command, ArtistDto artist, MusicDataArtistDto artistApi, ArtistApiMergeMode mode)
    {
        bool hasChanges = false;
        bool fillOnly = mode == ArtistApiMergeMode.FillEmptyOnly;

        hasChanges |= FillIfEmpty(command.MusicBrainzID, artist.MusicBrainzID, artistApi.MusicBrainzID);
        hasChanges |= FillIfEmpty(command.BornYear, artist.BornYear, artistApi.BeginYear);
        hasChanges |= FillIfEmpty(command.DiedYear, artist.DiedYear, artistApi.EndYear);
        hasChanges |= FillIfEmpty(command.Biography, artist.Biography, artistApi.Biography);

        hasChanges |= UpdateUrl(fillOnly, command.AllMusicUrl, artist.AllMusicUrl, artistApi.AllMusic);
        hasChanges |= UpdateUrl(fillOnly, command.WikipediaUrl, artist.WikipediaUrl, artistApi.Wikipedia);
        hasChanges |= UpdateUrl(fillOnly, command.OfficialSiteUrl, artist.OfficialSiteUrl, artistApi.Website);
        hasChanges |= UpdateUrl(fillOnly, command.FacebookUrl, artist.FacebookUrl, artistApi.Facebook);
        hasChanges |= UpdateUrl(fillOnly, command.TwitterUrl, artist.TwitterUrl, artistApi.Twitter);
        hasChanges |= UpdateUrl(fillOnly, command.FlickrUrl, artist.FlickrUrl, artistApi.Flickr);
        hasChanges |= UpdateUrl(fillOnly, command.InstagramUrl, artist.InstagramUrl, artistApi.Instagram);
        hasChanges |= UpdateUrl(fillOnly, command.TiktokUrl, artist.TiktokUrl, artistApi.TikTok);
        hasChanges |= UpdateUrl(fillOnly, command.ThreadsUrl, artist.ThreadsUrl, artistApi.Threads);
        hasChanges |= UpdateUrl(fillOnly, command.SongkickUrl, artist.SongkickUrl, artistApi.SongKick);
        hasChanges |= UpdateUrl(fillOnly, command.SoundcloundUrl, artist.SoundcloundUrl, artistApi.SoundCloud);
        hasChanges |= UpdateUrl(fillOnly, command.ImdbUrl, artist.ImdbUrl, artistApi.Imdb);
        hasChanges |= UpdateUrl(fillOnly, command.LastFmUrl, artist.LastFmUrl, artistApi.LastFM);
        hasChanges |= UpdateUrl(fillOnly, command.DiscogsUrl, artist.DiscogsUrl, artistApi.Discogs);
        hasChanges |= UpdateUrl(fillOnly, command.BandsintownUrl, artist.BandsintownUrl, artistApi.Bandsintown);
        hasChanges |= UpdateUrl(fillOnly, command.YoutubeUrl, artist.YoutubeUrl, artistApi.Youtube);
        hasChanges |= UpdateUrl(fillOnly, command.AudioDbID, artist.AudioDbID, artistApi.AudioDbID);

        return hasChanges;
    }

    private static bool FillIfEmpty(PatchField<string?> field, string? current, string? api)
    {
        if (string.IsNullOrWhiteSpace(api) || !string.IsNullOrWhiteSpace(current))
            return false;

        field.Set(api);

        return true;
    }

    private static bool FillIfEmpty(PatchField<int?> field, int? current, int? api)
    {
        if (!api.HasValue || current.HasValue)
            return false;

        field.Set(api);

        return true;
    }

    private static bool UpdateUrl(bool fillOnly, PatchField<string?> field, string? current, string? api)
    {
        return fillOnly ? FillIfEmpty(field, current, api) : UpdateIfDifferent(field, current, api);
    }

    private static bool UpdateIfDifferent(PatchField<string?> field, string? current, string? api)
    {
        if (string.IsNullOrWhiteSpace(api) || !current.AreDifferent(api))
            return false;

        field.Set(api);

        return true;
    }
}