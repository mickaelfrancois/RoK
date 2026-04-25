using MiF.Mediator.Interfaces;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Command;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Track;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumStatisticsServiceTests
{
    private readonly Mock<IMediator> _mediator = new();

    private AlbumStatisticsService BuildService() => new(_mediator.Object);

    [Fact(DisplayName = "UpdateIfNeededAsync should not send a command when statistics already match")]
    public async Task UpdateIfNeededAsync_ShouldSkip_WhenStatsMatch()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, TrackCount = 0, Duration = 0 };
        AlbumStatisticsService sut = BuildService();

        // Act
        bool result = await sut.UpdateIfNeededAsync(album, Array.Empty<TrackViewModel>());

        // Assert
        Assert.False(result);
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateAlbumStatisticsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UpdateIfNeededAsync should send a command and update the album when stats differ")]
    public async Task UpdateIfNeededAsync_ShouldUpdate_WhenStatsDiffer()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, TrackCount = 5, Duration = 600 };
        AlbumStatisticsService sut = BuildService();

        // Act — empty tracks differ from TrackCount=5
        bool result = await sut.UpdateIfNeededAsync(album, Array.Empty<TrackViewModel>());

        // Assert
        Assert.True(result);
        Assert.Equal(0, album.TrackCount);
        Assert.Equal(0, album.Duration);
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<UpdateAlbumStatisticsCommand>(c => c.Id == 1 && c.TrackCount == 0 && c.Duration == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
