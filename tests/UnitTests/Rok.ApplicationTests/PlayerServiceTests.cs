using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests;

public class PlayerServiceTests
{
    private readonly Mock<IPlayerEngine> mockPlayerEngine;
    private readonly Mock<ICallDetectionService> mockCallDetectionService;
    private readonly Mock<IAppOptions> mockAppOptions;
    private readonly Mock<IAlbumPicture> mockAlbumPicture;
    private readonly Mock<ILogger<PlayerService>> mockLogger;
    private readonly FakeTimeProvider fakeTimeProvider;
    private readonly Messenger messenger = new();
    private readonly PlayerService playerService;

    public PlayerServiceTests()
    {
        mockPlayerEngine = new Mock<IPlayerEngine>();
        mockAppOptions = new Mock<IAppOptions>();
        mockLogger = new Mock<ILogger<PlayerService>>();
        mockCallDetectionService = new Mock<ICallDetectionService>();
        mockAlbumPicture = new Mock<IAlbumPicture>();
        fakeTimeProvider = new FakeTimeProvider();

        mockPlayerEngine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        mockAppOptions.SetupGet(o => o.CrossFade).Returns(false);

        playerService = new PlayerService(mockCallDetectionService.Object, mockPlayerEngine.Object, mockAppOptions.Object, null, null, mockAlbumPicture.Object, fakeTimeProvider, messenger, Mock.Of<IMixCueProvider>(), mockLogger.Object);
    }

    private static List<TrackDto> BuildTracks(int count)
    {
        return Enumerable.Range(1, count).Select(id => new TrackDto { Id = id, Title = $"Track {id}" }).ToList();
    }

