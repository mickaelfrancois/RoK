using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;
using Rok.Application.Player.Output;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceGaplessTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Mock<ISystemMediaTransportControlsService> _smtc = new();
    private readonly Mock<IDiscordRichPresenceService> _discord = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceGaplessTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _engine.SetupGet(o => o.Position).Returns(90);
        _engine.SetupGet(o => o.Length).Returns(100);
        _appOptions.SetupGet(o => o.CrossfadeDurationSeconds).Returns(5);
    }

    private PlayerService BuildService(bool crossfade = false)
    {
        _appOptions.SetupGet(o => o.CrossFade).Returns(crossfade);

        return new(_callDetection.Object, _engine.Object, _appOptions.Object, _discord.Object, _smtc.Object, _albumPicture.Object, TimeProvider.System, _messenger, Mock.Of<IMixCueProvider>(), NullLogger<PlayerService>.Instance);
    }

    private static TrackDto BuildTrack(long id, long? albumId = null, int? trackNumber = null) => new() { Id = id, Title = $"t{id}", ArtistName = "artist", AlbumName = "album", AlbumId = albumId, TrackNumber = trackNumber };

    private void RaiseMediaAboutToEnd() => _engine.Raise(m => m.OnMediaAboutToEnd += null, _engine.Object, EventArgs.Empty);

    private void RaiseMediaEnded() => _engine.Raise(m => m.OnMediaEnded += null, _engine.Object, EventArgs.Empty);

    private void RaiseGaplessTransition(TrackDto track, double previousPosition = 100) =>
        _engine.Raise(m => m.OnGaplessTransition += null, _engine.Object, new GaplessTransitionEventArgs(track, previousPosition));

    private void VerifyNoCrossfade() =>
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "when_crossfade_is_disabled_about_to_end_queues_the_next_track")]
    public void WhenCrossfadeIsDisabled_AboutToEnd_QueuesNextTrack()
    {
        // Arrange
        PlayerService sut = BuildService(crossfade: false);
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]);

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        VerifyNoCrossfade();
    }

    [Fact(DisplayName = "when_same_album_consecutive_with_crossfade_about_to_end_queues_the_next_track")]
    public void WhenSameAlbumConsecutiveWithCrossfade_AboutToEnd_QueuesNextTrack()
    {
        // Arrange
        PlayerService sut = BuildService(crossfade: true);
        sut.LoadPlaylist([BuildTrack(1, albumId: 7, trackNumber: 1), BuildTrack(2, albumId: 7, trackNumber: 2)]);

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        VerifyNoCrossfade();
    }

    [Fact(DisplayName = "exclusive_mode_queues_gapless_instead_of_crossfade")]
    public void WhenExclusiveMode_AboutToEnd_QueuesGaplessInsteadOfCrossfade()
    {
        // Arrange
        PlayerService sut = BuildService(crossfade: true);
        _appOptions.SetupGet(o => o.OutputMode).Returns(EAudioOutputMode.Exclusive);
        sut.LoadPlaylist([BuildTrack(1, albumId: 7, trackNumber: 1), BuildTrack(2, albumId: 8, trackNumber: 1)]);

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        VerifyNoCrossfade();
    }

    [Fact(DisplayName = "when_album_changes_with_crossfade_about_to_end_starts_a_crossfade")]
    public void WhenAlbumChangesWithCrossfade_AboutToEnd_StartsCrossfade()
    {
        // Arrange
        _engine.SetupGet(o => o.Position).Returns(95);
        PlayerService sut = BuildService(crossfade: true);
        sut.LoadPlaylist([BuildTrack(1, albumId: 7, trackNumber: 9), BuildTrack(2, albumId: 8, trackNumber: 1)]);

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        _engine.Verify(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "when_muted_with_crossfade_about_to_end_queues_the_next_track")]
    public void WhenMutedWithCrossfade_AboutToEnd_QueuesNextTrack()
    {
        // Arrange
        PlayerService sut = BuildService(crossfade: true);
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]);
        sut.IsMuted = true;

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        VerifyNoCrossfade();
    }

    [Fact(DisplayName = "when_queue_is_refused_the_track_ends_and_next_reloads")]
    public void WhenQueueIsRefused_TrackEndsAndNextReloads()
    {
        // Arrange
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(false);
        PlayerService sut = BuildService(crossfade: false);
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]);
        RaiseMediaAboutToEnd();

        // Act
        RaiseMediaEnded();

        // Assert
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "when_gapless_transition_occurs_current_track_advances_without_set_track")]
    public void WhenGaplessTransitionOccurs_CurrentTrackAdvancesWithoutSetTrack()
    {
        // Arrange
        TrackDto second = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), second]);
        RaiseMediaAboutToEnd();

        // Act
        RaiseGaplessTransition(second);

        // Assert
        Assert.Equal(2, sut.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Playing, sut.PlaybackState);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "when_gapless_transition_occurs_media_changed_carries_previous_duration")]
    public void WhenGaplessTransitionOccurs_MediaChangedCarriesPreviousDuration()
    {
        // Arrange
        TrackDto first = BuildTrack(1);
        TrackDto second = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([first, second]);
        RaiseMediaAboutToEnd();

        List<MediaChangedMessage> messages = [];
        using IDisposable subscription = _messenger.Subscribe<MediaChangedMessage>(messages.Add);

        // Act
        RaiseGaplessTransition(second, previousPosition: 187.6);

        // Assert
        MediaChangedMessage message = Assert.Single(messages);
        Assert.Same(second, message.NewTrack);
        Assert.Same(first, message.PreviousTrack);
        Assert.Equal(187L, message.DurationPlayed);
    }

    [Fact(DisplayName = "when_gapless_transition_occurs_smtc_is_updated")]
    public void WhenGaplessTransitionOccurs_SmtcIsUpdated()
    {
        // Arrange
        TrackDto second = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), second]);
        RaiseMediaAboutToEnd();
        _smtc.Invocations.Clear();

        // Act
        RaiseGaplessTransition(second);

        // Assert
        _smtc.Verify(s => s.UpdateTrackInfoAsync(second, It.IsAny<string?>()), Times.Once);
        _smtc.Verify(s => s.UpdatePlaybackState(PlaybackStatus.Playing), Times.Once);
    }

    [Fact(DisplayName = "when_gapless_transition_occurs_discord_presence_is_updated")]
    public void WhenGaplessTransitionOccurs_DiscordPresenceIsUpdated()
    {
        // Arrange
        _appOptions.SetupGet(o => o.DiscordRichPresenceEnabled).Returns(true);
        TrackDto second = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), second]);
        RaiseMediaAboutToEnd();
        _discord.Invocations.Clear();

        // Act
        RaiseGaplessTransition(second);

        // Assert
        _discord.Verify(d => d.UpdatePresence("t2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact(DisplayName = "when_the_imminent_track_is_removed_the_queued_track_is_cleared")]
    public void WhenImminentTrackIsRemoved_QueuedTrackIsCleared()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2), BuildTrack(3)]);
        RaiseMediaAboutToEnd();

        // Act
        sut.RemoveUpcomingByTrack(2);

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.Once);
    }

    [Fact(DisplayName = "when_tracks_are_inserted_next_the_queued_track_is_cleared")]
    public void WhenTracksAreInsertedNext_QueuedTrackIsCleared()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]);
        RaiseMediaAboutToEnd();

        // Act
        sut.InsertTracksToPlaylist([BuildTrack(9)]);

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.Once);
    }

    [Fact(DisplayName = "when_tracks_are_appended_after_a_non_last_track_the_queue_is_kept")]
    public void WhenTracksAreAppendedAfterNonLastTrack_QueueIsKept()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]);
        RaiseMediaAboutToEnd();

        // Act
        sut.AddTracksToPlaylist([BuildTrack(9)]);

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.Never);
    }

    [Fact(DisplayName = "when_playlist_is_shuffled_the_queued_track_is_cleared")]
    public void WhenPlaylistIsShuffled_QueuedTrackIsCleared()
    {
        // Arrange
        List<TrackDto> tracks = [.. Enumerable.Range(1, 40).Select(id => new TrackDto { Id = id, Title = $"t{id}", ArtistId = id, ArtistName = $"a{id}" })];
        PlayerService sut = BuildService();
        sut.LoadPlaylist(tracks);
        RaiseMediaAboutToEnd();
        long imminentBefore = sut.GetQueue()[0].Id;

        // Act
        do
        {
            sut.ShuffleTracks();
        }
        while (sut.GetQueue()[0].Id == imminentBefore);

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "when_seeking_the_queued_track_is_cleared")]
    public async Task WhenSeeking_QueuedTrackIsCleared()
    {
        // Arrange
        TaskCompletionSource cleared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _engine.Setup(o => o.ClearNextTrack()).Callback(() => cleared.TrySetResult());
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]);
        RaiseMediaAboutToEnd();

        // Act
        sut.Position = 10;
        await cleared.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.Once);
    }

    [Fact(DisplayName = "when_stale_gapless_transition_arrives_next_reloads_the_right_track")]
    public void WhenStaleGaplessTransitionArrives_NextReloadsRightTrack()
    {
        // Arrange
        TrackDto second = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), second, BuildTrack(3)]);
        RaiseMediaAboutToEnd();
        sut.RemoveUpcomingByTrack(2);

        // Act
        RaiseGaplessTransition(second);

        // Assert
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 3), It.IsAny<float>()), Times.Once);
        Assert.Equal(3, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "when_at_last_track_without_looping_about_to_end_does_nothing")]
    public void WhenAtLastTrackWithoutLooping_AboutToEndDoesNothing()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1)]);

        // Act
        RaiseMediaAboutToEnd();
        RaiseMediaEnded();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
        VerifyNoCrossfade();
        Assert.Equal(EPlaybackState.Stopped, sut.PlaybackState);
    }

    [Fact(DisplayName = "when_looping_on_last_track_the_first_track_is_queued")]
    public void WhenLoopingOnLastTrack_FirstTrackIsQueued()
    {
        // Arrange
        TrackDto first = BuildTrack(1);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([first, BuildTrack(2)]);
        sut.IsLoopingEnabled = true;
        sut.Next();

        // Act
        RaiseMediaAboutToEnd();
        RaiseGaplessTransition(first);

        // Assert
        _engine.Verify(o => o.QueueNextTrack(first, It.IsAny<float>()), Times.Once);
        Assert.Equal(1, sut.CurrentTrack?.Id);
        Assert.Equal(2, sut.GetQueue()[0].Id);
    }

    [Fact(DisplayName = "when_next_is_pressed_after_queue_the_pending_transition_is_dropped")]
    public void WhenNextIsPressedAfterQueue_PendingTransitionIsDropped()
    {
        // Arrange
        TrackDto second = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1), second, BuildTrack(3)]);
        RaiseMediaAboutToEnd();
        sut.Next();

        // Act
        RaiseGaplessTransition(second);

        // Assert
        Assert.Equal(2, sut.CurrentTrack?.Id);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 3), It.IsAny<float>()), Times.Never);
    }
}