using Rok.Application.Dto.Lyrics;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Tracks.Command;
using Rok.Infrastructure.MusicData;

namespace Rok.Logic.ViewModels.Track.Services;

public class TrackLyricsService(IMediator mediator, ILyricsService lyricsService, IMusicDataApiService musicDataService, ILogger<TrackLyricsService> logger)
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
        if (string.IsNullOrEmpty(track.MusicFile) || string.IsNullOrEmpty(track.ArtistName) || string.IsNullOrEmpty(track.AlbumName) || string.IsNullOrEmpty(track.Title) || track.Duration <= 0)
            return false;

        if (!MusicDataApiService.IsApiRetryAllowed(track.GetLyricsLastAttempt))
            return false;

        logger.LogTrace("Fetching lyrics for {Artist} - {Title} from API", track.ArtistName, track.Title);

        await mediator.SendMessageAsync(new UpdateTrackGetLyricsLastAttemptCommand(track.Id));

        try
        {

            MusicDataLyricsDto? lyrics = await musicDataService.GetLyricsAsync(track.ArtistName, track.AlbumName, track.Title, track.Duration);
            if (lyrics is null)
                return false;

            if (lyrics.SyncLyrics is null && lyrics.PlainLyrics is null)
                return false;

            string fileName = lyrics.IsSynchronized
                ? lyricsService.GetSynchronizedLyricsFileName(track.MusicFile)
                : lyricsService.GetPlainLyricsFileName(track.MusicFile);

            await lyricsService.SaveLyricsAsync(new LyricsModel
            {
                File = fileName,
                PlainLyrics = lyrics.Lyrics!,
                LyricsType = lyrics.IsSynchronized ? ELyricsType.Synchronized : ELyricsType.Plain
            });

            logger.LogTrace("Lyrics saved to {File}", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching lyrics for {Artist} - {Title} from API", track.ArtistName, track.Title);
            return false;
        }

        return true;
    }
}