using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceRepeatModeTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceRepeatModeTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _engine.SetupGet(o => o.Length).Returns(100);
        _appOptions.SetupGet(o => o.CrossFade).Returns(false);
        _appOptions.SetupGet(o => o.CrossfadeDurationSeconds).Returns(5);
    }

    private PlayerService BuildService() => new(
        _callDetection.Object,
        _engine.Object,
        _appOptions.Object,
        discordService: null,
        smtcService: null,
        albumPicture: _albumPicture.Object,
        timeProvider: TimeProvider.System,
        messenger: _messenger,
        mixCues: Mock.Of<IMixCueProvider>(),
        logger: NullLogger<PlayerService>.Instance);

    private static TrackDto BuildTrack(long id, long? albumId = null) => new() { Id = id, Title = $"t{id}", ArtistName = "artist", AlbumName = "album", AlbumId = albumId };

    private PlayerService BuildLoadedService(ERepeatMode mode, params TrackDto[] tracks)
    {
        PlayerService sut = BuildService();
        sut.LoadPlaylist([.. tracks]);
        sut.RepeatMode = mode;

        return sut;
    }

    private void RaiseMediaEnded() => _engine.Raise(m => m.OnMediaEnded += null, _engine.Object, EventArgs.Empty);

    private void RaiseMediaAboutToEnd() => _engine.Raise(m => m.OnMediaAboutToEnd += null, _engine.Object, EventArgs.Empty);

    [Fact(DisplayName = "when_repeat_mode_changes_a_repeat_mode_changed_message_is_sent")]
    public void Changing_repeat_mode_sends_a_repeat_mode_changed_message()
    {
        // Arrange
        PlayerService sut = BuildService();
        List<RepeatModeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<RepeatModeChanged>(messages.Add);

        // Act
        sut.RepeatMode = ERepeatMode.One;

        // Assert
        RepeatModeChanged message = Assert.Single(messages);
        Assert.Equal(ERepeatMode.One, message.Mode);
        Assert.Equal(ERepeatMode.One, sut.RepeatMode);
    }

    [Fact(DisplayName = "when_repeat_mode_is_set_to_its_current_value_no_message_is_sent")]
    public void Setting_repeat_mode_to_its_current_value_sends_nothing()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.RepeatMode = ERepeatMode.All;
        List<RepeatModeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<RepeatModeChanged>(messages.Add);

        // Act
        sut.RepeatMode = ERepeatMode.All;

        // Assert
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "when_media_ends_in_repeat_one_the_same_track_is_reloaded_and_played")]
    public void Media_ended_in_repeat_one_replays_the_same_track()
    {
        // Arrange
        TrackDto first = BuildTrack(1);
        PlayerService sut = BuildLoadedService(ERepeatMode.One, first, BuildTrack(2));
        _engine.Invocations.Clear();

        // Act
        RaiseMediaEnded();

        // Assert
        _engine.Verify(o => o.SetTrack(first, It.IsAny<float>()), Times.Once);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Never);
        Assert.Same(first, sut.CurrentTrack);
        Assert.Equal(EPlaybackState.Playing, sut.PlaybackState);
    }

    [Fact(DisplayName = "when_media_ends_in_repeat_off_on_the_last_track_playback_stops")]
    public void Media_ended_in_repeat_off_on_the_last_track_stops()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(ERepeatMode.Off, BuildTrack(1));
        _engine.Invocations.Clear();

        // Act
        RaiseMediaEnded();

        // Assert
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
        Assert.Equal(EPlaybackState.Stopped, sut.PlaybackState);
    }

    [Fact(DisplayName = "when_next_is_pressed_in_repeat_one_the_next_track_plays")]
    public void Next_in_repeat_one_moves_to_the_next_track()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(ERepeatMode.One, BuildTrack(1), BuildTrack(2));

        // Act
        sut.Next();

        // Assert
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "when_next_is_pressed_in_repeat_one_on_the_last_track_playback_stops")]
    public void Next_in_repeat_one_on_the_last_track_stops_like_repeat_off()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(ERepeatMode.One, BuildTrack(1), BuildTrack(2));
        sut.Next();

        // Act
        sut.Next();

        // Assert
        Assert.Equal(EPlaybackState.Stopped, sut.PlaybackState);
        Assert.Equal(2, sut.CurrentTrack?.Id);
        Assert.False(sut.CanNext);
    }

    [Fact(DisplayName = "when_previous_is_pressed_in_repeat_one_on_the_first_track_the_queue_does_not_wrap")]
    public void Previous_in_repeat_one_on_the_first_track_does_not_wrap()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(ERepeatMode.One, BuildTrack(1), BuildTrack(2));

        // Act
        sut.Previous();

        // Assert
        Assert.False(sut.CanPrevious);
        Assert.Equal(1, sut.CurrentTrack?.Id);
        Assert.Equal(EPlaybackState.Stopped, sut.PlaybackState);
    }

    [Fact(DisplayName = "when_repeat_all_is_active_next_wraps_to_the_first_track")]
    public void Next_in_repeat_all_wraps_to_the_first_track()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(ERepeatMode.All, BuildTrack(1), BuildTrack(2));
        sut.Next();

        // Act
        sut.Next();

        // Assert
        Assert.Equal(1, sut.CurrentTrack?.Id);
        Assert.True(sut.CanNext);
    }

    [Fact(DisplayName = "when_about_to_end_in_repeat_one_no_gapless_track_is_queued")]
    public void About_to_end_in_repeat_one_queues_nothing()
    {
        // Arrange
        BuildLoadedService(ERepeatMode.One, BuildTrack(1), BuildTrack(2));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "when_about_to_end_in_repeat_one_with_crossfade_no_crossfade_starts")]
    public void About_to_end_in_repeat_one_with_crossfade_starts_nothing()
    {
        // Arrange
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
        _engine.SetupGet(o => o.Position).Returns(95);
        BuildLoadedService(ERepeatMode.One, BuildTrack(1, albumId: 7), BuildTrack(2, albumId: 8));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "when_repeat_one_is_enabled_after_a_gapless_queue_the_queued_track_is_cleared")]
    public void Enabling_repeat_one_after_a_gapless_queue_clears_the_next_track()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(ERepeatMode.Off, BuildTrack(1), BuildTrack(2));
        RaiseMediaAboutToEnd();
        _engine.Invocations.Clear();

        // Act
        sut.RepeatMode = ERepeatMode.One;

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "when_the_service_is_built_the_repeat_mode_is_read_from_the_options")]
    public void Building_the_service_reads_the_repeat_mode_from_the_options()
    {
        // Arrange
        _appOptions.SetupProperty(o => o.RepeatMode, ERepeatMode.One);

        // Act
        PlayerService sut = BuildService();

        // Assert
        Assert.Equal(ERepeatMode.One, sut.RepeatMode);
    }

    [Fact(DisplayName = "when_the_repeat_mode_changes_it_is_written_back_to_the_options")]
    public void Changing_the_repeat_mode_writes_it_back_to_the_options()
    {
        // Arrange
        PlayerService sut = BuildService();

        // Act
        sut.RepeatMode = ERepeatMode.All;

        // Assert
        _appOptions.VerifySet(o => o.RepeatMode = ERepeatMode.All, Times.Once);
    }
}