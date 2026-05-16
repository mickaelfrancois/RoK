using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Listening.Services;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningDataLoaderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IArtistViewModelFactory> _artistFactory = new();
    private readonly Mock<ITrackViewModelFactory> _trackFactory = new();

    private ListeningDataLoader BuildService() =>
        new(_mediator.Object, _artistFactory.Object, _trackFactory.Object, NullLogger<ListeningDataLoader>.Instance);

    [Fact(DisplayName = "GetTracksByArtistAsync should return an empty list when the artist has no tracks")]
    public async Task GetTracksByArtistAsync_ShouldReturnEmpty_WhenNoTracks()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetTracksByArtistIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TrackDto>());
        ListeningDataLoader sut = BuildService();

        // Act
        List<TrackDto> result = await sut.GetTracksByArtistAsync(artistId: 7, maxTracks: 5, excludeTrackIds: Array.Empty<long>());

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "GetTracksByArtistAsync should exclude tracks listed in excludeTrackIds")]
    public async Task GetTracksByArtistAsync_ShouldExcludeListedTracks()
    {
        // Arrange
        List<TrackDto> tracks = Enumerable.Range(1, 5).Select(i => new TrackDto { Id = i }).ToList();
        _mediator.Setup(m => m.Send(It.IsAny<GetTracksByArtistIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        ListeningDataLoader sut = BuildService();

        // Act
        List<TrackDto> result = await sut.GetTracksByArtistAsync(artistId: 7, maxTracks: 10, excludeTrackIds: new long[] { 2, 4 });

        // Assert
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, t => t.Id == 2 || t.Id == 4);
    }

    [Fact(DisplayName = "GetTracksByArtistAsync should cap the result at maxTracks")]
    public async Task GetTracksByArtistAsync_ShouldRespectMaxTracks()
    {
        // Arrange
        List<TrackDto> tracks = Enumerable.Range(1, 10).Select(i => new TrackDto { Id = i }).ToList();
        _mediator.Setup(m => m.Send(It.IsAny<GetTracksByArtistIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        ListeningDataLoader sut = BuildService();

        // Act
        List<TrackDto> result = await sut.GetTracksByArtistAsync(artistId: 7, maxTracks: 3, excludeTrackIds: Array.Empty<long>());

        // Assert
        Assert.Equal(3, result.Count);
    }
}
