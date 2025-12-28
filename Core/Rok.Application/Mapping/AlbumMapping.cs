using Rok.Application.Features.Albums.Command;
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

            GetMetaDataLastAttempt = album.GetMetaDataLastAttempt
        };
    }

    public static UpdateAlbumCommand ToCommand(this AlbumDto dto)
    {
        return new UpdateAlbumCommand
        {
            Id = dto.Id,
            MusicBrainzID = dto.MusicBrainzID,
            AllMusicID = dto.AllMusicID,
            ReleaseGroupMusicBrainzID = dto.ReleaseGroupMusicBrainzID,
            Sales = dto.Sales,
            AudioDbID = dto.AudioDbID,
            AudioDbArtistID = dto.AudioDbArtistID,
            DiscogsID = dto.DiscogsID,
            MusicMozID = dto.MusicMozID,
            LyricWikiID = dto.LyricWikiID,
            GeniusID = dto.GeniusID,
            WikipediaID = dto.WikipediaID,
            WikidataID = dto.WikidataID,
            AmazonID = dto.AmazonID,
            Label = dto.Label,
            ReleaseDate = dto.ReleaseDate,
            Wikipedia = dto.Wikipedia,
            IsLive = dto.IsLive,
            IsBestOf = dto.IsBestOf,
            IsCompilation = dto.IsCompilation,
            Biography = dto.Biography
        };
    }
}
