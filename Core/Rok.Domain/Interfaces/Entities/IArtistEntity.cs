namespace Rok.Domain.Interfaces.Entities;

public interface IArtistEntity : IEntity
{
    int AlbumCount { get; set; }
    int BestofCount { get; set; }
    string? Biography { get; set; }
    int? BornYear { get; set; }
    int CompilationCount { get; set; }
    string CountryCode { get; set; }
    long? CountryId { get; set; }
    string CountryName { get; set; }
    int? DiedYear { get; set; }
    bool Disbanded { get; set; }
    string? FacebookUrl { get; set; }
    int? FormedYear { get; set; }
    string? Gender { get; set; }
    long? GenreId { get; set; }
    string GenreName { get; set; }
    bool IsFavorite { get; set; }
    bool IsGenreFavorite { get; set; }
    DateTime? LastListen { get; set; }
    int ListenCount { get; set; }
    int LiveCount { get; set; }
    string? Members { get; set; }
    string? Mood { get; set; }
    string? MusicBrainzID { get; set; }
    string Name { get; set; }
    string? NovaUid { get; set; }
    string? OfficialSiteUrl { get; set; }
    string? SimilarArtists { get; set; }
    string? Style { get; set; }
    long TotalDurationSeconds { get; set; }
    int TrackCount { get; set; }
    string? TwitterUrl { get; set; }
    string? WikipediaUrl { get; set; }
    int? YearMaxi { get; set; }
    int? YearMini { get; set; }
    DateTime? GetMetaDataLastAttempt { get; set; }

    string ToString();
}