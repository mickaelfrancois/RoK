using Moq;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Tracks.Requests;

public class UpdateScoreCommandHandlerTests
{
    [Fact(DisplayName = "Handle should forward track id and score to repository")]
    public async Task Handle_ShouldForwardTrackIdAndScore_ToRepository()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateScoreAsync(1, 5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateScoreRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateScoreRequest(1, 5), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repository.Verify(r => r.UpdateScoreAsync(1, 5, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update score")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateScoreAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateScoreRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateScoreRequest(1, 5), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.score_update_failed");
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
        UpdateSkipCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateSkipCountRequest { TrackId = 1 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update skip count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateSkipCountAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateSkipCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateSkipCountRequest { TrackId = 1 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.skip_count_update_failed");
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
        UpdateTrackLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateTrackLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateTrackLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateTrackLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.last_listen_update_failed");
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
        ResetTrackListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetTrackListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetTrackListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetTrackListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.listen_count_reset_failed");
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
        UpdateTrackGetLyricsLastAttemptRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateTrackGetLyricsLastAttemptRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update lyrics timestamp")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.UpdateGetLyricsLastAttemptAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateTrackGetLyricsLastAttemptRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateTrackGetLyricsLastAttemptRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.lyrics_attempt_update_failed");
    }
}