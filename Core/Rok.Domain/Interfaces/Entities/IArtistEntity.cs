namespace Rok.Domain.Interfaces.Entities;

public interface IArtistEntity : IEntity
{
    int AlbumCount { get; set; }
    string? AllMusicUrl { get; set; }
    string? AudioDbID { get; set; }
    string? BandsintownUrl { get; set; }
    int BestofCount { get; set; }
    string? Biography { get; set; }
    int? BornYear { get; set; }
    int CompilationCount { get; set; }
    string CountryCode { get; set; }
    long? CountryId { get; set; }
    string CountryName { get; set; }
    int? DiedYear { get; set; }
    bool Disbanded { get; set; }
    string? DiscogsUrl { get; set; }
    string? FacebookUrl { get; set; }
    string? FlickrUrl { get; set; }
    int? FormedYear { get; set; }
    long? GenreId { get; set; }
    string GenreName { get; set; }
    DateTime? GetMetaDataLastAttempt { get; set; }
    string? ImdbUrl { get; set; }
    string? InstagramUrl { get; set; }
    bool IsFavorite { get; set; }
    bool IsGenreFavorite { get; set; }
    string? LastFmUrl { get; set; }
    DateTime? LastListen { get; set; }
    int ListenCount { get; set; }
    int LiveCount { get; set; }
    string? Members { get; set; }
    string? MusicBrainzID { get; set; }
    string Name { get; set; }
    string? OfficialSiteUrl { get; set; }
    string? SimilarArtists { get; set; }
    string? SongkickUrl { get; set; }
    string? SoundcloundUrl { get; set; }
    string? ThreadsUrl { get; set; }
    string? TiktokUrl { get; set; }
    long TotalDurationSeconds { get; set; }
    int TrackCount { get; set; }
    string? TwitterUrl { get; set; }
    string? WikipediaUrl { get; set; }
    int? YearMaxi { get; set; }
    int? YearMini { get; set; }
    string? YoutubeUrl { get; set; }

    string ToString();
}