namespace Rok.Application.Dto.Lyrics;

public class LyricsModel
{
    public string File { get; set; } = string.Empty;

    public string PlainLyrics { get; set; } = string.Empty;

    public string? SynchronizedLyrics { get; set; }

    public ELyricsType LyricsType { get; set; }

    // Lyrics to show to the user: the parsed plain text when available, otherwise
    // the raw file content. Some .lrc files only carry metadata (e.g.
    // "[au: instrumental]") which strips down to nothing once timestamps are
    // removed; falling back to the raw content guarantees the dialog still has
    // something to display instead of silently doing nothing.
    public string DisplayText =>
        !string.IsNullOrWhiteSpace(PlainLyrics) ? PlainLyrics : SynchronizedLyrics ?? string.Empty;
}