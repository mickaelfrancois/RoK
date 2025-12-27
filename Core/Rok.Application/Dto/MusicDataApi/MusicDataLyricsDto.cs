namespace Rok.Infrastructure.NovaApi;

public class MusicDataLyricsDto
{
    public string Origin { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public string AlbumName { get; set; } = string.Empty;

    public string? PlainLyrics { get; set; }

    public string? SyncLyrics { get; set; }

    public int Duration { get; set; }
}
