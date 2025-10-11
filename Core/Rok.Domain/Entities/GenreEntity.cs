namespace Rok.Domain.Entities;

[Table("Genres")]
public class GenreEntity : BaseEntity
{
    public override string ToString() => Name;

    public string Name { get; set; } = string.Empty;

    public int ArtistCount { get; set; } = 0;

    public int TrackCount { get; set; } = 0;

    public int AlbumCount { get; set; } = 0;

    public int LiveCount { get; set; } = 0;

    public int CompilationCount { get; set; }

    public int BestofCount { get; set; } = 0;

    public bool IsFavorite { get; set; }

    public int ListenCount { get; set; }

    public DateTime? LastListen { get; set; }
}