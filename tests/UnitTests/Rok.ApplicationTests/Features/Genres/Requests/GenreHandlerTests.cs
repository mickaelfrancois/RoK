using Moq;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Genres.Requests;

public class UpdateGenreFavoriteRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates favorite")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesFavorite()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(1, true, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateGenreFavoriteRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateGenreFavoriteRequest(1, true), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update favorite")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateGenreFavoriteRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateGenreFavoriteRequest(1, false), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("genre.favorite_update_failed");
    }
}

public class UpdateGenretLastListenRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates last listen")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLastListen()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateGenretLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateGenretLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateGenretLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateGenretLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("genre.last_listen_update_failed");
    }
}

public class ResetGenreListenCountRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository resets listen count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryResetsListenCount()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        ResetGenreListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetGenreListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetGenreListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetGenreListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("genre.listen_count_reset_failed");
    }
}

public class GetGenreByIdRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped genre when genre exists")]
    public async Task Handle_ShouldReturnMappedGenre_WhenGenreExists()
    {
        // Arrange
        GenreEntity entity = new() { Id = 2, Name = "Rock" };
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(2, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        GetGenreByIdRequestHandler handler = new(repository.Object);

        // Act
        Result<GenreDto> result = await handler.Handle(new GetGenreByIdRequest(2), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("Rock", result.Value.Name);
    }

    [Fact(DisplayName = "Handle should return NotFound failure when genre does not exist")]
    public async Task Handle_ShouldReturnNotFoundFailure_WhenGenreDoesNotExist()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((GenreEntity?)null);
        GetGenreByIdRequestHandler handler = new(repository.Object);

        // Act
        Result<GenreDto> result = await handler.Handle(new GetGenreByIdRequest(999), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("genre.not_found");
    }
}

public class GetAllGenresRequestHandlerTests
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
        GetAllGenresRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<GenreDto> result = await handler.Handle(new GetAllGenresRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
    }
}