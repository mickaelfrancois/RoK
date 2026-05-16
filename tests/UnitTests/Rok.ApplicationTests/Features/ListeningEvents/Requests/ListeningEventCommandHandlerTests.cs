using Moq;
using Rok.Application.Features.ListeningEvents.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.ListeningEvents.Requests;

public class CreateListeningEventCommandHandlerTests
{
    [Fact(DisplayName = "Handle should ignore event as fast change when played under 30 seconds and under 20 percent")]
    public async Task Handle_ShouldIgnoreEventAsFastChange_WhenPlayedUnder30SecondsAndUnder20Percent()
    {
        // Arrange
        Mock<IListeningEventRepository> repository = new();
        CreateListeningEventRequestHandler handler = new(repository.Object);
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 10, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(0, result.Value);
        repository.Verify(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "Handle should persist event and mark it as skipped when under 20 percent but played more than 30 seconds")]
    public async Task Handle_ShouldPersistEvent_AndMarkItAsSkipped_WhenUnder20PercentButPlayedMoreThan30Seconds()
    {
        // Arrange
        Mock<IListeningEventRepository> repository = new();
        ListeningEventEntity? capturedEntity = null;
        repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(88);
        CreateListeningEventRequestHandler handler = new(repository.Object);
        CreateListeningEventRequest command = new() { TrackId = 1, AlbumId = 2, ArtistId = 3, GenreId = 4, DurationPlayed = 40, DurationTotal = 500 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(88, result.Value);
        Assert.NotNull(capturedEntity);
        Assert.True(capturedEntity!.WasSkipped);
        Assert.Equal(1, capturedEntity.TrackId);
        Assert.Equal(40, capturedEntity.DurationPlayed);
    }

    [Fact(DisplayName = "Handle should persist event without skip flag when completion rate reaches 20 percent")]
    public async Task Handle_ShouldPersistEventWithoutSkipFlag_WhenCompletionRateReaches20Percent()
    {
        // Arrange
        Mock<IListeningEventRepository> repository = new();
        ListeningEventEntity? capturedEntity = null;
        repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(7);
        CreateListeningEventRequestHandler handler = new(repository.Object);
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 60, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(7, result.Value);
        Assert.NotNull(capturedEntity);
        Assert.False(capturedEntity!.WasSkipped);
    }

    [Fact(DisplayName = "Handle should return failure when repository returns non-positive id")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryReturnsNonPositiveId()
    {
        // Arrange
        Mock<IListeningEventRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        CreateListeningEventRequestHandler handler = new(repository.Object);
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 100, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("listening_event.create_failed");
    }
}
