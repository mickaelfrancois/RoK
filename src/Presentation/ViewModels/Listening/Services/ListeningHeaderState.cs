using Rok.Application.Dto;

namespace Rok.ViewModels.Listening.Services;

/// <summary>Which parts of the Listening page header are shown for the current track.</summary>
public sealed record ListeningHeaderState
{
    public bool IsEmpty { get; init; }

    public bool HasArtist { get; init; }

    public bool HasAlbum { get; init; }

    public bool ShowArtistAlbumSeparator { get; init; }

    public bool ShowArtistFavorite { get; init; }

    public bool ShowAlbumFavorite { get; init; }

    public bool HasYear { get; init; }

    public bool HasGenre { get; init; }

    public bool HasCountry { get; init; }

    public bool HasArtistLinks { get; init; }

    public static ListeningHeaderState From(TrackDto? track, ArtistDto? loadedArtist, AlbumDto? loadedAlbum)
    {
        if (track is null)
        {
            return new ListeningHeaderState { IsEmpty = true };
        }

        var hasArtist = track.ArtistId.HasValue;
        var hasAlbum = track.AlbumId.HasValue;
        var artist = loadedArtist is not null && loadedArtist.Id == track.ArtistId ? loadedArtist : null;
        var album = loadedAlbum is not null && loadedAlbum.Id == track.AlbumId ? loadedAlbum : null;

        return new ListeningHeaderState
        {
            HasArtist = hasArtist,
            HasAlbum = hasAlbum,
            ShowArtistAlbumSeparator = hasArtist && hasAlbum,
            ShowArtistFavorite = artist is not null,
            ShowAlbumFavorite = album is not null,
            HasYear = album is not null && (album.ReleaseDate.HasValue || album.Year.HasValue),
            HasGenre = track.GenreId.HasValue && !string.IsNullOrWhiteSpace(track.GenreName),
            HasCountry = !string.IsNullOrWhiteSpace(track.CountryCode),
            HasArtistLinks = artist is not null && HasAnyLink(artist)
        };
    }

    private static bool HasAnyLink(ArtistDto artist)
    {
        return !string.IsNullOrWhiteSpace(artist.OfficialSiteUrl)
            || !string.IsNullOrWhiteSpace(artist.LastFmUrl)
            || !string.IsNullOrWhiteSpace(artist.WikipediaUrl)
            || !string.IsNullOrWhiteSpace(artist.FacebookUrl)
            || !string.IsNullOrWhiteSpace(artist.InstagramUrl)
            || !string.IsNullOrWhiteSpace(artist.TwitterUrl)
            || !string.IsNullOrWhiteSpace(artist.ThreadsUrl)
            || !string.IsNullOrWhiteSpace(artist.TiktokUrl)
            || !string.IsNullOrWhiteSpace(artist.YoutubeUrl);
    }
}