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
            FacebookUrl = new PatchField<string>(from.Facebook),
            TwitterUrl = new PatchField<string>(from.Twitter),
            Disbanded = new PatchField<bool>(from.Disbanded == "Yes"),
            Gender = new PatchField<string>(from.Gender),
            MusicBrainzID = new PatchField<string>(from.MusicBrainzID),
            Mood = new PatchField<string>(from.Mood),
            BornYear = from.BornYear.HasValue ? new PatchField<int>(from.BornYear.Value) : default,
            DiedYear = from.DiedYear.HasValue ? new PatchField<int>(from.DiedYear.Value) : default,
            FormedYear = from.FormedYear.HasValue ? new PatchField<int>(from.FormedYear.Value) : default,
            OfficialSiteUrl = new PatchField<string>(from.Website),
            WikipediaUrl = new PatchField<string>(from.Wikipedia),
            Style = new PatchField<string>(from.Style)
        };

        string language = LanguageHelpers.GetCurrentLanguage().ToLower();
        if (language == "fr" && !string.IsNullOrEmpty(from.BiographyFR))
            to.Biography = new PatchField<string>(from.BiographyFR);
        else
            to.Biography = new PatchField<string>(from.Biography);

        return to;
    }
}
