using MiF.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Track.Services;

namespace Rok.PresentationTests.ViewModels.Track.Services;

public class TrackDetailDataLoaderTests
{
    private readonly Mock<IMediator> _mediator = new();

    private TrackDetailDataLoader BuildService() => new(_mediator.Object, NullLogger<TrackDetailDataLoader>.Instance);

    [Fact(DisplayName = "LoadTrackAsync should return the track when the mediator succeeds")]
    public async Task LoadTrackAsync_ShouldReturnTrack_WhenSuccess()
    {
        // Arrange
        TrackDto track = new() { Id = 42, Title = "Song" };
        _mediator.Setup(m => m.Send(It.IsAny<GetTrackByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<TrackDto>.Success(track));
        TrackDetailDataLoader sut = BuildService();

        // Act
        TrackDto? result = await sut.LoadTrackAsync(42);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result!.Id);
    }

    [Fact(DisplayName = "LoadTrackAsync should return null when the mediator returns an error")]
    public async Task LoadTrackAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetTrackByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<TrackDto>.Fail("not found"));
        TrackDetailDataLoader sut = BuildService();

        // Act
        TrackDto? result = await sut.LoadTrackAsync(99);

        // Assert
        Assert.Null(result);
    }
}
