namespace Rok.Application.Dto;

public class ArtistDto
{
    public override string ToString() => Name;

    public long Id { get; set; }

    public DateTime CreatDate { get; set; }

    public DateTime? EditDate { get; set; }

    public string Name { get; set; } = string.Empty;

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

    /* Additional info */

    public int? YearMini { get; set; }

    public int? YearMaxi { get; set; }

    public int TrackCount { get; set; } = 0;

    public int AlbumCount { get; set; } = 0;

    public int LiveCount { get; set; } = 0;

    public int CompilationCount { get; set; }

    public int BestofCount { get; set; } = 0;

    public bool IsFavorite { get; set; }

    public int ListenCount { get; set; }

    public DateTime? LastListen { get; set; }

    public long? GenreId { get; set; }

    public long? CountryId { get; set; }

    public long TotalDurationSeconds { get; set; } = 0;

    public int? FormedYear { get; set; }

    public int? BornYear { get; set; }

    public int? DiedYear { get; set; }

    public bool Disbanded { get; set; }

    public string? Members { get; set; }

    public string? SimilarArtists { get; set; }

    public string? Biography { get; set; }

    public DateTime? GetMetaDataLastAttempt { get; set; }



    public string GenreName { get; set; } = string.Empty;

    public bool IsGenreFavorite { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;
}
