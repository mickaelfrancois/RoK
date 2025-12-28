namespace Rok.Domain.Interfaces.Entities;

public interface IAlbumEntity : IEntity
{
    string AlbumPath { get; set; }
    string? AllMusicID { get; set; }
    string? AmazonID { get; set; }
    long? ArtistId { get; set; }
    string ArtistName { get; set; }
    string? AudioDbArtistID { get; set; }
    string? AudioDbID { get; set; }
    string CountryCode { get; set; }
    string CountryName { get; set; }
    string? DiscogsID { get; set; }
    long Duration { get; set; }
    string? GeniusID { get; set; }
    long? GenreId { get; set; }
    string GenreName { get; set; }
    DateTime? GetMetaDataLastAttempt { get; set; }
    bool IsArtistFavorite { get; set; }
    bool IsBestOf { get; set; }
    bool IsCompilation { get; set; }
    bool IsFavorite { get; set; }
    bool IsGenreFavorite { get; set; }
    bool IsLive { get; set; }
    string? Label { get; set; }
    DateTime? LastListen { get; set; }
    int ListenCount { get; set; }
    string? LyricWikiID { get; set; }
    string? MusicBrainzID { get; set; }
    string? MusicMozID { get; set; }
    string Name { get; set; }
    DateTime? ReleaseDate { get; set; }
    string? ReleaseFormat { get; set; }
    string? ReleaseGroupMusicBrainzID { get; set; }
    string? Sales { get; set; }
    int TrackCount { get; set; }
    string? WikidataID { get; set; }
    string? Wikipedia { get; set; }
    string? WikipediaID { get; set; }
    string? Biography { get; set; }
    int? Year { get; set; }

    string ToString();
}