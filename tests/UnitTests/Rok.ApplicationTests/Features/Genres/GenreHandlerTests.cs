using Moq;
using Rok.Application.Features.Genres.Command;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Genres;

public class UpdateGenreFavoriteCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates favorite")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesFavorite()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(1, true, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateGenreFavoriteCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateGenreFavoriteCommand(1, true), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update favorite")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateGenreFavoriteCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateGenreFavoriteCommand(1, false), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateGenretLastListenCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates last listen")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLastListen()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateGenretLastListenCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateGenretLastListenCommand(1), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateGenretLastListenCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateGenretLastListenCommand(1), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class ResetGenreListenCountCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository resets listen count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryResetsListenCount()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        ResetGenreListenCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new ResetGenreListenCountCommand(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetGenreListenCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new ResetGenreListenCountCommand(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class GetGenreByIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped genre when genre exists")]
    public async Task Handle_ShouldReturnMappedGenre_WhenGenreExists()
    {
        // Arrange
        GenreEntity entity = new() { Id = 2, Name = "Rock" };
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(2, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        GetGenreByIdQueryHandler handler = new(repository.Object);

        // Act
        Result<GenreDto> result = await handler.HandleAsync(new GetGenreByIdQuery(2), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Rock", result.Value!.Name);
    }

    [Fact(DisplayName = "Handle should return NotFound failure when genre does not exist")]
    public async Task Handle_ShouldReturnNotFoundFailure_WhenGenreDoesNotExist()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((GenreEntity?)null);
        GetGenreByIdQueryHandler handler = new(repository.Object);

        // Act
        Result<GenreDto> result = await handler.HandleAsync(new GetGenreByIdQuery(999), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class GetAllGenresQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return all mapped genres from repository")]
    public async Task Handle_ShouldReturnAllMappedGenres_FromRepository()
    {
        // Arrange
        List<GenreEntity> genres = new()
        {
            new() { Id = 1, Name = "Rock" },
            new() { Id = 2, Name = "Pop" }
        };
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genres);
        GetAllGenresQueryHandler handler = new(repository.Object);

        // Act
        IEnumerable<GenreDto> result = await handler.HandleAsync(new GetAllGenresQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
    }
}
