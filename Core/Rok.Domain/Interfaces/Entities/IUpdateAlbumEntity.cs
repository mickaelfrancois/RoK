namespace Rok.Domain.Interfaces.Entities;

public interface IUpdateAlbumEntity
{
    long Id { get; set; }
    string? Label { get; set; }
    string? Mood { get; set; }
    string? MusicBrainzID { get; set; }
    DateTime? ReleaseDate { get; set; }
    string? ReleaseFormat { get; set; }
    string? Sales { get; set; }
    string? Speed { get; set; }
    string? Theme { get; set; }
    string? Wikipedia { get; set; }
}