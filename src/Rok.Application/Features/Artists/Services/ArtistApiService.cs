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
    public async Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture, CancellationToken cancellationToken = default)
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
            dataUpdated = await UpdateArtistDataIfNeededAsync(artist, artistApi);

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

    private async Task<bool> UpdateArtistDataIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi)
    {
        UpdateArtistRequest command = new() { Id = artist.Id };

        if (!BuildPatch(command, artist, artistApi))
            return false;

        logger.LogTrace("Patch artist '{Name}' with API data.", artist.Name);

        await mediator.Send(command);

        return true;
    }

    private static bool BuildPatch(UpdateArtistRequest command, ArtistDto artist, MusicDataArtistDto artistApi)
    {
        bool hasChanges = false;

        hasChanges |= FillIfEmpty(command.MusicBrainzID, artist.MusicBrainzID, artistApi.MusicBrainzID);
        hasChanges |= FillIfEmpty(command.BornYear, artist.BornYear, artistApi.BeginYear);
        hasChanges |= FillIfEmpty(command.DiedYear, artist.DiedYear, artistApi.EndYear);
        hasChanges |= FillIfEmpty(command.Biography, artist.Biography, artistApi.Biography);

        hasChanges |= UpdateIfDifferent(command.AllMusicUrl, artist.AllMusicUrl, artistApi.AllMusic);
        hasChanges |= UpdateIfDifferent(command.WikipediaUrl, artist.WikipediaUrl, artistApi.Wikipedia);
        hasChanges |= UpdateIfDifferent(command.OfficialSiteUrl, artist.OfficialSiteUrl, artistApi.Website);
        hasChanges |= UpdateIfDifferent(command.FacebookUrl, artist.FacebookUrl, artistApi.Facebook);
        hasChanges |= UpdateIfDifferent(command.TwitterUrl, artist.TwitterUrl, artistApi.Twitter);
        hasChanges |= UpdateIfDifferent(command.FlickrUrl, artist.FlickrUrl, artistApi.Flickr);
        hasChanges |= UpdateIfDifferent(command.InstagramUrl, artist.InstagramUrl, artistApi.Instagram);
        hasChanges |= UpdateIfDifferent(command.TiktokUrl, artist.TiktokUrl, artistApi.TikTok);
        hasChanges |= UpdateIfDifferent(command.ThreadsUrl, artist.ThreadsUrl, artistApi.Threads);
        hasChanges |= UpdateIfDifferent(command.SongkickUrl, artist.SongkickUrl, artistApi.SongKick);
        hasChanges |= UpdateIfDifferent(command.SoundcloundUrl, artist.SoundcloundUrl, artistApi.SoundCloud);
        hasChanges |= UpdateIfDifferent(command.ImdbUrl, artist.ImdbUrl, artistApi.Imdb);
        hasChanges |= UpdateIfDifferent(command.LastFmUrl, artist.LastFmUrl, artistApi.LastFM);
        hasChanges |= UpdateIfDifferent(command.DiscogsUrl, artist.DiscogsUrl, artistApi.Discogs);
        hasChanges |= UpdateIfDifferent(command.BandsintownUrl, artist.BandsintownUrl, artistApi.Bandsintown);
        hasChanges |= UpdateIfDifferent(command.YoutubeUrl, artist.YoutubeUrl, artistApi.Youtube);
        hasChanges |= UpdateIfDifferent(command.AudioDbID, artist.AudioDbID, artistApi.AudioDbID);

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

    private static bool UpdateIfDifferent(PatchField<string?> field, string? current, string? api)
    {
        if (string.IsNullOrWhiteSpace(api) || !current.AreDifferent(api))
            return false;

        field.Set(api);

        return true;
    }
}