namespace Rok.Domain.Entities;

[Table("ListeningEvents")]
public class ListeningEventEntity : IListeningEventEntity
{
    [Dapper.Contrib.Extensions.Key]
    public long Id { get; set; }

    public long TrackId { get; set; }

    public long? ArtistId { get; set; }

    public long? AlbumId { get; set; }

    public long? GenreId { get; set; }

    public DateTime PlayedAt { get; set; }

    public bool WasSkipped { get; set; }

    public long DurationPlayed { get; set; }

    public long DurationTotal { get; set; }
}