using Moq;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Artists;

public class GetArtistByIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped artist when artist exists")]
    public async Task Handle_ShouldReturnMappedArtist_WhenArtistExists()
    {
        // Arrange
        ArtistEntity entity = new() { Id = 7, Name = "Daft Punk" };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(7, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        GetArtistByIdQueryHandler handler = new(repository.Object);

        // Act
        Result<ArtistDto> result = await handler.HandleAsync(new GetArtistByIdQuery(7), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Daft Punk", result.Value!.Name);
    }

    [Fact(DisplayName = "Handle should return NotFound failure when artist does not exist")]
    public async Task Handle_ShouldReturnNotFoundFailure_WhenArtistDoesNotExist()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((ArtistEntity?)null);
        GetArtistByIdQueryHandler handler = new(repository.Object);

        // Act
        Result<ArtistDto> result = await handler.HandleAsync(new GetArtistByIdQuery(999), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class GetArtistByNameQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped artist when artist name matches")]
    public async Task Handle_ShouldReturnMappedArtist_WhenArtistNameMatches()
    {
        // Arrange
        ArtistEntity entity = new() { Id = 1, Name = "Radiohead" };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByNameAsync("Radiohead", It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        GetArtistByNameQueryHandler handler = new(repository.Object);

        // Act
        Result<ArtistDto> result = await handler.HandleAsync(new GetArtistByNameQuery("Radiohead"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Radiohead", result.Value!.Name);
    }

    [Fact(DisplayName = "Handle should return NotFound failure when artist name is unknown")]
    public async Task Handle_ShouldReturnNotFoundFailure_WhenArtistNameIsUnknown()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((ArtistEntity?)null);
        GetArtistByNameQueryHandler handler = new(repository.Object);

        // Act
        Result<ArtistDto> result = await handler.HandleAsync(new GetArtistByNameQuery("Unknown"), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class GetAllArtistsQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return all mapped artists by default")]
    public async Task Handle_ShouldReturnAllMappedArtists_ByDefault()
    {
        // Arrange
        List<ArtistEntity> artists = new()
        {
            new() { Id = 1, Name = "A", AlbumCount = 0, LiveCount = 0, BestofCount = 0 },
            new() { Id = 2, Name = "B", AlbumCount = 1 }
        };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        GetAllArtistsQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<ArtistDto> result = await handler.HandleAsync(new GetAllArtistsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact(DisplayName = "Handle should exclude artists without any album when flag is set")]
    public async Task Handle_ShouldExcludeArtistsWithoutAlbum_WhenFlagIsSet()
    {
        // Arrange
        List<ArtistEntity> artists = new()
        {
            new() { Id = 1, Name = "Without", AlbumCount = 0, LiveCount = 0, BestofCount = 0 },
            new() { Id = 2, Name = "With Studio", AlbumCount = 3 },
            new() { Id = 3, Name = "With Live", LiveCount = 1 },
            new() { Id = 4, Name = "With BestOf", BestofCount = 1 }
        };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        GetAllArtistsQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<ArtistDto> result = await handler.HandleAsync(new GetAllArtistsQuery { ExcludeArtistsWithoutAlbum = true }, CancellationToken.None);

        // Assert
        List<ArtistDto> list = result.ToList();
        Assert.Equal(3, list.Count);
        Assert.DoesNotContain(list, a => a.Name == "Without");
    }
}
