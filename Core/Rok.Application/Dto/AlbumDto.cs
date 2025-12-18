namespace Rok.Application.Dto;

public class AlbumDto
{
    public override string ToString() => Name;

    public long Id { get; set; }

    public DateTime CreatDate { get; set; }

    public DateTime? EditDate { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Year { get; set; }

    public bool IsLive { get; set; } = false;

    public bool IsCompilation { get; set; } = false;

    public bool IsBestOf { get; set; } = false;

    public string? Wikipedia { get; set; }

    public string? NovaUid { get; set; }

    public int TrackCount { get; set; } = 0;

    public long Duration { get; set; } = 0;

    public DateTime? ReleaseDate { get; set; }

    public string? Label { get; set; }

    public string? Speed { get; set; }

    public string? Theme { get; set; }

    public string? Mood { get; set; }

    public string? Sales { get; set; }

    public string? ReleaseFormat { get; set; }

    public string? MusicBrainzID { get; set; }

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
}
