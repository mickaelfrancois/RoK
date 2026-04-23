namespace Rok.Import.Models;

public record AlbumCacheItem
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long? ArtistId { get; set; }

    public bool IsCompilation { get; set; }

    public string AlbumPath { get; set; } = string.Empty;
}
