namespace Rok.Domain.Entities;



public class UpdateAlbumEntity : IUpdateAlbumEntity
{
    public long Id { get; set; }

    public string? Sales { get; set; }

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

    public string? Label { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? Wikipedia { get; set; }

    public string? Theme { get; set; }

    public bool? IsLive { get; set; }

    public bool? IsBestOf { get; set; }

    public bool? IsCompilation { get; set; }

    public string? Biography { get; set; }
}
