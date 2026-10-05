using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Services.PlayerCommand;
using Rok.Services.Taskbar;

namespace Rok.PresentationTests.Services.Taskbar;

public class ThumbBarControllerTests
{
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IPlayerCommandService> _commands = new();
    private readonly Mock<IThumbBarHost> _host = new();
    private readonly Messenger _messenger = new();

    public ThumbBarControllerTests()
    {
        _player.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Stopped);
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto());
        _player.SetupGet(p => p.Playlist).Returns([new TrackDto(), new TrackDto()]);
        _player.SetupGet(p => p.CanNext).Returns(true);
        _player.SetupGet(p => p.CanPrevious).Returns(true);
    }

    private ThumbBarController CreateSut() => new(_player.Object, _commands.Object, _messenger, _host.Object, action => action(), NullLogger<ThumbBarController>.Instance);

    [Fact(DisplayName = "start_pushes_the_initial_state_to_the_host")]
    public void Start_PushesInitialState()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        sut.Start();

        // Assert
        _host.Verify(h => h.Apply(new ThumbBarState(false, true, true, true)), Times.Once);
        _host.Verify(h => h.Apply(It.IsAny<ThumbBarState>()), Times.Once);
    }

    [Fact(DisplayName = "media_state_change_updates_the_play_pause_icon")]
    public void MediaStateChanged_UpdatesPlayPauseIcon()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();
        _player.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Playing);

        // Act
        _messenger.Send(new MediaStateChanged(EPlaybackState.Playing));

        // Assert
        _host.Verify(h => h.Apply(It.Is<ThumbBarState>(s => s.ShowPause)), Times.Once);
    }

    [Fact(DisplayName = "playlist_change_enables_next")]
    public void PlaylistChanged_EnablesNext()
    {
        // Arrange
        _player.SetupGet(p => p.CanNext).Returns(false);
        using var sut = CreateSut();
        sut.Start();
        _player.SetupGet(p => p.CanNext).Returns(true);

        // Act
        _messenger.Send(new PlaylistChanged([new TrackDto()]));

        // Assert
        _host.Verify(h => h.Apply(It.Is<ThumbBarState>(s => s.IsNextEnabled)), Times.Once);
    }

    [Fact(DisplayName = "looping_change_refreshes_previous_and_next")]
    public void LoopingChanged_RefreshesPreviousAndNext()
    {
        // Arrange
        _player.SetupGet(p => p.CanNext).Returns(false);
        _player.SetupGet(p => p.CanPrevious).Returns(false);
        using var sut = CreateSut();
        sut.Start();
        _player.SetupGet(p => p.CanNext).Returns(true);
        _player.SetupGet(p => p.CanPrevious).Returns(true);

        // Act
        _messenger.Send(new LoopingChanged(true));

        // Assert
        _host.Verify(h => h.Apply(It.Is<ThumbBarState>(s => s.IsNextEnabled && s.IsPreviousEnabled)), Times.Once);
    }

    [Fact(DisplayName = "radio_station_change_disables_previous_and_next")]
    public void RadioStationChanged_DisablesPreviousAndNext()
    {
        // Arrange
        var station = new RadioStationDto(1, "Station", "http://stream", null, null, null, null, null, null, DateTime.UtcNow, null);
        using var sut = CreateSut();
        sut.Start();
        _player.SetupGet(p => p.CurrentStation).Returns(station);
        _player.SetupGet(p => p.Playlist).Returns([]);
        _player.SetupGet(p => p.CanNext).Returns(false);
        _player.SetupGet(p => p.CanPrevious).Returns(false);

        // Act
        _messenger.Send(new RadioStationChanged(station));

        // Assert
        _host.Verify(h => h.Apply(It.Is<ThumbBarState>(s => !s.IsNextEnabled && !s.IsPreviousEnabled && s.IsPlayPauseEnabled)), Times.Once);
    }

    [Fact(DisplayName = "media_changed_refreshes_the_state")]
    public void MediaChanged_RefreshesState()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();
        _player.SetupGet(p => p.CanPrevious).Returns(false);

        // Act
        _messenger.Send(new MediaChangedMessage(new TrackDto(), null, null));

        // Assert
        _host.Verify(h => h.Apply(It.Is<ThumbBarState>(s => !s.IsPreviousEnabled)), Times.Once);
    }

    [Fact(DisplayName = "unchanged_state_is_not_pushed_twice")]
    public void UnchangedState_IsPushedOnce()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();

        // Act
        _messenger.Send(new LoopingChanged(true));
        _messenger.Send(new MediaStateChanged(EPlaybackState.Stopped));

        // Assert
        _host.Verify(h => h.Apply(It.IsAny<ThumbBarState>()), Times.Once);
    }

    [Theory(DisplayName = "clicking_previous_play_pause_next_triggers_the_matching_command")]
    [InlineData(ThumbBarButton.Previous)]
    [InlineData(ThumbBarButton.PlayPause)]
    [InlineData(ThumbBarButton.Next)]
    public void Click_TriggersMatchingCommand(ThumbBarButton button)
    {
        // Arrange
        using var sut = CreateSut();
        sut.Start();

        // Act
        _host.Raise(h => h.ButtonClicked += null, _host.Object, button);

        // Assert
        _commands.Verify(c => c.Previous(), button == ThumbBarButton.Previous ? Times.Once() : Times.Never());
        _commands.Verify(c => c.Toggle(), button == ThumbBarButton.PlayPause ? Times.Once() : Times.Never());
        _commands.Verify(c => c.Next(), button == ThumbBarButton.Next ? Times.Once() : Times.Never());
    }

    [Fact(DisplayName = "clicking_a_disabled_button_does_nothing")]
    public void Click_DisabledButton_DoesNothing()
    {
        // Arrange
        _player.SetupGet(p => p.CanNext).Returns(false);
        using var sut = CreateSut();
        sut.Start();

        // Act
        _host.Raise(h => h.ButtonClicked += null, _host.Object, ThumbBarButton.Next);

        // Assert
        _commands.Verify(c => c.Next(), Times.Never);
    }

    [Fact(DisplayName = "after_dispose_messages_no_longer_reach_the_host")]
    public void Dispose_StopsMessagesReachingHost()
    {
        // Arrange
        var sut = CreateSut();
        sut.Start();
        sut.Dispose();
        _player.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Playing);

        // Act
        _messenger.Send(new MediaStateChanged(EPlaybackState.Playing));

        // Assert
        _host.Verify(h => h.Apply(It.IsAny<ThumbBarState>()), Times.Once);
        _host.Verify(h => h.Dispose(), Times.Once);
    }

    [Fact(DisplayName = "dispose_is_idempotent")]
    public void Dispose_IsIdempotent()
    {
        // Arrange
        var sut = CreateSut();
        sut.Start();

        // Act — deliberate double dispose to verify idempotency; using-declaration would defeat the test
#pragma warning disable IDISP017
        sut.Dispose();
        sut.Dispose();
#pragma warning restore IDISP017

        // Assert
        _host.Verify(h => h.Dispose(), Times.Once);
    }
}