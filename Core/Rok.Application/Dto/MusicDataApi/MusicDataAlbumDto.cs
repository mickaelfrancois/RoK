namespace Rok.Infrastructure.NovaApi;

public class MusicDataAlbumDto
{
    public string Origin { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string MusicBrainzArtistID { get; set; } = string.Empty;

    public string MusicBrainzID { get; set; } = string.Empty;

    public string? Artist { get; set; }

    public string? Biography { get; set; }

    public string? Wikipedia { get; set; }

    public string? PictureUrl { get; set; }

    public string? LastFM { get; set; }

    public string? Year { get; set; }

    public string? ReleaseGroupMusicBrainzID { get; set; }

    public string? AudioDbID { get; set; }

    public string? AudioDbArtistID { get; set; }

    public string? ReleaseFormat { get; set; }

    public string? Sales { get; set; }

    public string? AllMusicID { get; set; }

    public string? DiscogsID { get; set; }

    public string? MusicMozID { get; set; }

    public string? LyricWikiID { get; set; }

    public string? GeniusID { get; set; }

    public string? WikipediaID { get; set; }

    public string? WikidataID { get; set; }

    public string? AmazonID { get; set; }

    public int? Score { get; set; }

    public string? Label { get; set; }

    public string? Genre { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public List<MusicDataTrackDto> Tracks { get; set; } = [];


    public override string ToString() => $"{Name} - {Artist}";
}


public class MusicDataTrackDto
{
    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }

    public int? Duration { get; set; }

    public override string ToString() => $"{Position}. {Name}";
}