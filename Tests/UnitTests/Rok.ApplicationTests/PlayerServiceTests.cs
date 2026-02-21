using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Player;

namespace Rok.ApplicationTests;

public class PlayerServiceTests
{
    private readonly Mock<IPlayerEngine> mockPlayerEngine;
    private readonly Mock<IAppOptions> mockAppOptions;
    private readonly Mock<ILogger<PlayerService>> mockLogger;
    private readonly PlayerService playerService;

    public PlayerServiceTests()
    {
        // Setup commun pour tous les tests
        mockPlayerEngine = new Mock<IPlayerEngine>();
        mockAppOptions = new Mock<IAppOptions>();
        mockLogger = new Mock<ILogger<PlayerService>>();

        mockPlayerEngine.Setup(o => o.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        mockAppOptions.SetupGet(o => o.CrossFade).Returns(false);

        playerService = new PlayerService(mockPlayerEngine.Object, mockAppOptions.Object, null, mockLogger.Object);
    }

    [Fact]
    public void NextTrack_ShouldAdvanceToNextTrack()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2 });

        // Act
        playerService.Next();

        // Assert
        Assert.Equal(track2, playerService.CurrentTrack);
    }

    [Fact]
    public void NextTrack_ShouldLoopToFirstTrack_WhenLoopingIsEnabled()
    {
        // Arrange
        playerService.IsLoopingEnabled = true;

        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2 });

        playerService.Next(); // Move to track 2

        // Act
        playerService.Next(); // Should loop back to track 1

        // Assert
        Assert.Equal(track1, playerService.CurrentTrack);
    }

    [Fact]
    public void NextTrack_ShouldStop_WhenLoopingIsDisabled()
    {
        // Arrange
        playerService.IsLoopingEnabled = false;

        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2 });

        playerService.Next(); // Move to track 2

        // Act
        playerService.Next(); // Should stop playback

        // Assert
        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
    }

    [Fact]
    public void PreviousTrack_ShouldGoToPreviousTrack()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2 });

        playerService.Next(); // Move to track 2

        // Act
        playerService.Previous(); // Should go back to track 1

        // Assert
        Assert.Equal(track1, playerService.CurrentTrack);
    }

    [Fact]
    public void PreviousTrack_ShouldLoopToLastTrack_WhenLoopingIsEnabled()
    {
        // Arrange
        playerService.IsLoopingEnabled = true;

        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2 });

        // Act
        playerService.Previous(); // Should loop to track 2

        // Assert
        Assert.Equal(track2, playerService.CurrentTrack);
    }

    [Fact]
    public void PreviousTrack_ShouldStop_WhenLoopingIsDisabled()
    {
        // Arrange
        playerService.IsLoopingEnabled = false;
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2 });

        // Act
        playerService.Previous(); // Should stop playback

        // Assert
        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
    }

    [Fact]
    public void Pause_ShouldPausePlayback()
    {
        // Arrange
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });

        // Act
        playerService.Pause();

        // Assert
        Assert.Equal(EPlaybackState.Paused, playerService.PlaybackState);
    }

    [Fact]
    public void Play_ShouldStartPlayback()
    {
        // Arrange
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });

        // Act
        playerService.Play();

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact]
    public void Stop_ShouldStopPlayback()
    {
        // Arrange
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });
        playerService.Play(); // Start playback

        // Act
        playerService.Stop(true);

        // Assert
        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
    }

    [Fact]
    public void Start_ShouldStartPlayback()
    {
        // Arrange
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });

        // Act
        playerService.Start();

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact]
    public void Start_ShouldStartPlaybackWithTrack_WhenTrackIsProvided()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        TrackDto track3 = new() { Id = 3, Title = "Track 3" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track2, track3 });

        // Act
        playerService.Start(track2);

        // Assert
        Assert.Equal(track2, playerService.CurrentTrack);
    }

    [Fact]
    public void AddTracksToPlaylist_ShouldAddTracksToPlaylist()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };

        playerService.LoadPlaylist(new List<TrackDto> { track1 });

        // Act
        playerService.AddTracksToPlaylist(new List<TrackDto> { track2 });

        // Assert
        Assert.Contains(track2, playerService.Playlist);
    }

    [Fact]
    public void AddTracksToPlaylist_ShouldStartPlayback_WhenPlaylistContainsNoTracks()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };

        // Act
        playerService.AddTracksToPlaylist(new List<TrackDto> { track2 });

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact]
    public void InsertTracksToPlaylist_ShouldInsertTracksAtSpecifiedIndex()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        TrackDto track3 = new() { Id = 3, Title = "Track 3" };
        playerService.LoadPlaylist(new List<TrackDto> { track1, track3 });

        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto> { track2 }, 1);

        // Assert
        Assert.Equal(track2, playerService.Playlist[1]);
    }

    [Fact]
    public void InsertTracksToPlaylist_ShouldInsertTracksAtEnd_WhenIndexIsNull()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };
        playerService.LoadPlaylist(new List<TrackDto> { track1 });

        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto> { track2 }, null);

        // Assert
        Assert.Equal(track2, playerService.Playlist.Last());
    }

    [Fact]
    public void InsertTracksToPlaylist_ShouldInsertTracksAtEnd_WhenIndexIsOutOfRange()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };

        playerService.LoadPlaylist(new List<TrackDto> { track1 });

        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto> { track2 }, 10);

        // Assert
        Assert.Equal(track2, playerService.Playlist.Last());
    }

    [Fact]
    public void IsMuted_ShouldSetVolumeZero_WhenMuted()
    {
        // Arrange
        playerService.Volume = 50;

        // Act
        playerService.IsMuted = true;

        // Assert
        Assert.Equal(0, playerService.Volume);
    }

    [Fact]
    public void IsMuted_ShouldRestoreVolume_WhenUnmuted()
    {
        // Arrange
        playerService.Volume = 50;
        playerService.IsMuted = true;

        // Act
        playerService.IsMuted = false;

        // Assert
        Assert.Equal(50, playerService.Volume);
    }
}