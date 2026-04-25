using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Player;
using Rok.ViewModels.Tracks.Services;

namespace Rok.PresentationTests.ViewModels.Tracks.Services;

public class TracksPlaybackServiceTests
{
    private readonly Mock<IPlayerService> _player = new();

    private TracksPlaybackService BuildService() => new(_player.Object, NullLogger<TracksPlaybackService>.Instance);

    [Fact(DisplayName = "PlayTracks should not call LoadPlaylist when the input is empty")]
    public void PlayTracks_ShouldNotLoad_WhenEmpty()
    {
        // Arrange
        TracksPlaybackService sut = BuildService();

        // Act
        sut.PlayTracks(Array.Empty<TrackDto>());

        // Assert
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "PlayTracks should pass a single track unchanged to LoadPlaylist")]
    public void PlayTracks_ShouldLoadSingleTrack()
    {
        // Arrange
        TracksPlaybackService sut = BuildService();
        TrackDto track = new() { Id = 1 };

        // Act
        sut.PlayTracks(new[] { track });

        // Assert
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.Count == 1 && l[0].Id == 1), It.IsAny<TrackDto>()), Times.Once);
    }

    [Fact(DisplayName = "PlayTracks should load all tracks when multiple are provided")]
    public void PlayTracks_ShouldLoadAllTracks_WhenMultiple()
    {
        // Arrange
        TracksPlaybackService sut = BuildService();
        TrackDto[] tracks = new[] { new TrackDto { Id = 1 }, new TrackDto { Id = 2 }, new TrackDto { Id = 3 } };

        // Act
        sut.PlayTracks(tracks);

        // Assert
        _player.Verify(p => p.LoadPlaylist(It.Is<List<TrackDto>>(l => l.Count == 3), It.IsAny<TrackDto>()), Times.Once);
    }
}
