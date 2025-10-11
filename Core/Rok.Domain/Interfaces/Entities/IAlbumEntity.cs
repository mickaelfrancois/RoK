namespace Rok.Domain.Interfaces.Entities;

public interface IAlbumEntity : IEntity
{
    string AlbumPath { get; set; }
    long? ArtistId { get; set; }
    string ArtistName { get; set; }
    string CountryCode { get; set; }
    string CountryName { get; set; }
    long Duration { get; set; }
    long? GenreId { get; set; }
    string GenreName { get; set; }
    bool IsArtistFavorite { get; set; }
    bool IsBestOf { get; set; }
    bool IsCompilation { get; set; }
    bool IsFavorite { get; set; }
    bool IsGenreFavorite { get; set; }
    bool IsLive { get; set; }
    string? Label { get; set; }
    DateTime? LastListen { get; set; }
    int ListenCount { get; set; }
    string? Mood { get; set; }
    string? MusicBrainzID { get; set; }
    string Name { get; set; }
    string? NovaUid { get; set; }
    DateTime? ReleaseDate { get; set; }
    string? ReleaseFormat { get; set; }
    string? Sales { get; set; }
    string? Speed { get; set; }
    string? Theme { get; set; }
    int TrackCount { get; set; }
    string? Wikipedia { get; set; }
    int? Year { get; set; }
    DateTime? GetMetaDataLastAttempt { get; set; }

    string ToString();
}