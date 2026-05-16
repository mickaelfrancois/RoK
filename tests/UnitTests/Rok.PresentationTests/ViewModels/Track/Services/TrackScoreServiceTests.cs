using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Track.Services;

namespace Rok.PresentationTests.ViewModels.Track.Services;

public class TrackScoreServiceTests
{
    private readonly FakeMediator _mediator = new();

    private TrackScoreService BuildService() => new(_mediator, new Messenger());

    [Fact(DisplayName = "UpdateScoreAsync should send an UpdateScoreRequest with the provided values")]
    public async Task UpdateScoreAsync_ShouldSendUpdateScoreRequest()
    {
        // Arrange
        TrackScoreService sut = BuildService();

        // Act
        await sut.UpdateScoreAsync(trackId: 42, score: 5);

        // Assert
        UpdateScoreRequest sent = Assert.Single(_mediator.Sent<UpdateScoreRequest>());
        Assert.Equal(42, sent.TrackId);
        Assert.Equal(5, sent.Score);
    }
}
