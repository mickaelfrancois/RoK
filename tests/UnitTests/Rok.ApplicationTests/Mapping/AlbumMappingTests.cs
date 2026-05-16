using Rok.Application.Mapping;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Mapping;

public class AlbumMappingTests
{
    [Fact(DisplayName = "ToDto should preserve all main properties from the entity")]
    public void ToDto_ShouldPreserveMainProperties()
    {
        // Arrange
        AlbumEntity entity = new()
        {
            Id = 7,
            Name = "Greatest Hits",
            Year = 2020,
            ArtistId = 11,
            GenreId = 3,
            IsLive = true,
            ListenCount = 5,
            ArtistName = "Artist X",
            ArtistMusicBrainzID = "abcdef",
            IsCompilation = false
        };

        // Act
        AlbumDto dto = entity.ToDto();

        // Assert
        Assert.Equal(7, dto.Id);
        Assert.Equal("Greatest Hits", dto.Name);
        Assert.Equal(2020, dto.Year);
        Assert.Equal(11, dto.ArtistId);
        Assert.Equal(3, dto.GenreId);
        Assert.True(dto.IsLive);
        Assert.Equal(5, dto.ListenCount);
        Assert.Equal("Artist X", dto.ArtistName);
        Assert.Equal("abcdef", dto.ArtistMusicBrainzID);
    }

    [Fact(DisplayName = "ToDto should null out ArtistId and substitute ArtistName when album is a compilation")]
    public void ToDto_ShouldHideArtistFields_WhenCompilation()
    {
        // Arrange
        AlbumEntity entity = new()
        {
            Id = 7,
            Name = "Hits 2020",
            ArtistId = 11,
            ArtistName = "Various",
            ArtistMusicBrainzID = "abcdef",
            IsCompilation = true
        };

        // Act
        AlbumDto dto = entity.ToDto();

        // Assert
        Assert.Null(dto.ArtistId);
        Assert.Equal("N/A", dto.ArtistName);
        Assert.Equal(string.Empty, dto.ArtistMusicBrainzID);
        Assert.True(dto.IsCompilation);
    }

    [Fact(DisplayName = "ToDto should preserve metadata fields and tags as string")]
    public void ToDto_ShouldPreserveMetadataAndTags()
    {
        // Arrange
        AlbumEntity entity = new()
        {
            Id = 1,
            Name = "Album",
            MusicBrainzID = "mbid",
            Biography = "bio",
            TagsAsString = "rock;live",
            CountryCode = "FR",
            CountryName = "France"
        };

        // Act
        AlbumDto dto = entity.ToDto();

        // Assert
        Assert.Equal("mbid", dto.MusicBrainzID);
        Assert.Equal("bio", dto.Biography);
        Assert.Equal("rock;live", dto.TagsAsString);
        Assert.Equal("FR", dto.CountryCode);
        Assert.Equal("France", dto.CountryName);
    }
}