    private void MarkUnreadable(params int[] ids)
    {
        foreach (int id in ids)
        {
            mockPlayerEngine.Setup(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == id), It.IsAny<float>())).Returns(false);
        }
    }

    private void VerifySetTrack(int id, Times times)
    {
        mockPlayerEngine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == id), It.IsAny<float>()), times);
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

    [Fact(DisplayName = "when_the_track_has_played_more_than_three_seconds_previous_restarts_it")]
    public void Previous_ShouldRestartCurrentTrack_WhenPositionIsOverThreshold()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(3);
        playerService.LoadPlaylist(tracks);
        playerService.Next();
        mockPlayerEngine.SetupGet(o => o.Position).Returns(10);

        // Act
        playerService.Previous();

        // Assert
        mockPlayerEngine.Verify(o => o.SetPosition(0), Times.Once);
        Assert.Equal(tracks[1], playerService.CurrentTrack);
    }

    [Theory(DisplayName = "when_the_track_has_played_three_seconds_or_less_previous_moves_to_the_previous_track")]
    [InlineData(2)]
    [InlineData(3)]
    public void Previous_ShouldMoveToPreviousTrack_WhenPositionIsAtOrUnderThreshold(double position)
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(3);
        playerService.LoadPlaylist(tracks);
        playerService.Next();
        mockPlayerEngine.SetupGet(o => o.Position).Returns(position);

        // Act
        playerService.Previous();

        // Assert
        Assert.Equal(tracks[0], playerService.CurrentTrack);
        mockPlayerEngine.Verify(o => o.SetPosition(It.IsAny<double>()), Times.Never);
    }

    [Fact(DisplayName = "when_the_first_track_has_played_more_than_three_seconds_previous_restarts_it_instead_of_stopping")]
    public void Previous_ShouldRestartFirstTrack_WhenPositionIsOverThresholdWithoutLooping()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(2);
        playerService.LoadPlaylist(tracks);
        playerService.Start();
        mockPlayerEngine.SetupGet(o => o.Position).Returns(10);

        // Act
        playerService.Previous();

        // Assert
        mockPlayerEngine.Verify(o => o.SetPosition(0), Times.Once);
        Assert.Equal(tracks[0], playerService.CurrentTrack);
        Assert.NotEqual(EPlaybackState.Stopped, playerService.PlaybackState);
    }

    [Theory(DisplayName = "on_the_first_track_previous_is_available_only_after_three_seconds")]
    [InlineData(10, true)]
    [InlineData(1, false)]
    public void CanPrevious_ShouldDependOnPosition_OnFirstTrackWithoutLooping(double position, bool expected)
    {
        // Arrange
        playerService.LoadPlaylist(BuildTracks(2));
        playerService.Start();
        mockPlayerEngine.SetupGet(o => o.Position).Returns(position);

        // Act
        bool canPrevious = playerService.CanPrevious;

        // Assert
        Assert.Equal(expected, canPrevious);
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

    [Fact(DisplayName = "next_skips_unreadable_track_and_plays_first_readable_one")]
    public void Next_skips_unreadable_track_and_plays_first_readable_one()
    {
        // Arrange
        MarkUnreadable(2);
        playerService.LoadPlaylist(BuildTracks(3));

        // Act
        playerService.Next();

        // Assert
        Assert.Equal(3, playerService.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
        VerifySetTrack(2, Times.Once());
        VerifySetTrack(3, Times.Once());

        playerService.Next();

        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
        Assert.Equal(3, playerService.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "next_skips_unreadable_tracks_with_looping_wraps_to_start")]
    public void Next_skips_unreadable_tracks_with_looping_wraps_to_start()
    {
        // Arrange
        playerService.IsLoopingEnabled = true;
        MarkUnreadable(3);
        playerService.LoadPlaylist(BuildTracks(3));
        playerService.Next();

        // Act
        playerService.Next();

        // Assert
        Assert.Equal(1, playerService.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact(DisplayName = "next_with_all_following_tracks_unreadable_stops_without_play")]
    public void Next_with_all_following_tracks_unreadable_stops_without_play()
    {
        // Arrange
        MarkUnreadable(2, 3);
        playerService.LoadPlaylist(BuildTracks(3));

        // Act
        playerService.Next();

        // Assert
        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
        Assert.Equal(1, playerService.CurrentTrack?.Id);
        mockPlayerEngine.Verify(o => o.Play(), Times.Once);
        VerifySetTrack(2, Times.Once());
        VerifySetTrack(3, Times.Once());
    }

    [Fact(DisplayName = "previous_skips_unreadable_track_backwards")]
    public void Previous_skips_unreadable_track_backwards()
    {
        // Arrange
        MarkUnreadable(2);
        playerService.LoadPlaylist(BuildTracks(3));
        playerService.Next();
        Assert.Equal(3, playerService.CurrentTrack?.Id);

        // Act
        playerService.Previous();

        // Assert
        Assert.Equal(1, playerService.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact(DisplayName = "previous_with_looping_wraps_to_last_readable_track")]
    public void Previous_with_looping_wraps_to_last_readable_track()
    {
        // Arrange
        playerService.IsLoopingEnabled = true;
        MarkUnreadable(3);
        playerService.LoadPlaylist(BuildTracks(3));

        // Act
        playerService.Previous();

        // Assert
        Assert.Equal(2, playerService.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact(DisplayName = "start_on_unreadable_track_plays_next_readable_one")]
    public void Start_on_unreadable_track_plays_next_readable_one()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(3);
        MarkUnreadable(2);

        // Act
        playerService.LoadPlaylist(tracks, tracks[1]);

        // Assert
        Assert.Equal(3, playerService.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact(DisplayName = "start_with_first_track_unreadable_plays_second")]
    public void Start_with_first_track_unreadable_plays_second()
    {
        // Arrange
        MarkUnreadable(1);

        // Act
        playerService.LoadPlaylist(BuildTracks(2));

        // Assert
        Assert.Equal(2, playerService.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
        VerifySetTrack(1, Times.Once());
        VerifySetTrack(2, Times.Once());
    }

    [Fact(DisplayName = "fully_unreadable_playlist_stops_after_one_attempt_per_track")]
    public void Fully_unreadable_playlist_stops_after_one_attempt_per_track()
    {
        // Arrange
        MarkUnreadable(1, 2, 3);

        // Act
        playerService.LoadPlaylist(BuildTracks(3));

        // Assert
        mockPlayerEngine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Exactly(3));
        mockPlayerEngine.Verify(o => o.Play(), Times.Never);
        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
        Assert.Null(playerService.CurrentTrack);
    }

    [Fact(DisplayName = "fully_unreadable_playlist_with_looping_does_not_loop_forever")]
    public void Fully_unreadable_playlist_with_looping_does_not_loop_forever()
    {
        // Arrange
        playerService.IsLoopingEnabled = true;
        playerService.LoadPlaylist(BuildTracks(3));
        MarkUnreadable(1, 2, 3);
        mockPlayerEngine.Invocations.Clear();

        // Act
        playerService.Next();

        // Assert
        mockPlayerEngine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Exactly(3));
        Assert.Equal(EPlaybackState.Stopped, playerService.PlaybackState);
        Assert.Equal(1, playerService.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "each_skipped_track_logs_a_warning")]
    public void Each_skipped_track_logs_a_warning()
    {
        // Arrange
        MarkUnreadable(2, 3);
        playerService.LoadPlaylist(BuildTracks(4));

        // Act
        playerService.Next();

        // Assert
        Assert.Equal(4, playerService.CurrentTrack?.Id);
        mockLogger.Verify(
            l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact(DisplayName = "skipping_tracks_keeps_played_duration_of_previous_track")]
    public void Skipping_tracks_keeps_played_duration_of_previous_track()
    {
        // Arrange
        double position = 0;
        mockPlayerEngine.SetupGet(o => o.Position).Returns(() => position);
        mockPlayerEngine.Setup(o => o.Stop()).Callback(() => position = 0);
        MarkUnreadable(2);
        List<TrackDto> tracks = BuildTracks(3);
        playerService.LoadPlaylist(tracks);
        position = 42;
        List<MediaChangedMessage> messages = [];
        using IDisposable subscription = messenger.Subscribe<MediaChangedMessage>(messages.Add);

        // Act
        playerService.Next();

        // Assert
        MediaChangedMessage message = Assert.Single(messages);
        Assert.Same(tracks[2], message.NewTrack);
        Assert.Same(tracks[0], message.PreviousTrack);
        Assert.Equal(42L, message.DurationPlayed);
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
        playerService.AddTracksToPlaylist(new List<TrackDto> { track1, track2 });

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

    [Fact(DisplayName = "when_tracks_are_inserted_into_an_empty_queue_playback_starts")]
    public void InsertTracksToPlaylist_ShouldStartPlayback_WhenPlaylistContainsNoTracks()
    {
        // Arrange
        TrackDto track1 = new() { Id = 1, Title = "Track 1" };
        TrackDto track2 = new() { Id = 2, Title = "Track 2" };

        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto> { track1, track2 });

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
        Assert.Equal(track1, playerService.CurrentTrack);
    }

    [Fact(DisplayName = "when_tracks_are_inserted_next_they_follow_the_current_track_in_order")]
    public void InsertTracksToPlaylist_ShouldInsertAfterCurrentTrackInOrder_WhenIndexIsNull()
    {
        // Arrange
        TrackDto current = new() { Id = 1, Title = "Current" };
        TrackDto later = new() { Id = 2, Title = "Later" };
        TrackDto first = new() { Id = 3, Title = "First" };
        TrackDto second = new() { Id = 4, Title = "Second" };
        playerService.AddTracksToPlaylist(new List<TrackDto> { current, later });

        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto> { first, second });

        // Assert
        Assert.Equal(new[] { current, first, second, later }, playerService.Playlist);
        Assert.Equal(current, playerService.CurrentTrack);
    }

    [Fact(DisplayName = "when_play_next_is_used_twice_the_last_insert_plays_first")]
    public void InsertTracksToPlaylist_ShouldPlaceLastInsertFirst_WhenCalledTwice()
    {
        // Arrange
        TrackDto current = new() { Id = 1, Title = "Current" };
        TrackDto x = new() { Id = 2, Title = "X" };
        TrackDto y = new() { Id = 3, Title = "Y" };
        playerService.AddTracksToPlaylist(new List<TrackDto> { current });

        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto> { x });
        playerService.InsertTracksToPlaylist(new List<TrackDto> { y });

        // Assert
        Assert.Equal(new[] { current, y, x }, playerService.Playlist);
    }

    [Fact(DisplayName = "when_no_track_is_inserted_into_an_empty_queue_playback_does_not_start")]
    public void InsertTracksToPlaylist_ShouldNotStartPlayback_WhenTracksAreEmpty()
    {
        // Act
        playerService.InsertTracksToPlaylist(new List<TrackDto>());

        // Assert
        Assert.Empty(playerService.Playlist);
        Assert.NotEqual(EPlaybackState.Playing, playerService.PlaybackState);
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

    [Fact]
    public void CallStateChanged_ShouldPauseAndResume_WhenPlaying()
    {
        // Arrange
        mockAppOptions.SetupGet(o => o.PauseOnCall).Returns(true);
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });

        // Act
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, true);
        EPlaybackState pausedState = playerService.PlaybackState;
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, false);

        // Assert
        Assert.Equal(EPlaybackState.Paused, pausedState);
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact]
    public void CallStateChanged_ShouldResume_WhenSmtcPausePrecedesCall()
    {
        // Arrange
        mockAppOptions.SetupGet(o => o.PauseOnCall).Returns(true);
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });
        playerService.HandleMediaControlCommand(new MediaControlCommandMessage(MediaControlCommandMessage.CommandType.Pause));

        // Act
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, true);
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, false);

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact]
    public void CallStateChanged_ShouldNotResume_WhenSmtcPauseTooOld()
    {
        // Arrange
        mockAppOptions.SetupGet(o => o.PauseOnCall).Returns(true);
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });
        playerService.HandleMediaControlCommand(new MediaControlCommandMessage(MediaControlCommandMessage.CommandType.Pause));
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(4));

        // Act
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, true);
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, false);

        // Assert
        Assert.Equal(EPlaybackState.Paused, playerService.PlaybackState);
    }

    [Fact]
    public void CallStateChanged_ShouldNotResume_WhenUserPausedManually()
    {
        // Arrange
        mockAppOptions.SetupGet(o => o.PauseOnCall).Returns(true);
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });
        playerService.Pause();

        // Act
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, true);
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, false);

        // Assert
        Assert.Equal(EPlaybackState.Paused, playerService.PlaybackState);
    }

    [Fact]
    public void CallStateChanged_ShouldDoNothing_WhenPauseOnCallDisabled()
    {
        // Arrange
        mockAppOptions.SetupGet(o => o.PauseOnCall).Returns(false);
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });

        // Act
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, true);

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }

    [Fact]
    public void Play_ShouldClearPauseReason()
    {
        // Arrange
        mockAppOptions.SetupGet(o => o.PauseOnCall).Returns(true);
        TrackDto track = new() { Id = 1, Title = "Track 1" };
        playerService.LoadPlaylist(new List<TrackDto> { track });
        playerService.HandleMediaControlCommand(new MediaControlCommandMessage(MediaControlCommandMessage.CommandType.Pause));
        playerService.Play();

        // Act
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, true);
        mockCallDetectionService.Raise(c => c.CallStateChanged += null, this, false);

        // Assert
        Assert.Equal(EPlaybackState.Playing, playerService.PlaybackState);
    }
}