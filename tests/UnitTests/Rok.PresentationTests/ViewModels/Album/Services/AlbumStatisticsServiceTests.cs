using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Track;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumStatisticsServiceTests
{
    private readonly FakeMediator _mediator = new();

    private AlbumStatisticsService BuildService() => new(_mediator);

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
        Assert.Empty(_mediator.Sent<UpdateAlbumStatisticsRequest>());
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
        UpdateAlbumStatisticsRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumStatisticsRequest>());
        Assert.Equal(1, sent.Id);
        Assert.Equal(0, sent.TrackCount);
        Assert.Equal(0, sent.Duration);
    }
}
