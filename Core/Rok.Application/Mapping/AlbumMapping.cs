using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Mapping;

internal static class AlbumMapping
{
    public static AlbumDto ToDto(this IAlbumEntity album)
    {
        return new AlbumDto
        {
            Id = album.Id,
            CreatDate = album.CreatDate,
            EditDate = album.EditDate,
            Name = album.Name,
            MusicBrainzID = album.MusicBrainzID,
            ReleaseGroupMusicBrainzID = album.ReleaseGroupMusicBrainzID,

            Year = album.Year,
            IsLive = album.IsLive,
            IsCompilation = album.IsCompilation,
            IsBestOf = album.IsBestOf,
            Wikipedia = album.Wikipedia,
            TrackCount = album.TrackCount,
            Duration = album.Duration,
            ReleaseDate = album.ReleaseDate,
            Label = album.Label,
            Sales = album.Sales,
            ReleaseFormat = album.ReleaseFormat,
            AlbumPath = album.AlbumPath,
            ArtistId = album.ArtistId,
            GenreId = album.GenreId,
            IsFavorite = album.IsFavorite,
            ListenCount = album.ListenCount,
            LastListen = album.LastListen,
            GenreName = album.GenreName,
            IsGenreFavorite = album.IsGenreFavorite,
            ArtistName = album.ArtistName,
            IsArtistFavorite = album.IsArtistFavorite,
            CountryCode = album.CountryCode,
            CountryName = album.CountryName,
            ArtistMusicBrainzID = album.ArtistMusicBrainzID,
            AllMusicID = album.AllMusicID,
            AmazonID = album.AmazonID,
            AudioDbArtistID = album.AudioDbArtistID,
            AudioDbID = album.AudioDbID,
            Biography = album.Biography,
            DiscogsID = album.DiscogsID,
            GeniusID = album.GeniusID,
            LyricWikiID = album.LyricWikiID,
            MusicMozID = album.MusicMozID,
            WikidataID = album.WikidataID,
            WikipediaID = album.WikipediaID,
            LastFmUrl = album.LastFmUrl,
            IsLock = album.IsLock,

            GetMetaDataLastAttempt = album.GetMetaDataLastAttempt
        };
    }
}
