using Microsoft.Extensions.Logging;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
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
        if (CompareArtistFromApi(artist, artistApi))
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
        logger.LogTrace("Patch artist '{Name}' with API data.", artist.Name);

        UpdateArtistRequest command = new()
        {
            Id = artist.Id,
            MusicBrainzID = artistApi.MusicBrainzID ?? artist.MusicBrainzID,
            AllMusicUrl = artistApi.AllMusic ?? artist.AllMusicUrl,
            WikipediaUrl = artistApi.Wikipedia ?? artist.WikipediaUrl,
            OfficialSiteUrl = artistApi.Website ?? artist.OfficialSiteUrl,
            FacebookUrl = artistApi.Facebook ?? artist.FacebookUrl,
            TwitterUrl = artistApi.Twitter ?? artist.TwitterUrl,
            FlickrUrl = artistApi.Flickr ?? artist.FlickrUrl,
            InstagramUrl = artistApi.Instagram ?? artist.InstagramUrl,
            TiktokUrl = artistApi.TikTok ?? artist.TiktokUrl,
            ThreadsUrl = artistApi.Threads ?? artist.ThreadsUrl,
            SongkickUrl = artistApi.SongKick ?? artist.SongkickUrl,
            SoundcloundUrl = artistApi.SoundCloud ?? artist.SoundcloundUrl,
            ImdbUrl = artistApi.Imdb ?? artist.ImdbUrl,
            LastFmUrl = artistApi.LastFM ?? artist.LastFmUrl,
            DiscogsUrl = artistApi.Discogs ?? artist.DiscogsUrl,
            BandsintownUrl = artistApi.Bandsintown ?? artist.BandsintownUrl,
            YoutubeUrl = artistApi.Youtube ?? artist.YoutubeUrl,
            AudioDbID = artistApi.AudioDbID ?? artist.AudioDbID,
            BornYear = artistApi.BeginYear ?? artist.BornYear,
            DiedYear = artistApi.EndYear ?? artist.DiedYear,
        };

        command.Biography = string.IsNullOrEmpty(artist.Biography) ? artistApi.Biography : artist.Biography;

        await mediator.Send(command);

        return true;
    }

    private static bool CompareArtistFromApi(ArtistDto artist, MusicDataArtistDto artistApi)
    {
        if (artist.FlickrUrl.AreDifferent(artistApi.Flickr)) return true;
        if (artist.InstagramUrl.AreDifferent(artistApi.Instagram)) return true;
        if (artist.TiktokUrl.AreDifferent(artistApi.TikTok)) return true;
        if (artist.ThreadsUrl.AreDifferent(artistApi.Threads)) return true;
        if (artist.SongkickUrl.AreDifferent(artistApi.SongKick)) return true;
        if (artist.SoundcloundUrl.AreDifferent(artistApi.SoundCloud)) return true;
        if (artist.ImdbUrl.AreDifferent(artistApi.Imdb)) return true;
        if (artist.LastFmUrl.AreDifferent(artistApi.LastFM)) return true;
        if (artist.DiscogsUrl.AreDifferent(artistApi.Discogs)) return true;
        if (artist.BandsintownUrl.AreDifferent(artistApi.Bandsintown)) return true;
        if (artist.YoutubeUrl.AreDifferent(artistApi.Youtube)) return true;
        if (artist.AudioDbID.AreDifferent(artistApi.AudioDbID)) return true;
        if (artist.AllMusicUrl.AreDifferent(artistApi.AllMusic)) return true;
        if (artist.TwitterUrl.AreDifferent(artistApi.Twitter)) return true;
        if (artist.OfficialSiteUrl.AreDifferent(artistApi.Website)) return true;
        if (artist.FacebookUrl.AreDifferent(artistApi.Facebook)) return true;
        if (artist.WikipediaUrl.AreDifferent(artistApi.Wikipedia)) return true;
        if (artist.MusicBrainzID.AreDifferent(artistApi.MusicBrainzID)) return true;
        if (artist.BornYear.AreDifferent(artistApi.BeginYear)) return true;
        if (artist.DiedYear.AreDifferent(artistApi.EndYear)) return true;
        if (artist.Disbanded != artistApi.Disbanded) return true;

        return string.IsNullOrWhiteSpace(artist.Biography) && !string.IsNullOrWhiteSpace(artistApi.Biography);
    }
}