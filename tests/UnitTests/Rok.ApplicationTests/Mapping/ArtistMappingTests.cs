using Rok.Application.Features.Artists.Requests;
using Rok.Application.Mapping;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Mapping;

public class ArtistMappingTests
{
    [Fact(DisplayName = "ToDto should preserve all main artist properties")]
    public void ToDto_ShouldPreserveProperties()
    {
        // Arrange
        ArtistEntity entity = new()
        {
            Id = 11,
            Name = "Beatles",
            MusicBrainzID = "mbid",
            BornYear = 1960,
            DiedYear = 1970,
            Disbanded = true,
            TrackCount = 100,
            AlbumCount = 13,
            CountryCode = "GB",
            GenreName = "Rock",
            TagsAsString = "british;legendary"
        };

        // Act
        ArtistDto dto = entity.ToDto();

        // Assert
        Assert.Equal(11, dto.Id);
        Assert.Equal("Beatles", dto.Name);
        Assert.Equal("mbid", dto.MusicBrainzID);
        Assert.Equal(1960, dto.BornYear);
        Assert.Equal(1970, dto.DiedYear);
        Assert.True(dto.Disbanded);
        Assert.Equal(100, dto.TrackCount);
        Assert.Equal(13, dto.AlbumCount);
        Assert.Equal("GB", dto.CountryCode);
        Assert.Equal("Rock", dto.GenreName);
        Assert.Equal("british;legendary", dto.TagsAsString);
    }

    [Fact(DisplayName = "ToEntity should map a CreateArtistRequest to an ArtistEntity")]
    public void ToEntity_ShouldMapCreateCommand()
    {
        // Arrange
        CreateArtistRequest command = new()
        {
            Name = "Beatles",
            MusicBrainzID = "mbid",
            WikipediaUrl = "wiki",
            FacebookUrl = "fb",
            BornYear = 1960,
            DiedYear = 1970,
            Biography = "bio",
            GenreId = 1,
            CountryId = 2,
            TrackCount = 50,
            AlbumCount = 10
        };

        // Act
        ArtistEntity entity = command.ToEntity();

        // Assert
        Assert.Equal("Beatles", entity.Name);
        Assert.Equal("mbid", entity.MusicBrainzID);
        Assert.Equal("wiki", entity.WikipediaUrl);
        Assert.Equal("fb", entity.FacebookUrl);
        Assert.Equal(1960, entity.BornYear);
        Assert.Equal(1970, entity.DiedYear);
        Assert.Equal("bio", entity.Biography);
        Assert.Equal(1, entity.GenreId);
        Assert.Equal(2, entity.CountryId);
        Assert.Equal(50, entity.TrackCount);
        Assert.Equal(10, entity.AlbumCount);
    }

    [Fact(DisplayName = "ToCommand should map an ArtistDto to an UpdateArtistRequest")]
    public void ToCommand_ShouldMapDtoToUpdateCommand()
    {
        // Arrange
        ArtistDto dto = new()
        {
            Id = 11,
            MusicBrainzID = "mbid",
            WikipediaUrl = "wiki",
            BornYear = 1960,
            DiedYear = 1970,
            Disbanded = true,
            FormedYear = 1958,
            Biography = "bio"
        };

        // Act
        UpdateArtistRequest command = dto.ToCommand();

        // Assert
        Assert.Equal(11, command.Id);
        Assert.Equal("mbid", command.MusicBrainzID);
        Assert.Equal("wiki", command.WikipediaUrl);
        Assert.Equal(1960, command.BornYear);
        Assert.Equal(1970, command.DiedYear);
        Assert.True(command.Disbanded);
        Assert.Equal(1958, command.FormedYear);
        Assert.Equal("bio", command.Biography);
    }
}
