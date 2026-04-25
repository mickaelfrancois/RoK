using Moq;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Interfaces.Entities;

namespace Rok.ApplicationTests.Features.Albums;

public class GetAlbumByIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped album when album exists")]
    public async Task Handle_ShouldReturnMappedAlbum_WhenAlbumExists()
    {
        // Arrange
        AlbumEntity entity = new() { Id = 7, Name = "Thriller" };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(7, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        GetAlbumByIdQueryHandler handler = new(repository.Object);

        // Act
        Result<AlbumDto> result = await handler.HandleAsync(new GetAlbumByIdQuery(7), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.Id);
        Assert.Equal("Thriller", result.Value.Name);
    }

    [Fact(DisplayName = "Handle should return NotFound failure when album does not exist")]
    public async Task Handle_ShouldReturnNotFoundFailure_WhenAlbumDoesNotExist()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((AlbumEntity?)null);
        GetAlbumByIdQueryHandler handler = new(repository.Object);

        // Act
        Result<AlbumDto> result = await handler.HandleAsync(new GetAlbumByIdQuery(99), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class GetAllAlbumsQueryHandlerTests
{
    [Fact(DisplayName = "Handle should map all albums returned by repository")]
    public async Task Handle_ShouldMapAllAlbums_ReturnedByRepository()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, Name = "Album 1" },
            new() { Id = 2, Name = "Album 2" }
        };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        GetAllAlbumsQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<AlbumDto> result = await handler.HandleAsync(new GetAllAlbumsQuery(), CancellationToken.None);

        // Assert
        List<AlbumDto> list = result.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Album 1", list[0].Name);
        Assert.Equal("Album 2", list[1].Name);
    }
}

public class GetAlbumsByArtistIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return albums fetched by artist id")]
    public async Task Handle_ShouldReturnAlbums_FetchedByArtistId()
    {
        // Arrange
        List<IAlbumEntity> albums = new()
        {
            new AlbumEntity { Id = 1, ArtistId = 10, Name = "A" },
            new AlbumEntity { Id = 2, ArtistId = 10, Name = "B" }
        };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByArtistIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        GetAlbumsByArtistIdQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<AlbumDto> result = await handler.HandleAsync(new GetAlbumsByArtistIdQuery(10), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
        repository.Verify(r => r.GetByArtistIdAsync(10, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class GetAlbumsByGenreIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return albums fetched by genre id")]
    public async Task Handle_ShouldReturnAlbums_FetchedByGenreId()
    {
        // Arrange
        List<IAlbumEntity> albums = new()
        {
            new AlbumEntity { Id = 3, GenreId = 5, Name = "C" }
        };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByGenreIdAsync(5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        GetAlbumsByGenreIdQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<AlbumDto> result = await handler.HandleAsync(new GetAlbumsByGenreIdQuery(5), CancellationToken.None);

        // Assert
        Assert.Single(result);
        repository.Verify(r => r.GetByGenreIdAsync(5, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}
