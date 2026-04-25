using MiF.Mediator.Interfaces;
using Moq;
using Rok.Application.Features.Tracks.Command;
using Rok.ViewModels.Track.Services;

namespace Rok.PresentationTests.ViewModels.Track.Services;

public class TrackScoreServiceTests
{
    private readonly Mock<IMediator> _mediator = new();

    private TrackScoreService BuildService() => new(_mediator.Object);

    [Fact(DisplayName = "UpdateScoreAsync should send an UpdateScoreCommand with the provided values")]
    public async Task UpdateScoreAsync_ShouldSendUpdateScoreCommand()
    {
        // Arrange
        TrackScoreService sut = BuildService();

        // Act
        await sut.UpdateScoreAsync(trackId: 42, score: 5);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<UpdateScoreCommand>(c => c.TrackId == 42 && c.Score == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
