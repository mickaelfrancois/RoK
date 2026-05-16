using Moq;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Track.Services;

namespace Rok.PresentationTests.ViewModels.Track.Services;

public class TrackScoreServiceTests
{
    private readonly Mock<IMediator> _mediator = new();

    private TrackScoreService BuildService() => new(_mediator.Object);

    [Fact(DisplayName = "UpdateScoreAsync should send an UpdateScoreRequest with the provided values")]
    public async Task UpdateScoreAsync_ShouldSendUpdateScoreRequest()
    {
        // Arrange
        TrackScoreService sut = BuildService();

        // Act
        await sut.UpdateScoreAsync(trackId: 42, score: 5);

        // Assert
        _mediator.Verify(m => m.Send(
            It.Is<UpdateScoreRequest>(c => c.TrackId == 42 && c.Score == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
