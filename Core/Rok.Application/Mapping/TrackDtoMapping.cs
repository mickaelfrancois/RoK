namespace Rok.Application.Mapping;

internal static class TrackDtoMapping
{
    public static TrackDto Map(TrackEntity track)
    {
        return new TrackDto
        {
            Title = track.Title,
            ArtistId = track.ArtistId,
            AlbumId = track.AlbumId,
            GenreId = track.GenreId,
            TrackNumber = track.TrackNumber,
            Duration = track.Duration,
            Size = track.Size,
            Bitrate = track.Bitrate,
            NovaUid = track.NovaUid,
            MusicBrainzID = track.MusicBrainzID,
            MusicFile = track.MusicFile,
            FileDate = track.FileDate,
            IsLive = track.IsLive,
            Score = track.Score,
            ListenCount = track.ListenCount,
            LastListen = track.LastListen,
            SkipCount = track.SkipCount,
            LastSkip = track.LastSkip,
            AlbumName = track.AlbumName,
            IsAlbumFavorite = track.IsAlbumFavorite,
            IsAlbumCompilation = track.IsAlbumCompilation,
            IsAlbumLive = track.IsAlbumLive,
            GenreName = track.GenreName,
            IsGenreFavorite = track.IsGenreFavorite,
            ArtistName = track.ArtistName,
            IsArtistFavorite = track.IsArtistFavorite,
            CountryCode = track.CountryCode,
            CountryName = track.CountryName,
            Id = track.Id,
            CreatDate = track.CreatDate,
            EditDate = track.EditDate,
            GetLyricsLastAttempt = track.GetLyricsLastAttempt
        };
    }
}
