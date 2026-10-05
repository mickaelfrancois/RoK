using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceLoopingTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceLoopingTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _appOptions.SetupGet(o => o.CrossFade).Returns(false);
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

    [Fact(DisplayName = "when_looping_is_enabled_a_looping_changed_message_is_sent")]
    public void Enabling_looping_sends_a_looping_changed_message()
    {
        // Arrange
        PlayerService sut = BuildService();
        List<LoopingChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<LoopingChanged>(messages.Add);

        // Act
        sut.IsLoopingEnabled = true;

        // Assert
        LoopingChanged message = Assert.Single(messages);
        Assert.True(message.IsEnabled);
    }

    [Fact(DisplayName = "when_looping_is_set_to_its_current_value_no_message_is_sent")]
    public void Setting_looping_to_its_current_value_sends_nothing()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.IsLoopingEnabled = true;
        List<LoopingChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<LoopingChanged>(messages.Add);

        // Act
        sut.IsLoopingEnabled = true;

        // Assert
        Assert.Empty(messages);
    }
}