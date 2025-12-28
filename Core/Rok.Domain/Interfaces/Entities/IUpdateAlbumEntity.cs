namespace Rok.Domain.Interfaces.Entities;

public interface IUpdateAlbumEntity
{
    long Id { get; set; }

    string? AllMusicID { get; set; }
    string? AmazonID { get; set; }
    string? AudioDbArtistID { get; set; }
    string? AudioDbID { get; set; }
    string? DiscogsID { get; set; }
    string? GeniusID { get; set; }
    bool? IsBestOf { get; set; }
    bool? IsCompilation { get; set; }
    bool? IsLive { get; set; }
    string? Label { get; set; }
    string? LyricWikiID { get; set; }
    string? MusicBrainzID { get; set; }
    string? MusicMozID { get; set; }
    DateTime? ReleaseDate { get; set; }
    string? ReleaseGroupMusicBrainzID { get; set; }
    string? Sales { get; set; }
    string? Theme { get; set; }
    string? WikidataID { get; set; }
    string? Wikipedia { get; set; }
    string? WikipediaID { get; set; }
}