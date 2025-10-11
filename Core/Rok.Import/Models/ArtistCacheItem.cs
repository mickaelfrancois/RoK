namespace Rok.Import.Models;

public record ArtistCacheItem
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long? GenreId { get; set; }
}
