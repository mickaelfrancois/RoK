using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceVolumeTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Messenger _messenger = new();

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

    [Fact(DisplayName = "when_volume_changes_a_volume_changed_message_is_sent")]
    public void Changing_volume_sends_a_volume_changed_message()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 20;
        List<VolumeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<VolumeChanged>(messages.Add);

        // Act
        sut.Volume = 40;

        // Assert
        VolumeChanged message = Assert.Single(messages);
        Assert.Equal(new VolumeChanged(40, false), message);
    }

    [Fact(DisplayName = "when_volume_is_set_to_its_current_value_no_message_is_sent")]
    public void Setting_volume_to_its_current_value_sends_nothing()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 20;
        List<VolumeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<VolumeChanged>(messages.Add);

        // Act
        sut.Volume = 20;

        // Assert
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "when_play_is_called_without_volume_or_mute_change_no_message_is_sent")]
    public void Play_without_volume_or_mute_change_sends_nothing()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 20;
        List<VolumeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<VolumeChanged>(messages.Add);

        // Act
        sut.Play();

        // Assert
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "when_muting_a_single_volume_changed_message_reports_muted")]
    public void Muting_sends_a_single_message_reporting_muted()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 50;
        List<VolumeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<VolumeChanged>(messages.Add);

        // Act
        sut.IsMuted = true;

        // Assert
        VolumeChanged message = Assert.Single(messages);
        Assert.Equal(new VolumeChanged(0, true), message);
    }

    [Fact(DisplayName = "when_unmuting_the_previous_volume_is_restored_and_reported")]
    public void Unmuting_restores_the_volume_and_reports_it()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 50;
        sut.IsMuted = true;
        List<VolumeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<VolumeChanged>(messages.Add);

        // Act
        sut.IsMuted = false;

        // Assert
        VolumeChanged message = Assert.Single(messages);
        Assert.Equal(new VolumeChanged(50, false), message);
    }

    [Fact(DisplayName = "when_volume_is_raised_while_muted_the_player_is_unmuted")]
    public void Raising_volume_while_muted_unmutes()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 50;
        sut.IsMuted = true;
        List<VolumeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<VolumeChanged>(messages.Add);

        // Act
        sut.Volume = 5;

        // Assert
        Assert.False(sut.IsMuted);
        Assert.Equal(5, sut.Volume);
        VolumeChanged message = Assert.Single(messages);
        Assert.Equal(new VolumeChanged(5, false), message);
    }

    [Fact(DisplayName = "when_volume_is_set_to_zero_while_muted_mute_is_kept")]
    public void Setting_volume_to_zero_while_muted_keeps_mute()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 50;
        sut.IsMuted = true;

        // Act
        sut.Volume = 0;

        // Assert
        Assert.True(sut.IsMuted);
        Assert.Equal(0, sut.Volume);
    }

    [Fact(DisplayName = "when_toggling_mute_after_a_volume_change_while_muted_the_new_volume_is_kept")]
    public void Toggling_mute_after_changing_volume_while_muted_keeps_the_new_volume()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.Volume = 50;
        sut.IsMuted = true;
        sut.Volume = 10;

        // Act
        sut.IsMuted = true;
        sut.IsMuted = false;

        // Assert
        Assert.Equal(10, sut.Volume);
        Assert.False(sut.IsMuted);
    }
}