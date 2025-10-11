using Rok.Application.Dto.NovaApi;
using Rok.Application.Features.Artists.Command;
using Rok.Domain.Interfaces.Entities;
using Rok.Shared;

namespace Rok.Application.Mapping;

internal static class ArtistDtoMapping
{
    public static ArtistDto Map(IArtistEntity artist)
    {
        return new ArtistDto
        {
            Name = artist.Name,
            WikipediaUrl = artist.WikipediaUrl,
            OfficialSiteUrl = artist.OfficialSiteUrl,
            FacebookUrl = artist.FacebookUrl,
            TwitterUrl = artist.TwitterUrl,
            NovaUid = artist.NovaUid,
            MusicBrainzID = artist.MusicBrainzID,
            YearMini = artist.YearMini,
            YearMaxi = artist.YearMaxi,
            TrackCount = artist.TrackCount,
            AlbumCount = artist.AlbumCount,
            LiveCount = artist.LiveCount,
            CompilationCount = artist.CompilationCount,
            BestofCount = artist.BestofCount,
            IsFavorite = artist.IsFavorite,
            ListenCount = artist.ListenCount,
            LastListen = artist.LastListen,
            GenreId = artist.GenreId,
            CountryId = artist.CountryId,
            TotalDurationSeconds = artist.TotalDurationSeconds,
            FormedYear = artist.FormedYear,
            BornYear = artist.BornYear,
            DiedYear = artist.DiedYear,
            Disbanded = artist.Disbanded,
            Style = artist.Style,
            Gender = artist.Gender,
            Mood = artist.Mood,
            Members = artist.Members,
            SimilarArtists = artist.SimilarArtists,
            Biography = artist.Biography,
            GenreName = artist.GenreName,
            IsGenreFavorite = artist.IsGenreFavorite,
            CountryCode = artist.CountryCode,
            CountryName = artist.CountryName,
            Id = artist.Id,
            CreatDate = artist.CreatDate,
            EditDate = artist.EditDate,
            GetMetaDataLastAttempt = artist.GetMetaDataLastAttempt
        };
    }

    public static ArtistEntity Map(ArtistDto artist)
    {
        return new ArtistEntity
        {
            Id = artist.Id,
            Name = artist.Name,
            GenreId = artist.GenreId,
            CountryId = artist.CountryId
        };
    }

    public static ArtistEntity Map(CreateArtistCommand artist)
    {
        return new ArtistEntity
        {
            Name = artist.Name,
            GenreId = artist.GenreId,
            CountryId = artist.CountryId
        };
    }

    public static UpdateArtistEntity Map(PatchArtistCommand artist)
    {
        return new UpdateArtistEntity
        {
            Biography = artist.Biography,
            Id = artist.Id,
            WikipediaUrl = artist.WikipediaUrl,
            OfficialSiteUrl = artist.OfficialSiteUrl,
            FacebookUrl = artist.FacebookUrl,
            TwitterUrl = artist.TwitterUrl,
            NovaUid = artist.NovaUid,
            MusicBrainzID = artist.MusicBrainzID,
            FormedYear = artist.FormedYear,
            BornYear = artist.BornYear,
            DiedYear = artist.DiedYear,
            Disbanded = artist.Disbanded,
            Gender = artist.Gender,
            Mood = artist.Mood,
            Style = artist.Style,
        };
    }

    public static PatchArtistCommand Map(ApiArtistModel from)
    {
        PatchArtistCommand to = new()
        {
            FacebookUrl = from.Facebook,
            TwitterUrl = from.Twitter,
            Disbanded = from.Disbanded == "Yes",
            Gender = from.Gender,
            MusicBrainzID = from.MusicBrainzID,
            Mood = from.Mood,
            BornYear = from.BornYear,
            DiedYear = from.DiedYear,
            FormedYear = from.FormedYear,
            OfficialSiteUrl = from.Website,
            WikipediaUrl = from.Wikipedia,
            Style = from.Style
        };

        string language = LanguageHelpers.GetCurrentLanguage().ToLower();
        if (language == "fr" && string.IsNullOrEmpty(from.BiographyFR) == false)
            to.Biography = from.BiographyFR;
        else
            to.Biography = from.Biography;

        return to;
    }
}
