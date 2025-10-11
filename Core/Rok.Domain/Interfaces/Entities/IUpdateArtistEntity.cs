namespace Rok.Domain.Interfaces.Entities;

public interface IUpdateArtistEntity
{
    string? Biography { get; set; }
    int? BornYear { get; set; }
    int? DiedYear { get; set; }
    bool Disbanded { get; set; }
    string? FacebookUrl { get; set; }
    int? FormedYear { get; set; }
    string? Gender { get; set; }
    long Id { get; set; }
    string? Mood { get; set; }
    string? MusicBrainzID { get; set; }
    string? NovaUid { get; set; }
    string? OfficialSiteUrl { get; set; }
    string? Style { get; set; }
    string? TwitterUrl { get; set; }
    string? WikipediaUrl { get; set; }
}