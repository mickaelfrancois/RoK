namespace Rok.Application.Dto;

public class TrackDto
{
    public override string ToString() => Title;

    public long Id { get; set; }

    public DateTime CreatDate { get; set; }

    public DateTime? EditDate { get; set; }

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

    public string AlbumName { get; set; } = string.Empty;

    public bool IsAlbumFavorite { get; set; }

    public bool IsAlbumCompilation { get; set; }

    public bool IsAlbumLive { get; set; }

    public string GenreName { get; set; } = string.Empty;

    public bool IsGenreFavorite { get; set; }

    public string ArtistName { get; set; } = string.Empty;

    public bool IsArtistFavorite { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public bool Listening { get; set; }
}
