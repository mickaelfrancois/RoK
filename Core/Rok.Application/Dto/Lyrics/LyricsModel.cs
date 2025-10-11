namespace Rok.Application.Dto.Lyrics;

public class LyricsModel
{
    public string File { get; set; } = string.Empty;

    public string PlainLyrics { get; set; } = string.Empty;

    public string? SynchronizedLyrics { get; set; }

    public ELyricsType LyricsType { get; set; }
}