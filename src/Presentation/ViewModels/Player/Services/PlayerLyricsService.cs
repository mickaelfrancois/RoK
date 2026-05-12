using Rok.Application.Dto.Lyrics;

namespace Rok.ViewModels.Player.Services;

public class PlayerLyricsService(ILyricsService lyricsService, ILyricsParser lyricsParser)
{
    public bool CheckLyricsExists(string musicFile)
    {
        return lyricsService.CheckLyricsFileExists(musicFile) != ELyricsType.None;
    }

    public Task<LyricsModel?> LoadLyricsAsync(string musicFile)
    {
        return lyricsService.LoadLyricsAsync(musicFile);
    }

    public SyncLyricsModel ParseSynchronizedLyrics(string synchronizedLyrics)
    {
        return lyricsParser.Parse(synchronizedLyrics);
    }
}