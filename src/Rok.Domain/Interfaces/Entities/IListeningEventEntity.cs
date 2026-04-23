namespace Rok.Domain.Interfaces.Entities;

public interface IListeningEventEntity
{
    long Id { get; set; }

    long TrackId { get; set; }

    long? ArtistId { get; set; }

    long? AlbumId { get; set; }

    long? GenreId { get; set; }

    DateTime PlayedAt { get; set; }

    bool WasSkipped { get; }
}