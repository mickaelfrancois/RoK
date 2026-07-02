using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.ViewModels.Albums.Services;

namespace Rok.PresentationTests.ViewModels.Albums.Services;

public class AlbumsPlaybackServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerService> _player = new();

    private AlbumsPlaybackService BuildService() => new(_mediator, _player.Object, NullLogger<AlbumsPlaybackService>.Instance);

    [Fact(DisplayName = "PlayAlbumsAsync should skip work when no album ids are provided")]
    public async Task PlayAlbumsAsync_ShouldSkip_WhenEmpty()
    {
        // Arrange
        AlbumsPlaybackService sut = BuildService();

        // Act
        await sut.PlayAlbumsAsync(Array.Empty<long>());

        // Assert
        Assert.Empty(_mediator.Sent<GetTracksByAlbumListRequest>());
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "PlayAlbumsAsync should query tracks by the provided album ids and load them")]
    public async Task PlayAlbumsAsync_ShouldQueryAndLoad_WhenIdsProvided()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 11 } };
        _mediator.Setup<GetTracksByAlbumListRequest, IEnumerable<TrackDto>>()
                 .Returns(tracks);
        AlbumsPlaybackService sut = BuildService();

        // Act
        await sut.PlayAlbumsAsync(new long[] { 1, 2 });

        // Assert
        GetTracksByAlbumListRequest sent = Assert.Single(_mediator.Sent<GetTracksByAlbumListRequest>());
        Assert.True(sent.AlbumsId.SequenceEqual(new long[] { 1, 2 }));
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.Count == 2), It.IsAny<TrackDto>()), Times.Once);
    }

    [Fact(DisplayName = "PlayAlbumsAsync should route a single album to the ordered request without reordering")]
    public async Task PlayAlbumsAsync_ShouldRouteSingleAlbumToOrderedRequest_WhenOneId()
    {
        // Arrange
        List<TrackDto> ordered = new() { new TrackDto { Id = 1 }, new TrackDto { Id = 2 }, new TrackDto { Id = 3 } };
        _mediator.Setup<GetTracksByAlbumIdRequest, IEnumerable<TrackDto>>()
                 .Returns(ordered);
        AlbumsPlaybackService sut = BuildService();

        // Act
        await sut.PlayAlbumsAsync(new long[] { 42 });

        // Assert
        GetTracksByAlbumIdRequest sent = Assert.Single(_mediator.Sent<GetTracksByAlbumIdRequest>());
        Assert.Equal(42L, sent.GenreId);
        Assert.Empty(_mediator.Sent<GetTracksByAlbumListRequest>());
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.SequenceEqual(ordered)), It.IsAny<TrackDto>()), Times.Once);
    }
}