namespace Rok.Domain.Interfaces.Entities;

public interface IUpdateArtistEntity
{
    long Id { get; set; }
    string? AllMusicUrl { get; set; }
    string? AudioDbID { get; set; }
    string? BandsintownUrl { get; set; }
    string? Biography { get; set; }
    int? BornYear { get; set; }
    int? DiedYear { get; set; }
    bool Disbanded { get; set; }
    string? DiscogsUrl { get; set; }
    string? FacebookUrl { get; set; }
    string? FlickrUrl { get; set; }
    int? FormedYear { get; set; }
    string? ImdbUrl { get; set; }
    string? InstagramUrl { get; set; }
    string? LastFmUrl { get; set; }
    string? Members { get; set; }
    string? MusicBrainzID { get; set; }
    string? OfficialSiteUrl { get; set; }
    string? SimilarArtists { get; set; }
    string? SongkickUrl { get; set; }
    string? SoundcloundUrl { get; set; }
    string? ThreadsUrl { get; set; }
    string? TiktokUrl { get; set; }
    string? TwitterUrl { get; set; }
    string? WikipediaUrl { get; set; }
    string? YoutubeUrl { get; set; }
}