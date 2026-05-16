using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.ViewModels.Artists.Services;

namespace Rok.PresentationTests.ViewModels.Artists.Services;

public class ArtistsPlaybackServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlayerService> _player = new();

    private ArtistsPlaybackService BuildService() => new(_mediator.Object, _player.Object, NullLogger<ArtistsPlaybackService>.Instance);

    [Fact(DisplayName = "PlayArtistsAsync should skip work when no artist ids are provided")]
    public async Task PlayArtistsAsync_ShouldSkip_WhenEmpty()
    {
        // Arrange
        ArtistsPlaybackService sut = BuildService();

        // Act
        await sut.PlayArtistsAsync(Array.Empty<long>());

        // Assert
        _mediator.Verify(m => m.Send(It.IsAny<GetTracksByArtistListRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "PlayArtistsAsync should query tracks by the provided artist ids and load them")]
    public async Task PlayArtistsAsync_ShouldQueryAndLoad_WhenIdsProvided()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 11 } };
        _mediator.Setup(m => m.Send(It.Is<GetTracksByArtistListRequest>(q => q.ArtistIds.SequenceEqual(new long[] { 1, 2 })), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        ArtistsPlaybackService sut = BuildService();

        // Act
        await sut.PlayArtistsAsync(new long[] { 1, 2 });

        // Assert
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.Count == 2), It.IsAny<TrackDto>()), Times.Once);
    }
}
