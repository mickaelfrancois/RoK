namespace Rok.Domain.Entities;


[Table("Albums")]
public class AlbumEntity : BaseEntity, IAlbumEntity
{
    public override string ToString() => Name;

    public string Name { get; set; } = string.Empty;

    public string? MusicBrainzID { get; set; }

    public string? ReleaseGroupMusicBrainzID { get; set; }

    public string? AudioDbID { get; set; }

    public string? AudioDbArtistID { get; set; }

    public string? AllMusicID { get; set; }

    public string? DiscogsID { get; set; }

    public string? MusicMozID { get; set; }

    public string? LyricWikiID { get; set; }

    public string? GeniusID { get; set; }

    public string? WikipediaID { get; set; }

    public string? WikidataID { get; set; }

    public string? AmazonID { get; set; }

    public int? Year { get; set; }

    public bool IsLive { get; set; } = false;

    public bool IsCompilation { get; set; } = false;

    public bool IsBestOf { get; set; } = false;

    public string? Wikipedia { get; set; }

    public int TrackCount { get; set; } = 0;

    public long Duration { get; set; } = 0;

    public DateTime? ReleaseDate { get; set; }

    public string? Label { get; set; }

    public string? Sales { get; set; }

    public string? ReleaseFormat { get; set; }

    public string AlbumPath { get; set; } = string.Empty;

    public long? ArtistId { get; set; }

    public long? GenreId { get; set; }

    public bool IsFavorite { get; set; }

    public int ListenCount { get; set; }

    public DateTime? LastListen { get; set; }

    public DateTime? GetMetaDataLastAttempt { get; set; }

    public string? Biography { get; set; }


    [Write(false)]
    public string GenreName { get; set; } = string.Empty;

    [Write(false)]
    public bool IsGenreFavorite { get; set; }

    [Write(false)]
    public string ArtistName { get; set; } = string.Empty;

    [Write(false)]
    public bool IsArtistFavorite { get; set; }

    [Write(false)]
    public string CountryCode { get; set; } = string.Empty;

    [Write(false)]
    public string CountryName { get; set; } = string.Empty;
}
