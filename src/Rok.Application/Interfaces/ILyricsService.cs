using Rok.Application.Dto.Lyrics;

namespace Rok.Application.Interfaces;

public interface ILyricsService
{
    string GetSynchronizedLyricsFileName(string musicFile);

    string GetPlainLyricsFileName(string musicFile);

    ELyricsType CheckLyricsFileExists(string musicFile);

    Task<LyricsModel?> LoadLyricsAsync(string musicFile);

    Task SaveLyricsAsync(LyricsModel lyrics);
}