using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Artists.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Track;

namespace Rok.PresentationTests.ViewModels.Artist.Services;

public class ArtistStatisticsServiceTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ArtistStatisticsService BuildService() => new(_mediator.Object);

    [Fact(DisplayName = "NeedUpdate should return false when all counts already match")]
    public void NeedUpdate_ShouldReturnFalse_WhenAllCountsMatch()
    {
        // Arrange
        ArtistDto artist = new() { AlbumCount = 0, LiveCount = 0, CompilationCount = 0, BestofCount = 0, TrackCount = 0 };
        ArtistStatisticsService sut = BuildService();

        // Act
        bool result = sut.NeedUpdate(artist, Array.Empty<AlbumViewModel>(), Array.Empty<TrackViewModel>());

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "NeedUpdate should return true when track count differs")]
    public void NeedUpdate_ShouldReturnTrue_WhenTrackCountDiffers()
    {
        // Arrange
        ArtistDto artist = new() { TrackCount = 5 };
        ArtistStatisticsService sut = BuildService();

        // Act
        bool result = sut.NeedUpdate(artist, Array.Empty<AlbumViewModel>(), Array.Empty<TrackViewModel>());

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "UpdateIfNeededAsync should not send a command when stats already match")]
    public async Task UpdateIfNeededAsync_ShouldSkip_WhenStatsMatch()
    {
        // Arrange
        ArtistDto artist = new() { AlbumCount = 0, LiveCount = 0, CompilationCount = 0, BestofCount = 0, TrackCount = 0 };
        ArtistStatisticsService sut = BuildService();

        // Act
        bool result = await sut.UpdateIfNeededAsync(artist, Array.Empty<AlbumViewModel>(), Array.Empty<TrackViewModel>());

        // Assert
        Assert.False(result);
        _mediator.Verify(m => m.Send(It.IsAny<UpdateArtistStatisticsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UpdateIfNeededAsync should send a command and reset counts when stats differ from empty inputs")]
    public async Task UpdateIfNeededAsync_ShouldUpdate_WhenStatsDiffer()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, TrackCount = 5, AlbumCount = 2, BestofCount = 1 };
        ArtistStatisticsService sut = BuildService();

        // Act
        bool result = await sut.UpdateIfNeededAsync(artist, Array.Empty<AlbumViewModel>(), Array.Empty<TrackViewModel>());

        // Assert
        Assert.True(result);
        Assert.Equal(0, artist.TrackCount);
        Assert.Equal(0, artist.AlbumCount);
        _mediator.Verify(m => m.Send(
            It.Is<UpdateArtistStatisticsRequest>(c => c.Id == 1 && c.TrackCount == 0 && c.AlbumCount == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
