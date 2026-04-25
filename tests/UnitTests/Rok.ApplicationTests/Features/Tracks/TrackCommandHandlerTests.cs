using Moq;
using Rok.Application.Features.Tracks.Command;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Tracks;

public class UpdateScoreCommandHandlerTests
{
    [Fact(DisplayName = "Handle should forward track id and score to repository")]
    public async Task Handle_ShouldForwardTrackIdAndScore_ToRepository()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateScoreAsync(1, 5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateScoreCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateScoreCommand(1, 5), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        repository.Verify(r => r.UpdateScoreAsync(1, 5, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update score")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateScoreAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateScoreCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateScoreCommand(1, 5), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateSkipCountCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates skip count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesSkipCount()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateSkipCountAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateSkipCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateSkipCountCommand { TrackId = 1 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update skip count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateSkipCountAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateSkipCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateSkipCountCommand { TrackId = 1 }, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateTrackLastListenCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates last listen")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLastListen()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateTrackLastListenCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateTrackLastListenCommand(1), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateTrackLastListenCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateTrackLastListenCommand(1), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class ResetTrackListenCountCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository resets listen count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryResetsListenCount()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        ResetTrackListenCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new ResetTrackListenCountCommand(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetTrackListenCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new ResetTrackListenCountCommand(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateTrackGetLyricsLastAttemptCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates lyrics timestamp")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLyricsTimestamp()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateGetLyricsLastAttemptAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateTrackGetLyricsLastAttemptCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateTrackGetLyricsLastAttemptCommand(1), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update lyrics timestamp")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateGetLyricsLastAttemptAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateTrackGetLyricsLastAttemptCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateTrackGetLyricsLastAttemptCommand(1), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}
