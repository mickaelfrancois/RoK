using Rok.Application.Features.Albums.Command;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Mapping;

internal static class AlbumDtoMapping
{
    public static AlbumDto Map(IAlbumEntity album)
    {
        return new AlbumDto
        {
            Name = album.Name,
            Year = album.Year,
            IsLive = album.IsLive,
            IsCompilation = album.IsCompilation,
            IsBestOf = album.IsBestOf,
            Wikipedia = album.Wikipedia,
            NovaUid = album.NovaUid,
            TrackCount = album.TrackCount,
            Duration = album.Duration,
            ReleaseDate = album.ReleaseDate,
            Label = album.Label,
            Speed = album.Speed,
            Theme = album.Theme,
            Mood = album.Mood,
            Sales = album.Sales,
            ReleaseFormat = album.ReleaseFormat,
            MusicBrainzID = album.MusicBrainzID,
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
            Id = album.Id,
            CreatDate = album.CreatDate,
            EditDate = album.EditDate
        };
    }

    public static AlbumEntity Map(AlbumDto album)
    {
        return new AlbumEntity
        {
            Id = album.Id,
            Name = album.Name,
            GenreId = album.GenreId
        };
    }

    public static UpdateAlbumEntity Map(PatchAlbumCommand album)
    {
        return new UpdateAlbumEntity
        {
            Id = album.Id,
            Sales = album.Sales,
            Label = album.Label,
            Mood = album.Mood,
            MusicBrainzID = album.MusicBrainzID,
            Speed = album.Speed,
            ReleaseDate = album.ReleaseDate,
            ReleaseFormat = album.ReleaseFormat,
            Wikipedia = album.Wikipedia,
            Theme = album.Theme
        };
    }
}
