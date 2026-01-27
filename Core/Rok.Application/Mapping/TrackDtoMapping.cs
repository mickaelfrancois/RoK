namespace Rok.Application.Mapping;

internal static class TrackDtoMapping
{
    public static TrackDto Map(TrackEntity entity)
    {
        return new TrackDto
        {
            Title = entity.Title,
            ArtistId = entity.ArtistId,
            AlbumId = entity.AlbumId,
            GenreId = entity.GenreId,
            TrackNumber = entity.TrackNumber,
            Duration = entity.Duration,
            Size = entity.Size,
            Bitrate = entity.Bitrate,
            NovaUid = entity.NovaUid,
            MusicBrainzID = entity.MusicBrainzID,
            MusicFile = entity.MusicFile,
            FileDate = entity.FileDate,
            IsLive = entity.IsLive,
            Score = entity.Score,
            ListenCount = entity.ListenCount,
            LastListen = entity.LastListen,
            SkipCount = entity.SkipCount,
            LastSkip = entity.LastSkip,
            AlbumName = entity.AlbumName,
            IsAlbumFavorite = entity.IsAlbumFavorite,
            IsAlbumCompilation = entity.IsAlbumCompilation,
            IsAlbumLive = entity.IsAlbumLive,
            GenreName = entity.GenreName,
            IsGenreFavorite = entity.IsGenreFavorite,
            ArtistName = entity.ArtistName,
            IsArtistFavorite = entity.IsArtistFavorite,
            CountryCode = entity.CountryCode,
            CountryName = entity.CountryName,
            Id = entity.Id,
            CreatDate = entity.CreatDate,
            EditDate = entity.EditDate,
            GetLyricsLastAttempt = entity.GetLyricsLastAttempt,
            TagsAsString = entity.TagsAsString
        };
    }
}
