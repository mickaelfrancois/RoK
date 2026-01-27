namespace Rok.Application.Dto;

public class AlbumDto
{
    public override string ToString() => Name;

    public long Id { get; set; }

    public DateTime CreatDate { get; set; }

    public DateTime? EditDate { get; set; }

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

    public string? Biography { get; set; }

    public string? Sales { get; set; }

    public string? ReleaseFormat { get; set; }

    public string? LastFmUrl { get; set; }

    public string AlbumPath { get; set; } = string.Empty;

    public long? ArtistId { get; set; }

    public long? GenreId { get; set; }

    public bool IsFavorite { get; set; }

    public int ListenCount { get; set; }

    public DateTime? LastListen { get; set; }

    public DateTime? GetMetaDataLastAttempt { get; set; }

    public string GenreName { get; set; } = string.Empty;

    public bool IsGenreFavorite { get; set; }

    public string ArtistName { get; set; } = string.Empty;

    public bool IsArtistFavorite { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public string? ArtistMusicBrainzID { get; set; }

    public bool IsLock { get; set; }

    public string? TagsAsString { get; set; }


    public List<string> GetTags()
    {
        return string.IsNullOrEmpty(TagsAsString) ? new List<string>() : TagsAsString.Split(',').ToList();
    }
}
