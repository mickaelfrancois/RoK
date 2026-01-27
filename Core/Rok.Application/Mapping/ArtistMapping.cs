using Rok.Application.Features.Artists.Command;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Mapping;

public static class ArtistMapping
{
    public static ArtistDto ToDto(this IArtistEntity entity)
    {
        return new ArtistDto
        {
            Id = entity.Id,
            CreatDate = entity.CreatDate,
            EditDate = entity.EditDate,
            Name = entity.Name,
            MusicBrainzID = entity.MusicBrainzID,

            WikipediaUrl = entity.WikipediaUrl,
            OfficialSiteUrl = entity.OfficialSiteUrl,
            FacebookUrl = entity.FacebookUrl,
            TwitterUrl = entity.TwitterUrl,
            AllMusicUrl = entity.AllMusicUrl,
            FlickrUrl = entity.FlickrUrl,
            InstagramUrl = entity.InstagramUrl,
            TiktokUrl = entity.TiktokUrl,
            ThreadsUrl = entity.ThreadsUrl,
            SongkickUrl = entity.SongkickUrl,
            SoundcloundUrl = entity.SoundcloundUrl,
            ImdbUrl = entity.ImdbUrl,
            LastFmUrl = entity.LastFmUrl,
            DiscogsUrl = entity.DiscogsUrl,
            BandsintownUrl = entity.BandsintownUrl,
            YoutubeUrl = entity.YoutubeUrl,
            AudioDbID = entity.AudioDbID,

            YearMini = entity.YearMini,
            YearMaxi = entity.YearMaxi,
            TrackCount = entity.TrackCount,
            AlbumCount = entity.AlbumCount,
            LiveCount = entity.LiveCount,
            CompilationCount = entity.CompilationCount,
            BestofCount = entity.BestofCount,
            IsFavorite = entity.IsFavorite,
            ListenCount = entity.ListenCount,
            LastListen = entity.LastListen,
            GenreId = entity.GenreId,
            CountryId = entity.CountryId,
            TotalDurationSeconds = entity.TotalDurationSeconds,
            FormedYear = entity.FormedYear,
            BornYear = entity.BornYear,
            DiedYear = entity.DiedYear,
            Disbanded = entity.Disbanded,
            Members = entity.Members,
            SimilarArtists = entity.SimilarArtists,
            Biography = entity.Biography,
            GenreName = entity.GenreName,
            IsGenreFavorite = entity.IsGenreFavorite,
            CountryCode = entity.CountryCode,
            CountryName = entity.CountryName,
            GetMetaDataLastAttempt = entity.GetMetaDataLastAttempt,
            TagsAsString = entity.TagsAsString
        };
    }

    public static ArtistEntity ToEntity(this CreateArtistCommand command)
    {
        return new ArtistEntity
        {
            Name = command.Name,
            MusicBrainzID = command.MusicBrainzID,
            WikipediaUrl = command.WikipediaUrl,
            OfficialSiteUrl = command.OfficialSiteUrl,
            FacebookUrl = command.FacebookUrl,
            TwitterUrl = command.TwitterUrl,
            AllMusicUrl = command.AllMusicUrl,
            BandsintownUrl = command.BandsintownUrl,
            CompilationCount = command.CompilationCount,
            DiscogsUrl = command.DiscogsUrl,
            FlickrUrl = command.FlickrUrl,
            InstagramUrl = command.InstagramUrl,
            ImdbUrl = command.ImdbUrl,
            LastFmUrl = command.LastFmUrl,
            SoundcloundUrl = command.SoundcloundUrl,
            SongkickUrl = command.SongkickUrl,
            ThreadsUrl = command.ThreadsUrl,
            TiktokUrl = command.TiktokUrl,
            YoutubeUrl = command.YoutubeUrl,
            YearMini = command.YearMini,
            YearMaxi = command.YearMaxi,
            TrackCount = command.TrackCount,
            AlbumCount = command.AlbumCount,
            BestofCount = command.BestofCount,
            LiveCount = command.LiveCount,
            AudioDbID = command.AudioDbID,
            BornYear = command.BornYear,
            DiedYear = command.DiedYear,
            Biography = command.Biography,
            SimilarArtists = command.SimilarArtists,
            Members = command.Members,
            GenreId = command.GenreId,
            CountryId = command.CountryId
        };
    }

    public static UpdateArtistCommand ToCommand(this ArtistDto dto)
    {
        return new UpdateArtistCommand
        {
            Id = dto.Id,
            MusicBrainzID = dto.MusicBrainzID,
            WikipediaUrl = dto.WikipediaUrl,
            OfficialSiteUrl = dto.OfficialSiteUrl,
            FacebookUrl = dto.FacebookUrl,
            TwitterUrl = dto.TwitterUrl,
            AllMusicUrl = dto.AllMusicUrl,
            BandsintownUrl = dto.BandsintownUrl,
            DiscogsUrl = dto.DiscogsUrl,
            FlickrUrl = dto.FlickrUrl,
            InstagramUrl = dto.InstagramUrl,
            ImdbUrl = dto.ImdbUrl,
            LastFmUrl = dto.LastFmUrl,
            SoundcloundUrl = dto.SoundcloundUrl,
            SongkickUrl = dto.SongkickUrl,
            ThreadsUrl = dto.ThreadsUrl,
            TiktokUrl = dto.TiktokUrl,
            YoutubeUrl = dto.YoutubeUrl,
            AudioDbID = dto.AudioDbID,
            BornYear = dto.BornYear,
            DiedYear = dto.DiedYear,
            Biography = dto.Biography,
            SimilarArtists = dto.SimilarArtists,
            Members = dto.Members,
            FormedYear = dto.FormedYear,
            Disbanded = dto.Disbanded,
        };
    }
}
