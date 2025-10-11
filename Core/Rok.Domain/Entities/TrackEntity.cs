namespace Rok.Domain.Entities;


[Table("Tracks")]
public class TrackEntity : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public long? ArtistId { get; set; }

    public long? AlbumId { get; set; }

    public long? GenreId { get; set; }

    public int? TrackNumber { get; set; }

    public long Duration { get; set; }

    public long Size { get; set; }

    public int Bitrate { get; set; }

    public string? NovaUid { get; set; }

    public string? MusicBrainzID { get; set; }

    public string MusicFile { get; set; } = string.Empty;

    public DateTime FileDate { get; set; }

    public bool IsLive { get; set; }

    public int Score { get; set; }

    public int ListenCount { get; set; }

    public DateTime? LastListen { get; set; }

    public int SkipCount { get; set; }

    public DateTime? LastSkip { get; set; }

    public DateTime? GetLyricsLastAttempt { get; set; }


    [Write(false)]
    public string AlbumName { get; set; } = string.Empty;

    [Write(false)]
    public bool IsAlbumFavorite { get; set; }

    [Write(false)]
    public bool IsAlbumCompilation { get; set; }

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