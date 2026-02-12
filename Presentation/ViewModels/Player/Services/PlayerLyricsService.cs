using Rok.Application.Dto.Lyrics;
using Rok.Infrastructure.Lyrics;

namespace Rok.ViewModels.Player.Services;

public class PlayerLyricsService(ILyricsService lyricsService)
{
    public bool CheckLyricsExists(string musicFile)
    {
        return lyricsService.CheckLyricsFileExists(musicFile) != ELyricsType.None;
    }

    public async Task<LyricsModel?> LoadLyricsAsync(string musicFile)
    {
        return await lyricsService.LoadLyricsAsync(musicFile);
    }

    public SyncLyricsModel ParseSynchronizedLyrics(string synchronizedLyrics)
    {
        LyricsParser parser = new();
        return parser.Parse(synchronizedLyrics);
    }
}