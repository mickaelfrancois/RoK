using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Mapping;

internal static class AlbumMapping
{
    public static AlbumDto ToDto(this IAlbumEntity entity)
    {
        return new AlbumDto
        {
            Id = entity.Id,
            CreatDate = entity.CreatDate,
            EditDate = entity.EditDate,
            Name = entity.Name,
            MusicBrainzID = entity.MusicBrainzID,
            ReleaseGroupMusicBrainzID = entity.ReleaseGroupMusicBrainzID,

            Year = entity.Year,
            IsLive = entity.IsLive,
            IsCompilation = entity.IsCompilation,
            IsBestOf = entity.IsBestOf,
            Wikipedia = entity.Wikipedia,
            TrackCount = entity.TrackCount,
            Duration = entity.Duration,
            ReleaseDate = entity.ReleaseDate,
            Label = entity.Label,
            Sales = entity.Sales,
            ReleaseFormat = entity.ReleaseFormat,
            AlbumPath = entity.AlbumPath,
            ArtistId = entity.IsCompilation ? null : entity.ArtistId,
            GenreId = entity.GenreId,
            IsFavorite = entity.IsFavorite,
            ListenCount = entity.ListenCount,
            LastListen = entity.LastListen,
            GenreName = entity.GenreName,
            IsGenreFavorite = entity.IsGenreFavorite,
            ArtistName = entity.IsCompilation ? "N/A" : entity.ArtistName,
            IsArtistFavorite = entity.IsArtistFavorite,
            CountryCode = entity.CountryCode,
            CountryName = entity.CountryName,
            ArtistMusicBrainzID = entity.IsCompilation ? string.Empty : entity.ArtistMusicBrainzID,
            AllMusicID = entity.AllMusicID,
            AmazonID = entity.AmazonID,
            AudioDbArtistID = entity.AudioDbArtistID,
            AudioDbID = entity.AudioDbID,
            Biography = entity.Biography,
            DiscogsID = entity.DiscogsID,
            GeniusID = entity.GeniusID,
            LyricWikiID = entity.LyricWikiID,
            MusicMozID = entity.MusicMozID,
            WikidataID = entity.WikidataID,
            WikipediaID = entity.WikipediaID,
            LastFmUrl = entity.LastFmUrl,
            IsLock = entity.IsLock,

            GetMetaDataLastAttempt = entity.GetMetaDataLastAttempt,
            TagsAsString = entity.TagsAsString
        };
    }
}
