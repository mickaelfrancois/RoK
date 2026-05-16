using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.ViewModels.Artists.Services;

namespace Rok.PresentationTests.ViewModels.Artists.Services;

public class ArtistsPlaybackServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerService> _player = new();

    private ArtistsPlaybackService BuildService() => new(_mediator, _player.Object, NullLogger<ArtistsPlaybackService>.Instance);

    [Fact(DisplayName = "PlayArtistsAsync should skip work when no artist ids are provided")]
    public async Task PlayArtistsAsync_ShouldSkip_WhenEmpty()
    {
        // Arrange
        ArtistsPlaybackService sut = BuildService();

        // Act
        await sut.PlayArtistsAsync(Array.Empty<long>());

        // Assert
        Assert.Empty(_mediator.Sent<GetTracksByArtistListRequest>());
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "PlayArtistsAsync should query tracks by the provided artist ids and load them")]
    public async Task PlayArtistsAsync_ShouldQueryAndLoad_WhenIdsProvided()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 11 } };
        _mediator.Setup<GetTracksByArtistListRequest, IEnumerable<TrackDto>>()
                 .Returns(tracks);
        ArtistsPlaybackService sut = BuildService();

        // Act
        await sut.PlayArtistsAsync(new long[] { 1, 2 });

        // Assert
        GetTracksByArtistListRequest sent = Assert.Single(_mediator.Sent<GetTracksByArtistListRequest>());
        Assert.True(sent.ArtistIds.SequenceEqual(new long[] { 1, 2 }));
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.Count == 2), It.IsAny<TrackDto>()), Times.Once);
    }
}