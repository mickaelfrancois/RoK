using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.ViewModels.Albums.Services;

namespace Rok.PresentationTests.ViewModels.Albums.Services;

public class AlbumsPlaybackServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlayerService> _player = new();

    private AlbumsPlaybackService BuildService() => new(_mediator.Object, _player.Object, NullLogger<AlbumsPlaybackService>.Instance);

    [Fact(DisplayName = "PlayAlbumsAsync should skip work when no album ids are provided")]
    public async Task PlayAlbumsAsync_ShouldSkip_WhenEmpty()
    {
        // Arrange
        AlbumsPlaybackService sut = BuildService();

        // Act
        await sut.PlayAlbumsAsync(Array.Empty<long>());

        // Assert
        _mediator.Verify(m => m.Send(It.IsAny<GetTracksByAlbumListRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "PlayAlbumsAsync should query tracks by the provided album ids and load them")]
    public async Task PlayAlbumsAsync_ShouldQueryAndLoad_WhenIdsProvided()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 11 } };
        _mediator.Setup(m => m.Send(It.Is<GetTracksByAlbumListRequest>(q => q.AlbumsId.SequenceEqual(new long[] { 1, 2 })), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        AlbumsPlaybackService sut = BuildService();

        // Act
        await sut.PlayAlbumsAsync(new long[] { 1, 2 });

        // Assert
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.Count == 2), It.IsAny<TrackDto>()), Times.Once);
    }
}
