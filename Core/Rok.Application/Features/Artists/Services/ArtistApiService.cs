using Microsoft.Extensions.Logging;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Interfaces;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Artists.Services;

public class ArtistApiService(
    IMediator mediator,
    IMusicDataApiService musicDataApiService,
    ILogger<ArtistApiService> logger)
{
    public async Task<bool> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture)
    {
        if (string.IsNullOrEmpty(artist.Name))
            return false;

        if (!musicDataApiService.IsApiRetryAllowed(artist.GetMetaDataLastAttempt))
            return false;

        await mediator.SendMessageAsync(new UpdateArtistGetMetaDataLastAttemptCommand(artist.Id));
        artist.GetMetaDataLastAttempt = DateTime.UtcNow;

        MusicDataArtistDto? artistApi = await musicDataApiService.GetArtistAsync(artist.Name, artist.MusicBrainzID);
        if (artistApi == null)
            return false;

        if (!string.IsNullOrEmpty(artistApi.MusicBrainzID))
        {
            await DownloadPictureIfNeededAsync(artist, artistApi, pictureService, CancellationToken.None);
            await DownloadBackdropsIfNeededAsync(artist, artistApi, backdropPicture, CancellationToken.None);

            if (CompareArtistFromApi(artist, artistApi))
                return await UpdateArtistDataIfNeededAsync(artist, artistApi);
        }

        return false;
    }

    private async Task DownloadPictureIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi, IArtistPictureService pictureService, CancellationToken cancellationToken)
    {
        if (pictureService.PictureExists(artist.Name))
            return;

        if (string.IsNullOrWhiteSpace(artistApi.PictureUrl))
            return;

        string picturePath = pictureService.GetPictureFilePath(artist.Name);

        await musicDataApiService.DownloadArtistPictureAsync(artistApi, picturePath, cancellationToken);
    }

    private async Task DownloadBackdropsIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi, IBackdropPicture backdropPicture, CancellationToken cancellationToken)
    {
        if (backdropPicture.HasBackdrops(artist.Name))
            return;

        string backdropFolder = backdropPicture.GetArtistPictureFolder(artist.Name);

        await musicDataApiService.DownloadArtistBackdropsAsync(artistApi, backdropFolder, cancellationToken);
    }

    private async Task<bool> UpdateArtistDataIfNeededAsync(ArtistDto artist, MusicDataArtistDto artistApi)
    {
        logger.LogTrace("Patch artist '{Name}' with API data.", artist.Name);

        UpdateArtistCommand command = new()
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

        if (string.IsNullOrEmpty(artist.Biography))
            command.Biography = artistApi.Biography;

        await mediator.SendMessageAsync(command);

        return true;
    }

    private static bool CompareArtistFromApi(ArtistDto artist, MusicDataArtistDto artistApi)
    {
        if (artist.FlickrUrl.AreDifferents(artistApi.Flickr)) return true;
        if (artist.InstagramUrl.AreDifferents(artistApi.Instagram)) return true;
        if (artist.TiktokUrl.AreDifferents(artistApi.TikTok)) return true;
        if (artist.ThreadsUrl.AreDifferents(artistApi.Threads)) return true;
        if (artist.SongkickUrl.AreDifferents(artistApi.SongKick)) return true;
        if (artist.SoundcloundUrl.AreDifferents(artistApi.SoundCloud)) return true;
        if (artist.ImdbUrl.AreDifferents(artistApi.Imdb)) return true;
        if (artist.LastFmUrl.AreDifferents(artistApi.LastFM)) return true;
        if (artist.DiscogsUrl.AreDifferents(artistApi.Discogs)) return true;
        if (artist.BandsintownUrl.AreDifferents(artistApi.Bandsintown)) return true;
        if (artist.YoutubeUrl.AreDifferents(artistApi.Youtube)) return true;
        if (artist.AudioDbID.AreDifferents(artistApi.AudioDbID)) return true;
        if (artist.AllMusicUrl.AreDifferents(artistApi.AllMusic)) return true;
        if (artist.TwitterUrl.AreDifferents(artistApi.Twitter)) return true;
        if (artist.OfficialSiteUrl.AreDifferents(artistApi.Website)) return true;
        if (artist.FacebookUrl.AreDifferents(artistApi.Facebook)) return true;
        if (artist.ThreadsUrl.AreDifferents(artistApi.Threads)) return true;
        if (artist.BornYear.AreDifferents(artistApi.BeginYear)) return true;
        if (artist.DiedYear.AreDifferents(artistApi.EndYear)) return true;
        if (artist.Disbanded != artistApi.Disbanded) return true;

        return string.IsNullOrWhiteSpace(artist.Biography) && !string.IsNullOrWhiteSpace(artistApi.Biography);
    }
}
