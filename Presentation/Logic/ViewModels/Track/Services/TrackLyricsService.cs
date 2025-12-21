using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Tracks.Command;
using Rok.Infrastructure.NovaApi;

namespace Rok.Logic.ViewModels.Track.Services;

public class TrackLyricsService(
    IMediator mediator,
    ILyricsService lyricsService,
    INovaApiService novaApiService,
    ITranslateService translateService,
    ILogger<TrackLyricsService> logger)
{
    public bool CheckLyricsExists(string musicFile)
    {
        return lyricsService.CheckLyricsFileExists(musicFile) != ELyricsType.None;
    }

    public async Task<LyricsModel?> LoadLyricsAsync(string musicFile)
    {
        return await lyricsService.LoadLyricsAsync(musicFile);
    }

    public async Task<bool> GetAndSaveLyricsFromApiAsync(TrackDto track)
    {
        if (string.IsNullOrEmpty(track.MusicFile) || string.IsNullOrEmpty(track.ArtistName) || string.IsNullOrEmpty(track.Title))
            return false;

        if (!NovaApiService.IsApiRetryAllowed(track.GetLyricsLastAttempt))
            return false;

        logger.LogTrace("Fetching lyrics for {Artist} - {Title} from API", track.ArtistName, track.Title);

        await mediator.SendMessageAsync(new UpdateTrackGetLyricsLastAttemptCommand(track.Id));

        ApiLyricsModel? lyrics = await novaApiService.GetLyricsAsync(track.ArtistName, track.Title);
        if (lyrics == null)
            return false;

        string fileName = lyrics.IsSynchronized
            ? lyricsService.GetSynchronizedLyricsFileName(track.MusicFile)
            : lyricsService.GetPlainLyricsFileName(track.MusicFile);

        await lyricsService.SaveLyricsAsync(new LyricsModel
        {
            File = fileName,
            PlainLyrics = lyrics.Lyrics,
            LyricsType = lyrics.IsSynchronized ? ELyricsType.Synchronized : ELyricsType.Plain
        });

        logger.LogTrace("Lyrics saved to {File}", fileName);

        return true;
    }
}