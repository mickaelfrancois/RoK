using Moq;
using Rok.Application.Dto;
using Rok.Application.Player;
using Rok.Services.Taskbar;

namespace Rok.PresentationTests.Services.Taskbar;

public class ThumbBarStatePolicyTests
{
    private static RadioStationDto Station() => new(1, "Station", "http://stream", null, null, null, null, null, null, DateTime.UtcNow, null);

    [Fact(DisplayName = "playing_state_shows_the_pause_icon")]
    public void Compute_Playing_ShowsPause()
    {
        // Act
        var state = ThumbBarStatePolicy.Compute(EPlaybackState.Playing, true, true, true, 3);

        // Assert
        Assert.True(state.ShowPause);
    }

    [Theory(DisplayName = "paused_and_stopped_states_show_the_play_icon")]
    [InlineData(EPlaybackState.Paused)]
    [InlineData(EPlaybackState.Stopped)]
    [InlineData(EPlaybackState.Ended)]
    public void Compute_NotPlaying_ShowsPlay(EPlaybackState playbackState)
    {
        // Act
        var state = ThumbBarStatePolicy.Compute(playbackState, true, true, true, 3);

        // Assert
        Assert.False(state.ShowPause);
    }

    [Fact(DisplayName = "play_pause_is_disabled_when_nothing_is_loaded")]
    public void Compute_NothingLoaded_DisablesPlayPause()
    {
        // Act
        var state = ThumbBarStatePolicy.Compute(EPlaybackState.Stopped, false, false, false, 0);

        // Assert
        Assert.False(state.IsPlayPauseEnabled);
    }

    [Fact(DisplayName = "play_pause_is_enabled_when_a_track_or_a_station_is_loaded")]
    public void From_TrackOrStationLoaded_EnablesPlayPause()
    {
        // Arrange
        var withTrack = new Mock<IPlayerService>();
        withTrack.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Stopped);
        withTrack.SetupGet(p => p.CurrentTrack).Returns(new TrackDto());
        withTrack.SetupGet(p => p.Playlist).Returns([]);

        var withStation = new Mock<IPlayerService>();
        withStation.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Stopped);
        withStation.SetupGet(p => p.CurrentStation).Returns(Station());
        withStation.SetupGet(p => p.Playlist).Returns([]);

        // Act
        var trackState = ThumbBarStatePolicy.From(withTrack.Object);
        var stationState = ThumbBarStatePolicy.From(withStation.Object);

        // Assert
        Assert.True(trackState.IsPlayPauseEnabled);
        Assert.True(stationState.IsPlayPauseEnabled);
    }

    [Theory(DisplayName = "previous_and_next_follow_can_previous_and_can_next")]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Compute_WithQueue_CopiesCanPreviousAndCanNext(bool canPrevious, bool canNext)
    {
        // Act
        var state = ThumbBarStatePolicy.Compute(EPlaybackState.Playing, canPrevious, canNext, true, 2);

        // Assert
        Assert.Equal(canPrevious, state.IsPreviousEnabled);
        Assert.Equal(canNext, state.IsNextEnabled);
    }

    [Fact(DisplayName = "radio_mode_disables_previous_and_next")]
    public void From_RadioMode_DisablesPreviousAndNext()
    {
        // Arrange
        var player = new Mock<IPlayerService>();
        player.SetupGet(p => p.Mode).Returns(EPlaybackMode.Radio);
        player.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Playing);
        player.SetupGet(p => p.CurrentStation).Returns(Station());
        player.SetupGet(p => p.CanNext).Returns(false);
        player.SetupGet(p => p.CanPrevious).Returns(false);
        player.SetupGet(p => p.Playlist).Returns([]);

        // Act
        var state = ThumbBarStatePolicy.From(player.Object);

        // Assert
        Assert.False(state.IsPreviousEnabled);
        Assert.False(state.IsNextEnabled);
        Assert.True(state.IsPlayPauseEnabled);
    }

    [Fact(DisplayName = "empty_queue_disables_previous_and_next_even_when_looping")]
    public void Compute_EmptyQueue_DisablesPreviousAndNext()
    {
        // Act
        var state = ThumbBarStatePolicy.Compute(EPlaybackState.Stopped, true, true, false, 0);

        // Assert
        Assert.False(state.IsPreviousEnabled);
        Assert.False(state.IsNextEnabled);
    }
}