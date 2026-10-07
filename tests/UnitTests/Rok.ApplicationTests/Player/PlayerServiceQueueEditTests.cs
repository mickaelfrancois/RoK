using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceQueueEditTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Mock<IMixCueProvider> _cues = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceQueueEditTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
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
        mixCues: _cues.Object,
        logger: NullLogger<PlayerService>.Instance);

    private static TrackDto BuildTrack(long id) => new() { Id = id, Title = $"t{id}", Duration = 100 };

    private static List<TrackDto> BuildTracks(int count) => [.. Enumerable.Range(1, count).Select(i => BuildTrack(i))];

    private static List<long> Ids(IEnumerable<TrackDto> tracks) => [.. tracks.Select(t => t.Id)];

    private PlayerService BuildPlayingAt(List<TrackDto> tracks, int currentIndex)
    {
        PlayerService sut = BuildService();
        sut.LoadPlaylist(tracks, tracks[currentIndex]);

        return sut;
    }

    [Fact(DisplayName = "when_upcoming_track_is_moved_then_play_order_follows")]
    public void Moving_an_upcoming_track_reorders_the_queue()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(4);
        PlayerService sut = BuildPlayingAt([.. tracks], 0);
        List<PlaylistChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(messages.Add);

        // Act
        bool moved = sut.MoveUpcoming(3, 2);

        // Assert
        Assert.True(moved);
        Assert.Equal([1, 2, 4, 3], Ids(sut.Playlist));
        Assert.Single(messages);
    }

    [Fact(DisplayName = "when_track_is_moved_to_next_position_then_pending_transition_is_invalidated")]
    public void Moving_a_track_to_the_next_position_invalidates_the_pending_transition()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 0);
        _engine.Invocations.Clear();

        // Act
        bool moved = sut.MoveUpcoming(3, 1);

        // Assert
        Assert.True(moved);
        Assert.Equal(4, sut.GetQueue()[0].Id);
        _engine.Verify(o => o.ClearNextTrack(), Times.Once);
    }

    [Fact(DisplayName = "when_move_keeps_the_imminent_track_then_pending_transition_is_kept")]
    public void Moving_behind_the_imminent_track_keeps_the_pending_transition()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 0);
        _engine.Invocations.Clear();

        // Act
        bool moved = sut.MoveUpcoming(3, 2);

        // Assert
        Assert.True(moved);
        Assert.Equal(2, sut.GetQueue()[0].Id);
        _engine.Verify(o => o.ClearNextTrack(), Times.Never);
    }

    [Theory(DisplayName = "when_moving_current_or_played_track_then_move_is_refused")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Moving_the_current_or_a_played_track_is_refused(int fromIndex)
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(5);
        PlayerService sut = BuildPlayingAt([.. tracks], 2);
        List<PlaylistChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(messages.Add);

        // Act
        bool moved = sut.MoveUpcoming(fromIndex, 4);

        // Assert
        Assert.False(moved);
        Assert.Equal(Ids(tracks), Ids(sut.Playlist));
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "when_target_is_before_current_then_track_lands_right_after_current")]
    public void Moving_before_the_current_track_lands_right_after_it()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(5), 1);

        // Act
        bool moved = sut.MoveUpcoming(3, 0);

        // Assert
        Assert.True(moved);
        Assert.Equal([1, 2, 4, 3, 5], Ids(sut.Playlist));
        Assert.Equal(2, sut.CurrentTrack!.Id);
    }

    [Fact(DisplayName = "when_target_is_out_of_range_then_it_is_clamped_to_queue_end")]
    public void Moving_beyond_the_end_is_clamped_to_the_queue_end()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 0);

        // Act
        bool moved = sut.MoveUpcoming(1, 99);

        // Assert
        Assert.True(moved);
        Assert.Equal([1, 3, 4, 2], Ids(sut.Playlist));
    }

    [Theory(DisplayName = "when_from_index_is_out_of_range_or_equals_target_then_move_is_refused")]
    [InlineData(99, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 99)]
    public void Moving_with_an_invalid_source_or_same_target_is_refused(int fromIndex, int toIndex)
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(4);
        PlayerService sut = BuildPlayingAt([.. tracks], 0);
        List<PlaylistChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(messages.Add);

        // Act
        bool moved = sut.MoveUpcoming(fromIndex, toIndex);

        // Assert
        Assert.False(moved);
        Assert.Equal(Ids(tracks), Ids(sut.Playlist));
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "when_in_radio_mode_then_move_is_refused")]
    public void Moving_in_radio_mode_is_refused()
    {
        // Arrange
        _engine.Setup(o => o.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        PlayerService sut = BuildService();
        sut.PlayRadioStation(new RadioStationDto(1, "Radio", "http://stream", null, null, null, null, null, null, DateTime.UtcNow, null));

        // Act
        bool moved = sut.MoveUpcoming(1, 2);

        // Assert
        Assert.False(moved);
    }

    [Fact(DisplayName = "when_shuffle_is_disabled_after_a_move_then_original_order_is_restored_without_the_move")]
    public void Disabling_shuffle_after_a_move_restores_the_original_order()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(10);
        PlayerService sut = BuildPlayingAt([.. tracks], 2);
        sut.IsShuffleEnabled = true;
        TrackDto current = sut.CurrentTrack!;
        sut.MoveUpcoming(sut.Playlist.Count - 1, 3);

        // Act
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal(Ids(tracks), Ids(sut.Playlist));
        Assert.Equal(current, sut.CurrentTrack);
    }

    [Fact(DisplayName = "when_mix_plan_is_armed_and_imminent_track_moves_then_mix_preparation_is_rerequested")]
    public void Moving_the_imminent_track_rerequests_the_mix_preparation()
    {
        // Arrange
        _appOptions.SetupGet(o => o.MixMode).Returns(true);
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
        _cues.Setup(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new OutroCues(90, 0));
        _cues.Setup(c => c.GetIntroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntroCues(1));
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 0);
        _engine.Invocations.Clear();

        // Act
        bool moved = sut.MoveUpcoming(3, 1);

        // Assert
        Assert.True(moved);
        _engine.Verify(o => o.ClearTransitionCue(), Times.AtLeastOnce);
    }

    private void RaiseMediaEnded() => _engine.Raise(m => m.OnMediaEnded += null, _engine.Object, EventArgs.Empty);

    private void SetupRadio() => _engine.Setup(o => o.SetStream(It.IsAny<RadioStationDto>())).Returns(true);

    private static RadioStationDto BuildStation() => new(1, "Radio", "http://stream", null, null, null, null, null, null, DateTime.UtcNow, null);

    [Fact(DisplayName = "when_clear_upcoming_then_current_continues_and_nothing_follows")]
    public void Clearing_the_upcoming_tracks_keeps_history_and_current()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 1);
        List<PlaylistChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(messages.Add);

        // Act
        int removed = sut.ClearUpcoming();

        // Assert
        Assert.Equal(2, removed);
        Assert.Equal([1, 2], Ids(sut.Playlist));
        Assert.Equal(2, sut.CurrentTrack!.Id);
        Assert.False(sut.CanNext);
        Assert.Single(messages);
    }

    [Fact(DisplayName = "when_clear_upcoming_then_pending_transition_is_invalidated")]
    public void Clearing_the_upcoming_tracks_invalidates_the_pending_transition()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 0);
        _engine.Invocations.Clear();

        // Act
        sut.ClearUpcoming();

        // Assert
        _engine.Verify(o => o.ClearNextTrack(), Times.Once);
    }

    [Fact(DisplayName = "when_nothing_is_upcoming_then_clear_returns_zero_without_event")]
    public void Clearing_with_nothing_upcoming_does_nothing()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(3), 2);
        List<PlaylistChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(messages.Add);
        _engine.Invocations.Clear();

        // Act
        int removed = sut.ClearUpcoming();

        // Assert
        Assert.Equal(0, removed);
        Assert.Empty(messages);
        _engine.Verify(o => o.ClearNextTrack(), Times.Never);
    }

    [Fact(DisplayName = "when_shuffle_is_disabled_after_clear_then_removed_tracks_do_not_come_back")]
    public void Disabling_shuffle_after_a_clear_does_not_bring_back_removed_tracks()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(10), 2);
        sut.IsShuffleEnabled = true;
        TrackDto current = sut.CurrentTrack!;
        List<long> keptIds = Ids(sut.Playlist.Take(sut.Playlist.IndexOf(current) + 1));
        sut.ClearUpcoming();

        // Act
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal(current, sut.CurrentTrack);
        Assert.Equal(keptIds.Order(), Ids(sut.Playlist).Order());
        Assert.Equal(current.Id, sut.Playlist[^1].Id);
    }

    [Fact(DisplayName = "when_clear_upcoming_in_repeat_all_then_the_remaining_queue_loops_from_its_start")]
    public void Clearing_in_repeat_all_loops_over_the_remaining_queue()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(3), 1);
        sut.RepeatMode = ERepeatMode.All;
        sut.ClearUpcoming();
        _engine.Invocations.Clear();

        // Act
        RaiseMediaEnded();

        // Assert
        Assert.True(sut.CanNext);
        Assert.Equal(1, sut.CurrentTrack!.Id);
        Assert.Equal([1, 2], Ids(sut.Playlist));
    }

    [Fact(DisplayName = "when_clear_upcoming_in_repeat_one_then_current_still_repeats")]
    public void Clearing_in_repeat_one_keeps_repeating_the_current_track()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(3), 1);
        sut.RepeatMode = ERepeatMode.One;
        TrackDto current = sut.CurrentTrack!;
        sut.ClearUpcoming();
        _engine.Invocations.Clear();

        // Act
        RaiseMediaEnded();

        // Assert
        _engine.Verify(o => o.SetTrack(current, It.IsAny<float>()), Times.Once);
        Assert.Same(current, sut.CurrentTrack);
    }

    [Fact(DisplayName = "when_in_radio_mode_then_clear_upcoming_returns_zero")]
    public void Clearing_in_radio_mode_returns_zero()
    {
        // Arrange
        SetupRadio();
        PlayerService sut = BuildService();
        sut.PlayRadioStation(BuildStation());

        // Act
        int removed = sut.ClearUpcoming();

        // Assert
        Assert.Equal(0, removed);
    }

    [Fact(DisplayName = "when_clear_upcoming_then_playback_is_not_restarted")]
    public void Clearing_does_not_restart_playback()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(4), 1);
        _engine.Invocations.Clear();

        // Act
        sut.ClearUpcoming();

        // Assert
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
        _engine.Verify(o => o.Stop(), Times.Never);
    }

    [Fact(DisplayName = "upcoming_count_counts_tracks_after_current")]
    public void Upcoming_count_counts_tracks_after_the_current_one()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(3), 1);

        // Act
        int count = sut.UpcomingCount;

        // Assert
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = "upcoming_count_is_zero_in_radio_mode")]
    public void Upcoming_count_is_zero_in_radio_mode()
    {
        // Arrange
        SetupRadio();
        PlayerService sut = BuildService();
        sut.PlayRadioStation(BuildStation());

        // Act
        int count = sut.UpcomingCount;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "upcoming_count_follows_move_and_clear")]
    public void Upcoming_count_is_unchanged_by_a_move_and_zero_after_a_clear()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(5), 1);

        // Act
        sut.MoveUpcoming(4, 2);
        int afterMove = sut.UpcomingCount;
        sut.ClearUpcoming();
        int afterClear = sut.UpcomingCount;

        // Assert
        Assert.Equal(3, afterMove);
        Assert.Equal(0, afterClear);
    }
}