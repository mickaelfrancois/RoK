namespace Rok.Domain.Entities;


public class UpdateArtistEntity : IUpdateArtistEntity
{
    public long Id { get; set; }

    public string? MusicBrainzID { get; set; }

    /* Social networks and external links */

    public string? WikipediaUrl { get; set; }

    public string? OfficialSiteUrl { get; set; }

    public string? FacebookUrl { get; set; }

    public string? TwitterUrl { get; set; }

    public string? FlickrUrl { get; set; }

    public string? InstagramUrl { get; set; }

    public string? TiktokUrl { get; set; }

    public string? ThreadsUrl { get; set; }

    public string? SongkickUrl { get; set; }

    public string? SoundcloundUrl { get; set; }

    public string? ImdbUrl { get; set; }

    public string? LastFmUrl { get; set; }

    public string? DiscogsUrl { get; set; }

    public string? BandsintownUrl { get; set; }

    public string? YoutubeUrl { get; set; }

    public string? AudioDbID { get; set; }

    public string? AllMusicUrl { get; set; }

    public int? FormedYear { get; set; }

    public int? BornYear { get; set; }

    public int? DiedYear { get; set; }

    public bool Disbanded { get; set; }

    public string? Members { get; set; }

    public string? SimilarArtists { get; set; }

    public string? Biography { get; set; }
}
