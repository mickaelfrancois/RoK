namespace Rok.Infrastructure.NovaApi;

public class MusicDataArtistDto
{
    public string Origin { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string MusicBrainzID { get; set; } = string.Empty;

    public string? Biography { get; set; }

    public string? Website { get; set; }

    public string? Wikipedia { get; set; }

    public string? Facebook { get; set; }

    public string? Twitter { get; set; }

    public string? Flickr { get; set; }

    public string? Instagram { get; set; }

    public string? AllMusic { get; set; }

    public string? TikTok { get; set; }

    public string? Threads { get; set; }

    public string? SongKick { get; set; }

    public string? SoundCloud { get; set; }

    public string? Imdb { get; set; }

    public string? LastFM { get; set; }

    public string? Discogs { get; set; }

    public string? Bandsintown { get; set; }

    public string? Youtube { get; set; }

    public string? FanartUrl { get; set; }

    public string? Fanart2Url { get; set; }

    public string? Fanart3Url { get; set; }

    public string? Fanart4Url { get; set; }

    public string? Fanart5Url { get; set; }

    public string? BannerUrl { get; set; }

    public string? LogoUrl { get; set; }

    public string? PictureUrl { get; set; }

    public string? CountryCode { get; set; }

    public string? AudioDbID { get; set; }

    public int? BeginYear { get; set; }

    public int? EndYear { get; set; }

    public bool Disbanded { get; set; }

    public List<MusicDataMemberDto> Members { get; set; } = [];

    public override string ToString() => Name;
}


public class MusicDataMemberDto
{
    public string Name { get; set; } = string.Empty;

    public string MusicBrainzID { get; set; } = string.Empty;
}
